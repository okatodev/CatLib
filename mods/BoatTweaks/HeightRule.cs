using System;

namespace BoatTweaks;

public static class HeightRule
{
    public const float MinimumScale = 0.5f;
    public const float MaximumScale = 4f;

    public static (float Approved, float Maximum) Apply(float approved, float maximum, float approvedScale, float maximumScale)
    {
        var scaledMaximum = maximum > 0f ? maximum * Clamp(maximumScale) : maximum;
        if (approved <= 0f)
        {
            return (approved, scaledMaximum);
        }

        var scaledApproved = approved * Clamp(approvedScale);
        if (scaledMaximum > 0f)
        {
            scaledApproved = Math.Min(scaledApproved, scaledMaximum);
        }

        return (scaledApproved, scaledMaximum);
    }

    private static float Clamp(float scale) => Math.Min(Math.Max(scale, MinimumScale), MaximumScale);
}
