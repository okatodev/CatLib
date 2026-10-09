using System;
using System.Globalization;
using System.IO;
using System.Text;

namespace CatLib.CrashWatcher;

internal sealed class WatcherLog
{
    public const long MaxBytes = 256 * 1024;

    private static readonly object Gate = new object();

    private readonly string _path;

    public WatcherLog(string path)
    {
        _path = path;
    }

    public void Write(string message)
    {
        try
        {
            lock (Gate)
            {
                if (File.Exists(_path) && new FileInfo(_path).Length > MaxBytes)
                {
                    File.Delete(_path);
                }

                File.AppendAllText(_path, DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture) + " " + message + Environment.NewLine, Encoding.UTF8);
            }
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
}
