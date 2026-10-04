using System.Numerics;

namespace StackIt.Logic;

public readonly record struct SlidePlan(Vector2 Direction, float Distance)
{
    public bool HasDirection => Direction.LengthSquared() > 0f;

    public bool Slides => HasDirection && Distance > 0f;
}
