using System.Collections.Generic;
using CatLib.Tests.Framework;

namespace CatLib.Tests.Suites.Ui;

public sealed class ModsListTest : TestCase
{
    public override string Suite => "Ui";

    public override int Order => 5;

    public override IEnumerable<TestStep> Run(TestContext context)
    {
        foreach (var step in SettingsMenuFixture.EnsureModsTab(context))
        {
            yield return step;
        }

        var controller = SettingsMenuFixture.Tab.Controller;
        var sandbox = new UiRowsSandbox("UiList");
        try
        {
            yield return Wait.Until(() => SettingsMenuFixture.HasItem(controller, sandbox.Settings), 3, "the new mod to appear in the list");
            context.Note($"Mods listed with the sandbox: {controller.Items.Count}");
        }
        finally
        {
            sandbox.Dispose();
        }

        yield return Wait.Until(() => !SettingsMenuFixture.HasItem(controller, sandbox.Settings), 3, "the disposed mod to leave the list");
        Assert.True(controller.Selected != null, "A mod must stay selected after the selected one disappears");
    }
}
