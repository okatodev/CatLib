using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using CatLib.Diagnostics;

namespace CatLib.CrashWatcher;

internal sealed class CrashWindow
{
    public const int OpenFolderId = 100;
    public const int CopyId = 101;

    private readonly CrashReport _report;
    private readonly bool _russian;
    private readonly string _gamePath;
    private readonly WatcherLog _log;

    public CrashWindow(CrashReport report, bool russian, string gamePath, WatcherLog log)
    {
        _report = report;
        _russian = russian;
        _gamePath = gamePath;
        _log = log;
    }

    public void Show(string phrase)
    {
        var content = _report.Summary + "\n\n" + CrashText.HelpLine(_russian);
        try
        {
            ShowTaskDialog(phrase, content);
        }
        catch (Exception exception)
        {
            _log.Write($"The task dialog is unavailable, using a message box: {exception.Message}");
            NativeMethods.MessageBox(IntPtr.Zero, phrase + "\n\n" + content + "\n\n" + _report.Details + "\n\n" + CrashText.Footer(_russian, _report.Folder),
                CrashText.WindowTitle(_russian), NativeMethods.IconError | NativeMethods.TopMost);
        }
    }

    private void ShowTaskDialog(string phrase, string content)
    {
        var buttons = new[]
        {
            (OpenFolderId, CrashText.OpenFolderButton(_russian)),
            (CopyId, CrashText.CopyButton(_russian))
        };
        var buttonSize = 4 + IntPtr.Size;
        var buttonMemory = Marshal.AllocHGlobal(buttonSize * buttons.Length);
        var texts = new IntPtr[buttons.Length];
        var icon = GameIcon();
        NativeMethods.TaskDialogCallback callback = OnNotification;
        try
        {
            for (var index = 0; index < buttons.Length; index++)
            {
                texts[index] = Marshal.StringToHGlobalUni(buttons[index].Item2);
                Marshal.WriteInt32(buttonMemory, index * buttonSize, buttons[index].Item1);
                Marshal.WriteIntPtr(buttonMemory, index * buttonSize + 4, texts[index]);
            }

            var config = new NativeMethods.TaskDialogConfig
            {
                Flags = NativeMethods.AllowCancellation | (icon == IntPtr.Zero ? 0 : NativeMethods.UseMainIconHandle),
                CommonButtons = NativeMethods.CloseButton,
                WindowTitle = CrashText.WindowTitle(_russian),
                MainIcon = icon == IntPtr.Zero ? NativeMethods.WarningIcon : icon,
                MainInstruction = phrase,
                Content = content,
                ButtonCount = (uint)buttons.Length,
                Buttons = buttonMemory,
                DefaultButton = NativeMethods.CloseResult,
                ExpandedInformation = _report.Details,
                ExpandedControlText = CrashText.HideDetails(_russian),
                CollapsedControlText = CrashText.ShowDetails(_russian),
                Footer = CrashText.Footer(_russian, _report.Folder),
                Callback = callback
            };
            config.Size = (uint)Marshal.SizeOf(typeof(NativeMethods.TaskDialogConfig));

            var result = NativeMethods.TaskDialogIndirect(ref config, out _, out _, out _);
            if (result < 0)
            {
                Marshal.ThrowExceptionForHR(result);
            }
        }
        finally
        {
            GC.KeepAlive(callback);
            foreach (var text in texts)
            {
                if (text != IntPtr.Zero)
                {
                    Marshal.FreeHGlobal(text);
                }
            }

            Marshal.FreeHGlobal(buttonMemory);
            if (icon != IntPtr.Zero)
            {
                NativeMethods.DestroyIcon(icon);
            }
        }
    }

    private int OnNotification(IntPtr window, int notification, IntPtr wParam, IntPtr lParam, IntPtr data)
    {
        try
        {
            if (notification == NativeMethods.TaskDialogCreated)
            {
                NativeMethods.SetWindowPos(window, NativeMethods.TopMostWindow, 0, 0, 0, 0, NativeMethods.NoMove | NativeMethods.NoSize);
                NativeMethods.SetForegroundWindow(window);
                return NativeMethods.Ok;
            }

            if (notification != NativeMethods.TaskDialogButtonClicked)
            {
                return NativeMethods.Ok;
            }

            switch (wParam.ToInt32())
            {
                case OpenFolderId:
                    OpenFolder();
                    return NativeMethods.KeepOpen;
                case CopyId:
                    if (ClipboardText.Set(window, _report.Text))
                    {
                        SetFooter(window, CrashText.Copied(_russian) + ". " + CrashText.Footer(_russian, _report.Folder));
                    }

                    return NativeMethods.KeepOpen;
                default:
                    return NativeMethods.Ok;
            }
        }
        catch (Exception exception)
        {
            _log.Write($"The crash window action failed: {exception.Message}");
            return NativeMethods.KeepOpen;
        }
    }

    private void OpenFolder()
    {
        Process.Start(new ProcessStartInfo("explorer.exe", "\"" + _report.Folder + "\"") { UseShellExecute = false });
    }

    private static void SetFooter(IntPtr window, string text)
    {
        var pointer = Marshal.StringToHGlobalUni(text);
        try
        {
            NativeMethods.SendMessage(window, NativeMethods.SetElementText, new IntPtr(NativeMethods.FooterElement), pointer);
        }
        finally
        {
            Marshal.FreeHGlobal(pointer);
        }
    }

    private IntPtr GameIcon()
    {
        if (string.IsNullOrEmpty(_gamePath))
        {
            return IntPtr.Zero;
        }

        var large = new IntPtr[1];
        var small = new IntPtr[1];
        try
        {
            if (NativeMethods.ExtractIconEx(_gamePath, 0, large, small, 1) == 0)
            {
                return IntPtr.Zero;
            }
        }
        catch (Exception)
        {
            return IntPtr.Zero;
        }

        if (small[0] != IntPtr.Zero)
        {
            NativeMethods.DestroyIcon(small[0]);
        }

        return large[0];
    }
}
