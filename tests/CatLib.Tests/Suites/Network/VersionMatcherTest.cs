using System.Collections.Generic;
using CatLib.Net;
using CatLib.Tests.Framework;

namespace CatLib.Tests.Suites.Network;

public sealed class VersionMatcherTest : TestCase
{
    public override string Suite => "Network";

    public override IEnumerable<TestStep> Run(TestContext context)
    {
        var cases = new (string Host, string Client, VersionRule Rule, bool Expected)[]
        {
            ("1.2.3", "1.2.3", VersionRule.Exact, true),
            ("1.2.3", "1.2.4", VersionRule.Exact, false),
            ("1.2.3", "1.2.9", VersionRule.SameMinor, true),
            ("1.2.3", "1.3.0", VersionRule.SameMinor, false),
            ("1.2.3", "1.9.0", VersionRule.SameMajor, true),
            ("1.2.3", "2.0.0", VersionRule.SameMajor, false),
            ("1.2.3", "9.9.9", VersionRule.Any, true),
            ("1.2", "1.2.7", VersionRule.SameMinor, true),
            ("v1.2.0", "1.2.5", VersionRule.SameMinor, true),
            ("1.2.0-beta.1", "1.2.0", VersionRule.SameMinor, true),
            ("1.2.0-beta.1", "1.2.0", VersionRule.Exact, false),
            ("nightly", "nightly", VersionRule.SameMinor, true),
            ("nightly", "nightly-2", VersionRule.SameMajor, false),
            ("", "1.0.0", VersionRule.SameMajor, false)
        };

        foreach (var (host, client, rule, expected) in cases)
        {
            Assert.Equal(expected, VersionMatcher.Matches(host, client, rule), $"{rule} host \"{host}\" client \"{client}\"");
        }

        yield break;
    }
}
