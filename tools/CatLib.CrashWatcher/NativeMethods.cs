using System;
using System.Runtime.InteropServices;
using System.Text;

namespace CatLib.CrashWatcher;

internal static class NativeMethods
{
    public const uint Synchronize = 0x00100000;
    public const uint ProcessQueryLimitedInformation = 0x1000;
    public const uint Infinite = 0xFFFFFFFF;
    public const uint UnicodeText = 13;
    public const uint MovableMemory = 0x0002;
    public const uint TopMost = 0x0008;
    public const uint NoSize = 0x0001;
    public const uint NoMove = 0x0002;
    public const uint IconError = 0x10;
    public const int TaskDialogCreated = 0;
    public const int TaskDialogButtonClicked = 2;
    public const int SetElementText = 0x0400 + 108;
    public const int FooterElement = 2;
    public const int AllowCancellation = 0x0008;
    public const int UseMainIconHandle = 0x0002;
    public const int CloseButton = 0x0020;
    public const int CloseResult = 8;
    public const int KeepOpen = 1;
    public const int Ok = 0;
    public const uint ProcessQueryInformation = 0x0400;
    public const uint ProcessVmRead = 0x0010;
    public const uint ProcessDuplicateHandle = 0x0040;
    public const uint ThreadGetContext = 0x0008;
    public const uint ThreadQueryInformation = 0x0040;
    public const uint ThreadSuspendResume = 0x0002;
    public const uint ThreadQueryLimitedInformation = 0x0800;
    public const uint SnapThreads = 0x00000004;
    public const int ThreadEntrySize = 28;
    public const int ContextControlInteger = 0x100003;
    public const int ContextIntegerOffset = 0x78;
    public const int ContextRipOffset = 0xF8;
    public const int MemoryInfoSize = 48;
    public const uint MemoryCommit = 0x1000;
    public const uint ExecutableProtection = 0xF0;
    public const uint SuspendFailed = 0xFFFFFFFF;
    public const uint DebugContinue = 0x00010002;
    public const uint DebugExceptionNotHandled = 0x80010001;
    public const int ExceptionDebugEvent = 1;
    public const int CreateProcessDebugEvent = 3;
    public const int ExitProcessDebugEvent = 5;
    public const int LoadDllDebugEvent = 6;
    public const int DebugEventSize = 176;
    public const int ExceptionRecordSize = 152;
    public const int ContextSize = 1232;
    public const int ContextFlagsOffset = 0x30;
    public const int ContextAll = 0x10001F;
    public const uint ListModulesAll = 3;
    public static readonly IntPtr WarningIcon = new IntPtr(0xFFFF);
    public static readonly IntPtr TopMostWindow = new IntPtr(-1);

    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern IntPtr OpenProcess(uint access, bool inherit, int processId);

    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern uint WaitForSingleObject(IntPtr handle, uint milliseconds);

    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern bool GetExitCodeProcess(IntPtr process, out uint exitCode);

    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern bool GetProcessTimes(IntPtr process, out long creation, out long exit, out long kernel, out long user);

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    public static extern bool QueryFullProcessImageName(IntPtr process, uint flags, StringBuilder name, ref uint size);

    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern bool CloseHandle(IntPtr handle);

    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern bool DebugActiveProcess(int processId);

    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern bool DebugActiveProcessStop(int processId);

    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern bool DebugSetProcessKillOnExit(bool killOnExit);

    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern bool WaitForDebugEvent(IntPtr debugEvent, uint milliseconds);

    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern bool ContinueDebugEvent(int processId, int threadId, uint continueStatus);

    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern IntPtr OpenThread(uint access, bool inherit, int threadId);

    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern bool GetThreadContext(IntPtr thread, IntPtr context);

    [DllImport("kernel32.dll", SetLastError = true, EntryPoint = "K32EnumProcessModulesEx")]
    public static extern bool EnumProcessModulesEx(IntPtr process, [Out] IntPtr[] modules, uint size, out uint needed, uint filter);

    [DllImport("kernel32.dll", SetLastError = true, EntryPoint = "K32GetModuleInformation")]
    public static extern bool GetModuleInformation(IntPtr process, IntPtr module, out ModuleInfo info, uint size);

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode, EntryPoint = "K32GetModuleFileNameExW")]
    public static extern uint GetModuleFileNameEx(IntPtr process, IntPtr module, StringBuilder name, uint size);

    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern IntPtr CreateToolhelp32Snapshot(uint flags, int processId);

    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern bool Thread32First(IntPtr snapshot, IntPtr entry);

    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern bool Thread32Next(IntPtr snapshot, IntPtr entry);

    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern uint SuspendThread(IntPtr thread);

    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern uint ResumeThread(IntPtr thread);

    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern bool ReadProcessMemory(IntPtr process, IntPtr address, byte[] buffer, IntPtr size, out IntPtr read);

    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern IntPtr VirtualQueryEx(IntPtr process, IntPtr address, IntPtr buffer, IntPtr length);

    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern int GetThreadDescription(IntPtr thread, out IntPtr description);

    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern IntPtr LocalFree(IntPtr memory);

    [DllImport("dbghelp.dll", SetLastError = true)]
    public static extern bool MiniDumpWriteDump(IntPtr process, int processId, IntPtr file, uint type, IntPtr exceptionParam, IntPtr userStreamParam, IntPtr callbackParam);

    [StructLayout(LayoutKind.Sequential)]
    public struct ModuleInfo
    {
        public IntPtr BaseOfDll;
        public uint SizeOfImage;
        public IntPtr EntryPoint;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern IntPtr GlobalAlloc(uint flags, UIntPtr bytes);

    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern IntPtr GlobalLock(IntPtr memory);

    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern bool GlobalUnlock(IntPtr memory);

    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern IntPtr GlobalFree(IntPtr memory);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern bool OpenClipboard(IntPtr owner);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern bool EmptyClipboard();

    [DllImport("user32.dll", SetLastError = true)]
    public static extern IntPtr SetClipboardData(uint format, IntPtr memory);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern bool CloseClipboard();

    [DllImport("user32.dll", SetLastError = true)]
    public static extern bool SetWindowPos(IntPtr window, IntPtr after, int x, int y, int width, int height, uint flags);

    [DllImport("user32.dll")]
    public static extern bool SetForegroundWindow(IntPtr window);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    public static extern IntPtr SendMessage(IntPtr window, int message, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    public static extern int MessageBox(IntPtr owner, string text, string caption, uint type);

    [DllImport("user32.dll")]
    public static extern bool DestroyIcon(IntPtr icon);

    [DllImport("user32.dll")]
    public static extern bool SetProcessDPIAware();

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    public static extern uint ExtractIconEx(string file, int index, IntPtr[] large, IntPtr[] small, uint count);

    [DllImport("comctl32.dll", CharSet = CharSet.Unicode, PreserveSig = true)]
    public static extern int TaskDialogIndirect(ref TaskDialogConfig config, out int button, out int radioButton, out bool verification);

    public delegate int TaskDialogCallback(IntPtr window, int notification, IntPtr wParam, IntPtr lParam, IntPtr data);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode, Pack = 1)]
    public struct TaskDialogConfig
    {
        public uint Size;
        public IntPtr Parent;
        public IntPtr Instance;
        public int Flags;
        public int CommonButtons;
        [MarshalAs(UnmanagedType.LPWStr)] public string WindowTitle;
        public IntPtr MainIcon;
        [MarshalAs(UnmanagedType.LPWStr)] public string MainInstruction;
        [MarshalAs(UnmanagedType.LPWStr)] public string Content;
        public uint ButtonCount;
        public IntPtr Buttons;
        public int DefaultButton;
        public uint RadioButtonCount;
        public IntPtr RadioButtons;
        public int DefaultRadioButton;
        [MarshalAs(UnmanagedType.LPWStr)] public string VerificationText;
        [MarshalAs(UnmanagedType.LPWStr)] public string ExpandedInformation;
        [MarshalAs(UnmanagedType.LPWStr)] public string ExpandedControlText;
        [MarshalAs(UnmanagedType.LPWStr)] public string CollapsedControlText;
        public IntPtr FooterIcon;
        [MarshalAs(UnmanagedType.LPWStr)] public string Footer;
        [MarshalAs(UnmanagedType.FunctionPtr)] public TaskDialogCallback Callback;
        public IntPtr CallbackData;
        public uint Width;
    }
}
