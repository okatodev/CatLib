using System.Collections.Generic;
using CatLib.Tests.Framework;

namespace CatLib.Tests.Suites.Ui;

public sealed class SettingsMenuCleanupTest : TestCase
{
    public override string Suite => "Ui";

    public override int Order => 99;

    public override IEnumerable<TestStep> Run(TestContext context)
    {
        SettingsMenuFixture.RestoreTab();
        SettingsMenuFixture.CloseIfOpened(context);
        yield break;
    }
}
