using System.Collections.Generic;
using CatLib.Tests.Framework;
using StackIt.Logic;

namespace CatLib.Tests.Suites.StackIt;

public sealed class LossRulesTest : TestCase
{
    public override string Suite => "StackIt";

    public override IEnumerable<TestStep> Run(TestContext context)
    {
        Assert.Equal(LossAction.Keep, LossRules.Decide(false, false, false, true), "Nothing lost, nothing happens");
        Assert.Equal(LossAction.Drop, LossRules.Decide(false, false, true, true), "By default the host lets a parcel fall when a cell under it hangs in the air");
        Assert.Equal(LossAction.Drop, LossRules.Decide(false, true, false, true), "and when the parcel under its centre is taken");
        Assert.Equal(LossAction.Wait, LossRules.Decide(false, false, true, false), "A client waits for the host to let it fall");
        Assert.Equal(LossAction.Keep, LossRules.Decide(true, true, true, true), "Keeping balance, a parcel never falls on its own");

        Assert.True(LossRules.IsDue(true, 10f, 0f, 10f), "A cell that lost its parcel is due at once");
        Assert.False(LossRules.IsDue(false, 10f, 12f, 15f), "A cell without a parcel after loading waits a few seconds from the level start");
        Assert.True(LossRules.IsDue(false, 10f, 12f, 18f), "and is due after them");

        Assert.True(LossRules.Clips(0f, 0.5f, 0.4f, 0.9f), "A parcel reaching into a hanging part clips");
        Assert.False(LossRules.Clips(0f, 0.41f, 0.4f, 0.9f), "A parcel whose top is level with the hanging part's bottom fits under it");
        Assert.False(LossRules.Clips(0f, 0.25f, 0.4f, 0.9f), "A lower parcel fits under it");
        Assert.False(LossRules.Clips(0.9f, 1.4f, 0.4f, 0.9f), "A parcel above it does not clip");
        yield break;
    }
}
