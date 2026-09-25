using System.Collections.Generic;
using CatLib.Tests.Diagnostics.Inspection;
using CatLib.Tests.Framework;

namespace CatLib.Tests.Suites.Formatting;

public sealed class InspectionTextTest : TestCase
{
    public override string Suite => "Formatting";

    public override IEnumerable<TestStep> Run(TestContext context)
    {
        Assert.Equal("Height", InspectionText.MemberName("_Height_k__BackingField"), "Auto property backing field");
        Assert.Equal("_currentRepairIndex", InspectionText.MemberName("_currentRepairIndex"), "Private field keeps its name");
        Assert.Equal("PossibleSprites", InspectionText.MemberName("PossibleSprites"), "Public field keeps its name");
        Assert.Equal("", InspectionText.MemberName(""), "Empty name");

        Assert.Equal("1.5", InspectionText.Scalar(1.5f), "Float with invariant culture");
        Assert.Equal("0.333", InspectionText.Scalar(1.0 / 3), "Double rounded to three digits");
        Assert.Equal("42", InspectionText.Scalar(42), "Integer");
        Assert.Equal("true", InspectionText.Scalar(true), "Boolean");
        Assert.Equal("Boat", InspectionText.Scalar(SampleStorage.Boat), "Enum name");
        Assert.Equal("\"a\\nb\"", InspectionText.Scalar("a\nb"), "Line breaks are escaped");
        Assert.Equal("null", InspectionText.Scalar(null), "Null");
        Assert.True(InspectionText.Clip(new string('x', 500)).EndsWith("...(500 chars)"), "Long text is clipped with its length");
        Assert.True(InspectionText.IsScalar(typeof(SampleStorage)), "Enums are scalars");
        Assert.False(InspectionText.IsScalar(typeof(List<int>)), "Collections are not scalars");
        yield break;
    }

    public enum SampleStorage
    {
        Other,
        Boat
    }
}
