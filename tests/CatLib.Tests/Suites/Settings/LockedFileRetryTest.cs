using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using CatLib.Tests.Framework;

namespace CatLib.Tests.Suites.Settings;

public sealed class LockedFileRetryTest : TestCase
{
    private const int LockMilliseconds = 1200;

    public override string Suite => "Settings";

    public override IEnumerable<TestStep> Run(TestContext context)
    {
        using var sandbox = new ConfigSandbox("LockedFile");
        var setting = sandbox.Settings.Local("General", "Speed", 1, "Speed.");
        var received = new List<int>();
        setting.Apply(received.Add);

        var bytes = Encoding.UTF8.GetBytes(sandbox.ContentWith("General", "Speed", "2"));
        var locker = new FileStream(sandbox.FilePath, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
        locker.SetLength(0);
        locker.Write(bytes, 0, bytes.Length);
        locker.Flush(true);
        Task.Delay(LockMilliseconds).ContinueWith(_ => locker.Dispose());

        yield return Wait.Until(() => received.Count >= 2, 8, "the value written under an exclusive lock to be applied");

        var report = sandbox.Reports[sandbox.Reports.Count - 1];
        context.Note($"Read attempts: {report.Attempts}");
        Assert.SequenceEqual(new[] { 1, 2 }, received, "Applied values");
        Assert.AtLeast(2, report.Attempts, "Read attempts while the file was locked");
    }
}
