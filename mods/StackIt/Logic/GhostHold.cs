namespace StackIt.Logic;

public static class GhostHold
{
    public const float MostSeconds = 2f;

    public static bool IsOver(bool gone, bool hostMoved, float heldFor) => gone || hostMoved || heldFor >= MostSeconds;
}
