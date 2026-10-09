using System;
using System.Collections.Generic;
using CatLib.CrashWatcher.Symbols;

namespace CatLib.CrashWatcher.Stacks;

internal sealed class StackUnwinder
{
    public const int MaxFrames = 64;
    public const int ScanBytes = 0x8000;
    public const int ScanPage = 0x1000;
    public const int MaxScans = 6;
    public const int MaxChain = 32;
    public const uint PastProlog = uint.MaxValue;
    public const string EndOfStack = "end of the stack";

    private const int PushNonvolatile = 0;
    private const int AllocLarge = 1;
    private const int AllocSmall = 2;
    private const int SetFramePointer = 3;
    private const int SaveNonvolatile = 4;
    private const int SaveNonvolatileFar = 5;
    private const int PushMachineFrame = 10;
    private const int SetFramePointerLarge = 11;
    private const int LastKnownOperation = 11;

    private static readonly int[] ExtraSlots = { 0, 1, 0, 0, 1, 2, 1, 2, 1, 2, 0, 2 };

    private readonly IProcessMemory _memory;
    private readonly ModuleSet _modules;
    private readonly CodeNames _names;
    private readonly byte[] _word = new byte[8];
    private bool _doubtful;

    public StackUnwinder(IProcessMemory memory, ModuleSet modules, CodeNames names)
    {
        _memory = memory;
        _modules = modules;
        _names = names;
    }

    public string LastStop { get; private set; } = string.Empty;

    public List<UnwoundFrame> Walk(RegisterSet start, int maxFrames = MaxFrames)
    {
        LastStop = "frame limit";
        var frames = new List<UnwoundFrame>();
        var registers = start.Copy();
        RegisterSet beforeScan = null;
        var scans = 0;
        var scanned = false;
        var doubtful = false;
        var interrupted = true;
        while (frames.Count < maxFrames)
        {
            if (registers.Rip == 0 && frames.Count > 0)
            {
                if (!Retry(frames, ref registers, beforeScan, ref scans))
                {
                    LastStop = EndOfStack;
                    break;
                }

                scanned = true;
                interrupted = false;
                continue;
            }

            var notCodeAtTop = frames.Count == 0 && !IsCode(registers.Rip);
            frames.Add(new UnwoundFrame(registers.Rip, registers.Rsp, scanned || doubtful, interrupted, notCodeAtTop));
            var top = interrupted;
            scanned = false;
            doubtful = false;
            if (notCodeAtTop)
            {
                if (!Pop(registers))
                {
                    LastStop = "unwinding failed";
                    break;
                }

                interrupted = false;
                continue;
            }

            var previous = registers.Rsp;
            _doubtful = false;
            var step = Step(registers, top);
            doubtful = _doubtful;
            interrupted = step == StepResult.MachineFrame;
            if (step == StepResult.NoUnwindData)
            {
                beforeScan = registers.Copy();
                if (scans >= MaxScans || !Scan(registers, registers.Rsp))
                {
                    LastStop = scans >= MaxScans ? "too many searches of the stack" : "no return address found on the stack";
                    break;
                }

                scans++;
                scanned = true;
                continue;
            }

            var notCode = registers.Rip != 0 && !IsCode(registers.Rip);
            if (step == StepResult.Failed || notCode || (step == StepResult.Unwound && registers.Rsp <= previous))
            {
                if (Retry(frames, ref registers, beforeScan, ref scans))
                {
                    scanned = true;
                    interrupted = false;
                    continue;
                }

                LastStop = step == StepResult.Failed ? "unwinding failed" : notCode ? "the next address is not code" : "the stack pointer went back";
                break;
            }
        }

        return frames;
    }

    public static bool FollowsCall(byte[] b)
    {
        return b[2] == 0xE8
               || (b[1] == 0xFF && b[2] == 0x15)
               || (b[5] == 0xFF && (b[6] & 0xF8) == 0xD0)
               || (b[5] == 0xFF && (b[6] & 0xF8) == 0x10 && (b[6] & 7) != 4 && (b[6] & 7) != 5)
               || (b[4] == 0xFF && b[5] == 0x14)
               || (b[4] == 0xFF && (b[5] & 0xF8) == 0x50 && (b[5] & 7) != 4)
               || (b[3] == 0xFF && b[4] == 0x54)
               || (b[1] == 0xFF && (b[2] & 0xF8) == 0x90 && (b[2] & 7) != 4)
               || (b[0] == 0xFF && b[1] == 0x94);
    }

    private bool Retry(List<UnwoundFrame> frames, ref RegisterSet registers, RegisterSet beforeScan, ref int scans)
    {
        if (frames.Count == 0 || !frames[frames.Count - 1].Scanned || beforeScan == null || scans >= MaxScans)
        {
            return false;
        }

        var doubtful = frames[frames.Count - 1];
        var retried = beforeScan.Copy();
        scans++;
        if (!Scan(retried, doubtful.StackPointer))
        {
            return false;
        }

        frames.RemoveAt(frames.Count - 1);
        registers = retried;
        return true;
    }

    private bool IsCode(ulong address)
    {
        var module = _modules.Find(address);
        if (module == null)
        {
            return _memory.IsExecutable(address);
        }

        var image = _names.Image(module.Path);
        return image == null || image.IsExecutable((uint)(address - module.Start));
    }

    private StepResult Step(RegisterSet registers, bool top)
    {
        var module = _modules.Find(registers.Rip);
        var image = module == null ? null : _names.Image(module.Path);
        if (image == null || !image.IsAmd64)
        {
            return StepResult.NoUnwindData;
        }

        var rva = (uint)(registers.Rip - module.Start);
        if (!image.TryFindFunction(top ? rva : rva - 1, out var function))
        {
            return Pop(registers) ? StepResult.Unwound : StepResult.Failed;
        }

        return Unwind(image, rva, function, registers, top);
    }

    private StepResult Unwind(PeImage image, uint rva, RuntimeFunction function, RegisterSet registers, bool top)
    {
        var info = UnwindInfo.Read(image, function.UnwindInfo);
        if (info == null)
        {
            return StepResult.Failed;
        }

        var prologOffset = rva >= function.Begin && rva - function.Begin < info.PrologSize ? rva - function.Begin : PastProlog;
        if (top && prologOffset == PastProlog && TryEpilog(image, rva, function, info.FrameRegister, registers))
        {
            return StepResult.Unwound;
        }

        var frame = EstablisherFrame(info, prologOffset, registers);
        var machineFrame = false;
        var current = info;
        var primary = true;
        for (var chain = 0; chain < MaxChain; chain++)
        {
            var codes = current.Codes;
            var index = 0;
            while (index < current.CodeCount)
            {
                var offset = codes[index * 2];
                var operation = codes[index * 2 + 1] & 0x0F;
                var operationInfo = codes[index * 2 + 1] >> 4;
                if (operation > LastKnownOperation)
                {
                    return StepResult.Failed;
                }

                var slots = Slots(operation, operationInfo);
                if (index + slots > current.CodeCount)
                {
                    return StepResult.Failed;
                }

                if (primary && prologOffset != PastProlog && offset > prologOffset)
                {
                    index += slots;
                    continue;
                }

                switch (operation)
                {
                    case PushNonvolatile:
                        if (!ReadWord(registers.Rsp, out var pushed))
                        {
                            return StepResult.Failed;
                        }

                        registers.Restore(operationInfo, pushed);
                        registers.Rsp += 8;
                        break;
                    case AllocLarge:
                        registers.Rsp += operationInfo == 0 ? Slot16(codes, index + 1) * 8UL : Slot32(codes, index + 1);
                        break;
                    case AllocSmall:
                        registers.Rsp += (ulong)(operationInfo + 1) * 8;
                        break;
                    case SetFramePointer:
                    case SetFramePointerLarge:
                        if (registers.IsStale(current.FrameRegister))
                        {
                            _doubtful = true;
                        }

                        var frameOffset = operation == SetFramePointer ? (ulong)current.FrameOffset : Slot32(codes, index + 1);
                        registers.Rsp = registers.Integer[current.FrameRegister] - frameOffset * 16UL;
                        break;
                    case SaveNonvolatile:
                    case SaveNonvolatileFar:
                        var at = frame + (operation == SaveNonvolatile ? Slot16(codes, index + 1) * 8UL : Slot32(codes, index + 1));
                        if (!ReadWord(at, out var saved))
                        {
                            return StepResult.Failed;
                        }

                        registers.Restore(operationInfo, saved);
                        break;
                    case PushMachineFrame:
                        if (operationInfo != 0)
                        {
                            registers.Rsp += 8;
                        }

                        if (!ReadWord(registers.Rsp, out var rip) || !ReadWord(registers.Rsp + 24, out var rsp))
                        {
                            return StepResult.Failed;
                        }

                        registers.Rip = rip;
                        registers.Rsp = rsp;
                        machineFrame = true;
                        break;
                }

                index += slots;
            }

            if (!current.Chained)
            {
                break;
            }

            var parent = image.ReadChained(current.Address, current.CodeCount);
            current = parent.UnwindInfo == 0 ? null : UnwindInfo.Read(image, parent.UnwindInfo);
            if (current == null)
            {
                return StepResult.Failed;
            }

            primary = false;
        }

        if (machineFrame)
        {
            return StepResult.MachineFrame;
        }

        return Pop(registers) ? StepResult.Unwound : StepResult.Failed;
    }

    private ulong EstablisherFrame(UnwindInfo info, uint prologOffset, RegisterSet registers)
    {
        if (info.FrameRegister == 0)
        {
            return registers.Rsp;
        }

        var established = prologOffset == PastProlog || info.Chained;
        if (!established)
        {
            var index = 0;
            while (index < info.CodeCount)
            {
                var operation = info.Codes[index * 2 + 1] & 0x0F;
                if (operation == SetFramePointer)
                {
                    established = prologOffset >= info.Codes[index * 2];
                    break;
                }

                if (operation > LastKnownOperation)
                {
                    break;
                }

                index += Slots(operation, info.Codes[index * 2 + 1] >> 4);
            }
        }

        if (!established)
        {
            return registers.Rsp;
        }

        if (registers.IsStale(info.FrameRegister))
        {
            _doubtful = true;
        }

        return registers.Integer[info.FrameRegister] - (ulong)info.FrameOffset * 16UL;
    }

    private bool TryEpilog(PeImage image, uint rva, RuntimeFunction function, int frameRegister, RegisterSet registers)
    {
        var code = image.Read(rva, 32);
        if (code == null)
        {
            return false;
        }

        var at = 0;
        var adjust = 0L;
        var useFrame = -1;
        if (code[0] == 0x48 && code[1] == 0x83 && code[2] == 0xC4)
        {
            adjust = (sbyte)code[3];
            at = 4;
        }
        else if (code[0] == 0x48 && code[1] == 0x81 && code[2] == 0xC4)
        {
            adjust = BitConverter.ToInt32(code, 3);
            at = 7;
        }
        else if ((code[0] & 0xFE) == 0x48 && code[1] == 0x8D && (code[2] & 0x38) == 0x20 && (code[2] & 0xC0) != 0xC0)
        {
            var mode = code[2] >> 6;
            var baseRegister = (code[2] & 7) + ((code[0] & 1) != 0 ? 8 : 0);
            if ((code[2] & 7) == 4 || mode == 0 || frameRegister == 0 || baseRegister != frameRegister)
            {
                return false;
            }

            adjust = mode == 1 ? (sbyte)code[3] : BitConverter.ToInt32(code, 3);
            useFrame = baseRegister;
            at = mode == 1 ? 4 : 7;
        }

        var pops = new List<int>();
        while (at < code.Length - 7)
        {
            var rex = 0;
            if ((code[at] & 0xF0) == 0x40)
            {
                rex = code[at];
                at++;
            }

            var opcode = code[at];
            if ((rex & 0x08) == 0 && opcode >= 0x58 && opcode <= 0x5F)
            {
                pops.Add((opcode - 0x58) + ((rex & 1) != 0 ? 8 : 0));
                at++;
                continue;
            }

            if (opcode == 0xF2 && rex == 0)
            {
                at++;
                opcode = code[at];
            }

            bool returns;
            switch (opcode)
            {
                case 0xC3:
                case 0xC2:
                    returns = true;
                    break;
                case 0xF3:
                    returns = code[at + 1] == 0xC3;
                    break;
                case 0xE9:
                    returns = LeavesFunction((long)rva + at + 5 + BitConverter.ToInt32(code, at + 1), function);
                    break;
                case 0xEB:
                    returns = LeavesFunction((long)rva + at + 2 + (sbyte)code[at + 1], function);
                    break;
                case 0xFF:
                    returns = (rex == 0 && code[at + 1] == 0x25) || ((rex & 0x08) != 0 && (code[at + 1] >> 3 & 7) == 4);
                    break;
                default:
                    returns = false;
                    break;
            }

            if (!returns)
            {
                return false;
            }

            var rsp = useFrame >= 0 ? registers.Integer[useFrame] + (ulong)adjust : registers.Rsp + (ulong)adjust;
            var values = new ulong[pops.Count];
            for (var index = 0; index < pops.Count; index++)
            {
                if (!ReadWord(rsp, out values[index]))
                {
                    return false;
                }

                rsp += 8;
            }

            if (!ReadWord(rsp, out var rip))
            {
                return false;
            }

            for (var index = 0; index < pops.Count; index++)
            {
                registers.Restore(pops[index], values[index]);
            }

            registers.Rip = rip;
            registers.Rsp = rsp + 8;
            return true;
        }

        return false;
    }

    private static bool LeavesFunction(long target, RuntimeFunction function) => target < function.Begin || target >= function.End || target == function.Begin;

    private bool Scan(RegisterSet registers, ulong from)
    {
        var buffer = new byte[ScanPage];
        var position = from & ~7UL;
        var end = position + ScanBytes;
        while (position < end)
        {
            var length = ReadSome(position, buffer);
            if (length == 0)
            {
                return false;
            }

            for (var offset = 0; offset + 8 <= length; offset += 8)
            {
                var value = BitConverter.ToUInt64(buffer, offset);
                if (LooksLikeReturnAddress(value))
                {
                    registers.Rip = value;
                    registers.Rsp = position + (ulong)offset + 8;
                    registers.Stale = RegisterSet.AllStale;
                    return true;
                }
            }

            position += (ulong)length;
        }

        return false;
    }

    private int ReadSome(ulong address, byte[] buffer)
    {
        var untilPage = ScanPage - (int)(address & (ScanPage - 1));
        for (var length = untilPage; length >= 8; length /= 2)
        {
            if (_memory.TryRead(address, buffer, length & ~7))
            {
                return length & ~7;
            }
        }

        return 0;
    }

    private bool LooksLikeReturnAddress(ulong value)
    {
        var module = _modules.Find(value);
        if (module == null || value - module.Start < 8)
        {
            return false;
        }

        var image = _names.Image(module.Path);
        var rva = (uint)(value - module.Start);
        if (image == null || !image.IsExecutable(rva - 1))
        {
            return false;
        }

        var before = image.Read(rva - 7, 7);
        return before != null && FollowsCall(before);
    }

    private bool Pop(RegisterSet registers)
    {
        if (!ReadWord(registers.Rsp, out var rip))
        {
            return false;
        }

        registers.Rip = rip;
        registers.Rsp += 8;
        return true;
    }

    private bool ReadWord(ulong address, out ulong value)
    {
        value = 0;
        if (!_memory.TryRead(address, _word, 8))
        {
            return false;
        }

        value = BitConverter.ToUInt64(_word, 0);
        return true;
    }

    private static int Slots(int operation, int operationInfo)
    {
        var slots = ExtraSlots[Math.Min(operation, LastKnownOperation)] + 1;
        if (operation == AllocLarge && operationInfo != 0)
        {
            slots++;
        }

        return slots;
    }

    private static ulong Slot16(byte[] codes, int index) => BitConverter.ToUInt16(codes, index * 2);

    private static ulong Slot32(byte[] codes, int index) => BitConverter.ToUInt32(codes, index * 2);

    private enum StepResult
    {
        Unwound,
        MachineFrame,
        NoUnwindData,
        Failed
    }
}
