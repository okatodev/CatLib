using System.Collections.Generic;
using CatLib.Tests.Framework;
using CatLib.UI;

namespace CatLib.Tests.Suites.Ui;

public sealed class ControlsRegisteredTest : TestCase
{
    public override string Suite => "Ui";

    public override int Order => 9;

    public override IEnumerable<TestStep> Run(TestContext context)
    {
        using var sandbox = new UiRowsSandbox("UiControls");
        foreach (var step in SettingsMenuFixture.SelectMod(context, sandbox.Settings))
        {
            yield return step;
        }

        var options = SettingsMenuFixture.Tab.Options;
        var name = SettingsMenuFixture.Row<TextRow>("Name").Input;
        var color = SettingsMenuFixture.Row<DropdownRow>("Color").Dropdown;

        var inputs = options._inputFields;
        var dropdowns = options._dropdowns;
        var nameRegistered = false;
        var colorRegistered = false;
        var dead = 0;

        for (var index = 0; index < inputs.Length; index++)
        {
            nameRegistered |= inputs[index] != null && inputs[index].Pointer == name.Pointer;
            dead += UiClone.IsAlive(inputs[index]) ? 0 : 1;
        }

        for (var index = 0; index < dropdowns.Length; index++)
        {
            colorRegistered |= dropdowns[index] != null && dropdowns[index].Pointer == color.Pointer;
            dead += UiClone.IsAlive(dropdowns[index]) ? 0 : 1;
        }

        context.Note($"Registered input fields: {inputs.Length}, dropdowns: {dropdowns.Length}");

        Assert.True(nameRegistered, "Mod input fields must be registered in OptionsInterface._inputFields");
        Assert.True(colorRegistered, "Mod dropdowns must be registered in OptionsInterface._dropdowns");
        Assert.Equal(0, dead, "Destroyed controls left in the OptionsInterface arrays");
    }
}
