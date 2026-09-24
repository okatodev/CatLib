using System.Collections.Generic;
using System.Linq;
using BepInEx.Configuration;
using CatLib.Tests.Framework;
using CatLib.UI;

namespace CatLib.Tests.Suites.Presentation;

public sealed class PresentationKindTest : TestCase
{
    public override string Suite => "Presentation";

    public override IEnumerable<TestStep> Run(TestContext context)
    {
        Assert.Equal(ControlKind.Toggle, SettingPresentation.For(typeof(bool), null).Kind, "bool");
        Assert.Equal(ControlKind.Text, SettingPresentation.For(typeof(string), null).Kind, "string");
        Assert.Equal(ControlKind.Text, SettingPresentation.For(typeof(int), null).Kind, "int without a range");
        Assert.Equal(ControlKind.Text, SettingPresentation.For(typeof(SampleFlags), null).Kind, "flags enum");

        var intRange = SettingPresentation.For(typeof(int), new AcceptableValueRange<int>(0, 10));
        Assert.Equal(ControlKind.Slider, intRange.Kind, "int with a range");
        Assert.True(intRange.WholeNumbers, "int slider uses whole numbers");
        Assert.Equal(0f, intRange.Min, "int slider min");
        Assert.Equal(10f, intRange.Max, "int slider max");
        Assert.Equal((object)7, intRange.FromSlider(6.6f), "int slider rounds to the nearest whole value");
        Assert.Equal("7", intRange.FormatSlider(6.6f), "int slider text");

        var floatRange = SettingPresentation.For(typeof(float), new AcceptableValueRange<float>(0f, 3f));
        Assert.Equal(ControlKind.Slider, floatRange.Kind, "float with a range");
        Assert.False(floatRange.WholeNumbers, "float slider uses fractions");
        Assert.Equal((object)2.35f, floatRange.FromSlider(2.3456f), "float slider rounds to two digits");
        Assert.Equal("2.35", floatRange.FormatSlider(2.3456f), "float slider text");
        Assert.Equal("1.5", floatRange.FormatSlider(1.5f), "float slider text without trailing zeros");

        var enumeration = SettingPresentation.For(typeof(SampleMode), null);
        Assert.Equal(ControlKind.Dropdown, enumeration.Kind, "enum");
        Assert.SequenceEqual(new[] { "Fast Mode", "Slow Mode", "HTTP Mode" }, enumeration.ChoiceLabels, "enum labels");
        Assert.Equal(1, enumeration.IndexOf(SampleMode.SlowMode), "enum index");

        var listed = SettingPresentation.For(typeof(string), new AcceptableValueList<string>("Red", "Green", "Blue"));
        Assert.Equal(ControlKind.Dropdown, listed.Kind, "acceptable value list");
        Assert.SequenceEqual(new[] { "Red", "Green", "Blue" }, listed.ChoiceLabels, "listed labels");
        Assert.Equal(2, listed.IndexOf("Blue"), "listed index");
        Assert.Equal(-1, listed.IndexOf("Purple"), "missing listed value");

        var listedNumbers = SettingPresentation.For(typeof(int), new AcceptableValueList<int>(1, 2, 4, 8));
        Assert.Equal(ControlKind.Dropdown, listedNumbers.Kind, "listed numbers take a dropdown over a text field");
        Assert.SequenceEqual(new object[] { 1, 2, 4, 8 }, listedNumbers.Choices.ToArray(), "listed number choices");
        yield break;
    }
}
