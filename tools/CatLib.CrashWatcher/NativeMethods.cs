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
