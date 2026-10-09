using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Threading;

namespace CatLib.CrashWatcher.Symbols;

internal sealed class SymbolStore
{
    public const string UnityServer = "http://symbolserver.unity3d.com/";
    public const string MicrosoftServer = "https://msdl.microsoft.com/download/symbols/";
    public const string MissingMarker = "missing.txt";
    public const long MaxDownloadBytes = 1024L * 1024 * 1024;
    public const int KeptVersions = 2;
    public static readonly TimeSpan RetryMissingAfter = TimeSpan.FromDays(7);
    public static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(30);
    public static readonly TimeSpan ExpandTimeout = TimeSpan.FromMinutes(3);
    public static readonly string[] UnityPrefixes = { "UnityPlayer_", "WindowsPlayer_", "baselib_" };
    public static readonly string[] MicrosoftNames =
    {
        "ntdll.pdb", "kernelbase.pdb", "kernel32.pdb", "ucrtbase.pdb", "vcruntime140.amd64.pdb", "vcruntime140_1.amd64.pdb", "msvcp140.amd64.pdb",
        "user32.pdb", "win32u.pdb", "d3d11.pdb", "dxgi.pdb", "coreclr.pdb", "clrjit.pdb"
    };

    private readonly string _root;
    private readonly WatcherLog _log;
    private readonly object _gate = new object();
    private readonly ManualResetEvent _idle = new ManualResetEvent(true);
    private int _pending;

    public SymbolStore(string root, WatcherLog log)
    {
        _root = root;
        _log = log;
    }

    public string Root => _root;

    public static string ServerFor(string pdbName)
    {
        foreach (var prefix in UnityPrefixes)
        {
            if (pdbName.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                return UnityServer;
            }
        }

        foreach (var name in MicrosoftNames)
        {
            if (string.Equals(pdbName, name, StringComparison.OrdinalIgnoreCase))
            {
                return MicrosoftServer;
            }
        }

        return null;
    }

    public string CachedPath(string pdbName, string key)
    {
        if (string.IsNullOrEmpty(_root) || string.IsNullOrEmpty(pdbName) || string.IsNullOrEmpty(key))
        {
            return null;
        }

        var path = Path.Combine(_root, pdbName, key, pdbName);
        return File.Exists(path) ? path : null;
    }

    public bool WaitIdle(TimeSpan timeout) => _idle.WaitOne(timeout);

    public void Prefetch(IEnumerable<KeyValuePair<string, string>> pdbs)
    {
        var list = new List<KeyValuePair<string, string>>();
        foreach (var pdb in pdbs)
        {
            if (ServerFor(pdb.Key) != null && CachedPath(pdb.Key, pdb.Value) == null && !RecentlyMissing(pdb.Key, pdb.Value))
            {
                list.Add(pdb);
            }
        }

        if (list.Count == 0)
        {
            return;
        }

        lock (_gate)
        {
            _pending++;
            _idle.Reset();
        }

        var thread = new Thread(() => Download(list)) { IsBackground = true, Name = "CatLib symbols", Priority = ThreadPriority.BelowNormal };
        thread.Start();
    }

    private void Download(List<KeyValuePair<string, string>> pdbs)
    {
        try
        {
            ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;
            foreach (var pdb in pdbs)
            {
                try
                {
                    DownloadOne(pdb.Key, pdb.Value);
                }
                catch (Exception exception)
                {
                    _log.Write($"Downloading symbols {pdb.Key} failed: {exception.Message}");
                }
            }
        }
        finally
        {
            lock (_gate)
            {
                _pending--;
                if (_pending == 0)
                {
                    _idle.Set();
                }
            }
        }
    }

    private void DownloadOne(string pdbName, string key)
    {
        var server = ServerFor(pdbName);
        var folder = Path.Combine(_root, pdbName, key);
        Directory.CreateDirectory(folder);
        var target = Path.Combine(folder, pdbName);
        var clock = Stopwatch.StartNew();
        var plain = server + pdbName + "/" + key + "/" + pdbName;
        if (TryFetch(plain, target + ".part", out var status))
        {
            File.Move(target + ".part", target);
            _log.Write($"Symbols {pdbName} downloaded from {server} in {clock.Elapsed.TotalSeconds:0.0} s, {new FileInfo(target).Length / 1024} KiB");
            Prune(pdbName, key);
            return;
        }

        var compressed = server + pdbName + "/" + key + "/" + pdbName.Substring(0, pdbName.Length - 1) + "_";
        var cab = target + ".cab";
        if (TryFetch(compressed, cab, out var compressedStatus))
        {
            if (Expand(cab, target))
            {
                _log.Write($"Symbols {pdbName} downloaded compressed from {server} in {clock.Elapsed.TotalSeconds:0.0} s, {new FileInfo(target).Length / 1024} KiB");
                Prune(pdbName, key);
            }

            TryDelete(cab);
            return;
        }

        File.WriteAllText(Path.Combine(folder, MissingMarker), DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture));
        _log.Write($"Symbols {pdbName} {key} are not on {server} ({status}, {compressedStatus}), the next try is in {RetryMissingAfter.TotalDays} days");
    }

    private void Prune(string pdbName, string keep)
    {
        try
        {
            var folders = new List<DirectoryInfo>(new DirectoryInfo(Path.Combine(_root, pdbName)).GetDirectories());
            folders.Sort((left, right) => right.LastWriteTimeUtc.CompareTo(left.LastWriteTimeUtc));
            var kept = 0;
            foreach (var folder in folders)
            {
                if (string.Equals(folder.Name, keep, StringComparison.OrdinalIgnoreCase) || (File.Exists(Path.Combine(folder.FullName, pdbName)) && ++kept < KeptVersions))
                {
                    continue;
                }

                if (!File.Exists(Path.Combine(folder.FullName, MissingMarker)))
                {
                    folder.Delete(true);
                    _log.Write($"Removed symbols {pdbName} of an older version ({folder.Name})");
                }
            }
        }
        catch (Exception exception)
        {
            _log.Write($"Could not remove older symbols of {pdbName}: {exception.Message}");
        }
    }

    private bool RecentlyMissing(string pdbName, string key)
    {
        var marker = Path.Combine(_root, pdbName, key, MissingMarker);
        try
        {
            return File.Exists(marker) && DateTime.UtcNow - File.GetLastWriteTimeUtc(marker) < RetryMissingAfter;
        }
        catch (Exception)
        {
            return false;
        }
    }

    private bool TryFetch(string url, string path, out string status)
    {
        status = string.Empty;
        TryDelete(path);
        try
        {
            using (var client = new HttpClient { Timeout = RequestTimeout })
            using (var request = new HttpRequestMessage(HttpMethod.Get, url))
            {
                request.Headers.UserAgent.ParseAdd("Microsoft-Symbol-Server/10.0.0.0");
                using (var response = client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead).GetAwaiter().GetResult())
                {
                    if (response.StatusCode != HttpStatusCode.OK || response.Content.Headers.ContentLength > MaxDownloadBytes)
                    {
                        status = ((int)response.StatusCode).ToString(CultureInfo.InvariantCulture);
                        return false;
                    }

                    using (var input = response.Content.ReadAsStreamAsync().GetAwaiter().GetResult())
                    using (var output = new FileStream(path, FileMode.Create, FileAccess.Write))
                    {
                        var buffer = new byte[81920];
                        long total = 0;
                        int read;
                        while ((read = input.Read(buffer, 0, buffer.Length)) > 0)
                        {
                            total += read;
                            if (total > MaxDownloadBytes)
                            {
                                throw new IOException("the file is larger than " + MaxDownloadBytes / (1024 * 1024) + " MB");
                            }

                            output.Write(buffer, 0, read);
                        }
                    }
                }
            }

            return true;
        }
        catch (Exception exception)
        {
            status = exception.GetBaseException().Message;
            TryDelete(path);
            return false;
        }
    }

    private bool Expand(string cab, string target)
    {
        var expand = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "expand.exe");
        if (!File.Exists(expand))
        {
            _log.Write($"Compressed symbols need {expand}, which is missing");
            return false;
        }

        var start = new ProcessStartInfo(expand, "\"" + cab + "\" \"" + target + "\"")
        {
            UseShellExecute = false,
            CreateNoWindow = true
        };
        using (var process = Process.Start(start))
        {
            if (process == null || !process.WaitForExit((int)ExpandTimeout.TotalMilliseconds))
            {
                _log.Write("Expanding compressed symbols took too long");
                return false;
            }
        }

        if (File.Exists(target))
        {
            return true;
        }

        _log.Write($"Expanding {cab} gave no {Path.GetFileName(target)}");
        return false;
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch (Exception)
        {
        }
    }
}
