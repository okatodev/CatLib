namespace StackIt.Logic;

public static class LossRules
{
    public static LossAction Decide(bool keepBalanced, bool mainLost, bool hangingDue, bool isHost)
    {
        if (keepBalanced || (!mainLost && !hangingDue))
        {
            return LossAction.Keep;
        }

        return isHost ? LossAction.Drop : LossAction.Wait;
    }

    public static bool IsDue(bool lost, float since, float levelStart, float now, float grace = BridgeRules.GraceSeconds) =>
        lost || now - System.Math.Max(since, levelStart) > grace;

    public static bool Clips(float placedBottom, float placedTop, float hangingBottom, float hangingTop, float tolerance = BridgeRules.LevelTolerance) =>
        placedTop > hangingBottom + tolerance && placedBottom < hangingTop - tolerance;
}
