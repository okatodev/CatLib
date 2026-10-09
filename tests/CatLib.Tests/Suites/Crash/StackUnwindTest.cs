using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using CatLib.CrashWatcher.Stacks;
using CatLib.CrashWatcher.Symbols;
using CatLib.Diagnostics;
using CatLib.Tests.Framework;

namespace CatLib.Tests.Suites.Crash;

public sealed class StackUnwindTest : TestCase
{
    public const ulong ImageBase = 0x7FF600000000;
    public const ulong JitCode = 0x50000000;

    public override string Suite => "Crash";

    public override IEnumerable<TestStep> Run(TestContext context)
    {
        var root = Path.Combine(Path.GetTempPath(), "CatLibStackUnwind_" + Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(root);
            var gameAssembly = Path.Combine(root, CodeNames.GameAssemblyName);
            File.WriteAllBytes(gameAssembly, SyntheticImage.Build());
            CheckImage(gameAssembly);
            CheckUnwinding(gameAssembly, root);
            CheckNames(gameAssembly, root);
            CheckStaleMap(gameAssembly, root);
            Assert.True(StackUnwinder.FollowsCall(new byte[] { 0, 0, 0xE8, 1, 2, 3, 4 }), "A relative call comes before a return address");
            Assert.True(StackUnwinder.FollowsCall(new byte[] { 0, 0xFF, 0x15, 1, 2, 3, 4 }), "A call through the import table too");
            Assert.True(StackUnwinder.FollowsCall(new byte[] { 0, 0, 0, 0, 0x41, 0xFF, 0xD2 }), "And a call through a register");
            Assert.False(StackUnwinder.FollowsCall(new byte[7]), "Zero bytes are not a call");
            Assert.Equal("il2cpp_baselib::Baselib_SystemFutex_Wait", CodeNames.Readable("?Baselib_SystemFutex_Wait@il2cpp_baselib@@YAXPEAHHI@Z"), "C++ export names are made readable");
            Assert.Equal("??0Thread@@QEAA@XZ", CodeNames.Readable("??0Thread@@QEAA@XZ"), "Special C++ names stay as they are");
            Assert.Equal("RaiseException", CodeNames.Readable("RaiseException"), "Plain names stay as they are");
        }
        finally
        {
            try
            {
                Directory.Delete(root, true);
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }

        yield break;
    }

    private static void CheckImage(string path)
    {
        using (var image = PeImage.Open(path))
        {
            Assert.NotNull(image, "The synthetic GameAssembly.dll opens");
            Assert.True(image.IsAmd64, "It is an x64 image");
            Assert.True(image.TryFindFunction(0x1010, out var first) && first.Begin == 0x1000, "An address inside the first function finds it");
            Assert.False(image.TryFindFunction(0x10C5, out _), "A leaf function has no unwind data");
            Assert.True(image.TryFindFunction(0x10A5, out var part) && part.Begin == 0x10A0, "The second part of a split function has its own entry");
            Assert.Equal(0x1080u, image.PrimaryStart(part), "The chain leads to the start of the whole function");
            Assert.Equal(".text", image.SectionName(0x1010), "Section of the code");
            Assert.True(image.IsExecutable(0x1010), "Code is executable");
            Assert.False(image.IsExecutable(0x2010), "Unwind data is not");
            Assert.True(image.HasFunctionStartBetween(0x1000, 0x1040), "A function starts after the first one");
            Assert.False(image.HasFunctionStartBetween(0x10C0, 0x10C8), "Nothing starts inside the leaf function");
        }
    }

    private static void CheckUnwinding(string gameAssembly, string root)
    {
        var modules = new ModuleSet(new[] { new CrashModule(gameAssembly, ImageBase, 0x3000) });
        using (var names = new CodeNames(Path.Combine(root, "no-interop"), null))
        {
            var stack = new FakeMemory(0x10000, 0x400);
            var s0 = 0x10000UL;
            var s1 = s0 + 8;
            var s2 = s1 + 0x130;
            var s3 = s2 + 0x50;
            stack.Set(s0, ImageBase + 0x1010);
            stack.Set(s1 + 0x20, 0xB0B);
            stack.Set(s1 + 0x28, ImageBase + 0x1050);
            stack.Set(s2 + 0x30, 0x5151);
            stack.Set(s2 + 0x40, 0xBB);
            stack.Set(s2 + 0x48, ImageBase + 0x10A8);
            stack.Set(s3 + 0x10, 0);
            var registers = new RegisterSet { Rip = ImageBase + 0x10C5, Rsp = s0 };
            registers.Integer[RegisterSet.FramePointer] = s2 + 0x20;
            var unwinder = new StackUnwinder(stack, modules, names);
            var frames = unwinder.Walk(registers);
            Assert.SequenceEqual(new[] { ImageBase + 0x10C5, ImageBase + 0x1010, ImageBase + 0x1050, ImageBase + 0x10A8 }, Addresses(frames),
                "A leaf, a function with pushes, a function with a frame pointer and alloca, and a split function unwind in order");
            Assert.Equal(s2 + 0x50, frames[3].StackPointer, "The frame pointer restores the stack after alloca");
            Assert.Equal("end of the stack", unwinder.LastStop, "The walk ends at a zero return address");
            Assert.False(frames.Exists(frame => frame.Scanned), "Nothing was guessed");

            var scan = new FakeMemory(0x20000, 0x400);
            scan.Set(0x20000, 0xDEAD);
            scan.Set(0x20008, ImageBase + 0x1020);
            scan.Set(0x20010, ImageBase + 0x1010);
            scan.Executable = JitCode;
            unwinder = new StackUnwinder(scan, modules, names);
            frames = unwinder.Walk(new RegisterSet { Rip = JitCode, Rsp = 0x20000 });
            Assert.SequenceEqual(new[] { JitCode, ImageBase + 0x1010 }, Addresses(frames), "Out of JIT code the walk finds the return address after a call");
            Assert.True(frames[1].Scanned, "That frame is marked as found by a search");
            Assert.Equal(0x20018UL, frames[1].StackPointer, "The search skips a code address that does not follow a call");

            var epilog = new FakeMemory(0x30000, 0x400);
            epilog.Set(0x30000, 0xB0B);
            epilog.Set(0x30008, ImageBase + 0x1010);
            unwinder = new StackUnwinder(epilog, modules, names);
            frames = unwinder.Walk(new RegisterSet { Rip = ImageBase + 0x1034, Rsp = 0x30000 });
            Assert.SequenceEqual(new[] { ImageBase + 0x1034, ImageBase + 0x1010 }, Addresses(frames), "A crash in the middle of an epilog unwinds what is left of it");
            Assert.Equal(0x30010UL, frames[1].StackPointer, "The stack is not freed twice");

            var prolog = new FakeMemory(0x40000, 0x400);
            prolog.Set(0x40000, 0xB0B);
            prolog.Set(0x40008, ImageBase + 0x10C5);
            unwinder = new StackUnwinder(prolog, modules, names);
            frames = unwinder.Walk(new RegisterSet { Rip = ImageBase + 0x1001, Rsp = 0x40000 });
            Assert.SequenceEqual(new[] { ImageBase + 0x1001, ImageBase + 0x10C5 }, Addresses(frames), "A crash in the middle of a prolog undoes only what ran");
            Assert.Equal(0x40010UL, frames[1].StackPointer, "The stack was not allocated yet");

            var bad = new FakeMemory(0x50000, 0x400);
            bad.Set(0x50000, 0x1234);
            unwinder = new StackUnwinder(bad, modules, names);
            frames = unwinder.Walk(new RegisterSet { Rip = ImageBase + 0x10C5, Rsp = 0x50000 });
            Assert.Equal(1, frames.Count, "A return address into data ends the walk");
            Assert.Equal("the next address is not code", unwinder.LastStop, "And says why");

            var data = new FakeMemory(0x58000, 0x400);
            data.Set(0x58000, ImageBase + 0x2010);
            unwinder = new StackUnwinder(data, modules, names);
            frames = unwinder.Walk(new RegisterSet { Rip = ImageBase + 0x10C5, Rsp = 0x58000 });
            Assert.Equal(1, frames.Count, "A return address into the unwind data of a module is not code either");

            var nullCall = new FakeMemory(0x70000, 0x400);
            nullCall.Set(0x70000, ImageBase + 0x1010);
            unwinder = new StackUnwinder(nullCall, modules, names);
            frames = unwinder.Walk(new RegisterSet { Rip = 0, Rsp = 0x70000 });
            Assert.SequenceEqual(new[] { 0UL, ImageBase + 0x1010 }, Addresses(frames), "A call through a null pointer still shows who called it");
            Assert.True(frames[0].Interrupted && !frames[1].Interrupted, "Only the first frame was interrupted, the rest are return addresses");
            Assert.True(frames[0].NotCode && !frames[1].NotCode, "The bad address is marked as not code, not as JIT code");

            var stale = new FakeMemory(0x60000, 0x400);
            stale.Set(0x60000, ImageBase + 0x1050);
            stale.Set(0x60148, ImageBase + 0x10C5);
            stale.Executable = JitCode;
            var staleStart = new RegisterSet { Rip = JitCode, Rsp = 0x60000 };
            staleStart.Integer[RegisterSet.FramePointer] = 0x60120;
            unwinder = new StackUnwinder(stale, modules, names);
            frames = unwinder.Walk(staleStart);
            Assert.SequenceEqual(new[] { JitCode, ImageBase + 0x1050, ImageBase + 0x10C5 }, Addresses(frames), "A frame pointer function found by a search still unwinds");
            Assert.True(frames[1].Scanned && frames[2].Scanned, "Its caller is doubtful too: the frame pointer after a search may be stale");
        }
    }

    private static void CheckNames(string gameAssembly, string root)
    {
        var interop = Path.Combine(root, "interop");
        Directory.CreateDirectory(interop);
        var assembly = typeof(StackUnwindTest).Assembly;
        File.Copy(assembly.Location, Path.Combine(interop, Path.GetFileName(assembly.Location)));
        var sample = typeof(StackUnwindTest).GetMethod(nameof(Sample), System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static).MetadataToken;
        var put = typeof(Holder<>).GetMethod(nameof(Holder<int>.Put)).MetadataToken;
        var generic = typeof(StackUnwindTest).GetMethod(nameof(Generic), System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static).MetadataToken;
        WriteMap(interop, assembly.FullName, new[] { 0x1000L, 0x1000L, 0x1040L, 0x10C0L }, new[] { sample, put, put, generic });

        using (var names = new CodeNames(interop, null))
        {
            var prefix = typeof(StackUnwindTest).FullName + ".";
            Assert.Equal(prefix + "Sample(int, string[], ref List<int>) + 0x10, 1 more method has the same code", names.Name(gameAssembly, 0x1010),
                "A game method with its parameters, the offset, and methods folded into the same code");
            Assert.Equal(prefix + "Holder<T>.Put(T, Dictionary<string, T[]>) + 0x5", names.Name(gameAssembly, 0x1045, false, out var isGame), "A method of a nested generic type");
            Assert.True(isGame, "A name from the method map is a game method");
            names.Name(gameAssembly, 0x10A5, false, out isGame);
            Assert.False(isGame, "The IL2CPP runtime is not");
            Assert.Equal(prefix + "Holder<T>.Put(T, Dictionary<string, T[]>) + 0x0", names.Name(gameAssembly, 0x1040), "An interrupted instruction at a function start belongs to that function");
            Assert.True(names.Name(gameAssembly, 0x1040, true).StartsWith(prefix + "Sample(int, string[], ref List<int>) + 0x40"), "A return address right after the last call belongs to the caller");
            Assert.Equal(prefix + "Generic<TItem>(TItem, TItem?) + 0x5", names.Name(gameAssembly, 0x10C5), "A leaf method without unwind data");
            Assert.Equal("IL2CPP runtime, in the function at 0x1080", names.Name(gameAssembly, 0x10A5), "Code that is no game method is named by its section and function");
            Assert.True(names.HasGameMethods, "The method map was used");
            Assert.Null(names.Name(Path.Combine(root, "missing.dll"), 0x10), "A missing module has no names");
        }
    }

    private static void CheckStaleMap(string gameAssembly, string root)
    {
        var interop = Path.Combine(root, "stale");
        Directory.CreateDirectory(interop);
        var sample = typeof(StackUnwindTest).GetMethod(nameof(Sample), System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static).MetadataToken;
        WriteMap(interop, typeof(StackUnwindTest).Assembly.FullName, new[] { 0x1010L, 0x1050L }, new[] { sample, sample });
        using (var names = new CodeNames(interop, null))
        {
            Assert.Equal("IL2CPP runtime, in the function at 0x1000", names.Name(gameAssembly, 0x1010), "A map of another game build is not used");
            Assert.False(names.HasGameMethods, "No game names from a wrong map");
        }
    }

    private static List<ulong> Addresses(List<UnwoundFrame> frames) => frames.ConvertAll(frame => frame.Address);

    private static void WriteMap(string directory, string assemblyName, long[] rvas, int[] tokens)
    {
        using (var stream = new MemoryStream())
        using (var writer = new BinaryWriter(stream, Encoding.UTF8))
        {
            writer.Write(GameMethodMap.Magic);
            writer.Write(GameMethodMap.SupportedVersion);
            writer.Write(1);
            writer.Write(rvas.Length);
            writer.Write(0);
            writer.Write(assemblyName);
            var dataOffset = (int)stream.Position;
            foreach (var rva in rvas)
            {
                writer.Write(rva);
            }

            foreach (var token in tokens)
            {
                writer.Write(token);
                writer.Write(0);
            }

            writer.Flush();
            var bytes = stream.ToArray();
            BitConverter.GetBytes(dataOffset).CopyTo(bytes, 16);
            File.WriteAllBytes(Path.Combine(directory, GameMethodMap.FileName), bytes);
        }
    }

    private static int Sample(int count, string[] names, ref List<int> values) => count + names.Length + values.Count;

    private static void Generic<TItem>(TItem item, TItem? maybe)
        where TItem : struct
    {
    }

    private sealed class Holder<T>
    {
        public void Put(T item, Dictionary<string, T[]> map)
        {
        }
    }

    private sealed class FakeMemory : IProcessMemory
    {
        private readonly ulong _start;
        private readonly byte[] _bytes;

        public FakeMemory(ulong start, int size)
        {
            _start = start;
            _bytes = new byte[size];
        }

        public ulong Executable { get; set; }

        public void Set(ulong address, ulong value) => BitConverter.GetBytes(value).CopyTo(_bytes, (int)(address - _start));

        public bool TryRead(ulong address, byte[] buffer, int count)
        {
            if (address < _start || address + (ulong)count > _start + (ulong)_bytes.Length)
            {
                return false;
            }

            Array.Copy(_bytes, (int)(address - _start), buffer, 0, count);
            return true;
        }

        public bool IsExecutable(ulong address) => Executable != 0 && address >= Executable && address < Executable + 0x1000;
    }
}
