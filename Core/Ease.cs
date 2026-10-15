using System;

namespace Fourgate.Core;

/// <summary>Easing curves for UI animation: each maps 0–1 (clamped) to 0–1.</summary>
public static class Ease
{
    /// <summary>Fast at first, slowing to a stop.</summary>
    public static float OutCubic(float t)
    {
        t = Math.Clamp(t, 0f, 1f);
        var inverse = 1f - t;
        return 1f - inverse * inverse * inverse;
    }

    /// <summary>Slow at first, speeding up.</summary>
    public static float InCubic(float t)
    {
        t = Math.Clamp(t, 0f, 1f);
        return t * t * t;
    }

    /// <summary>Overshoots a little past 1, then settles back: a springy arrival.</summary>
    public static float OutBack(float t)
    {
        t = Math.Clamp(t, 0f, 1f);
        const float Overshoot = 1.70158f;
        var u = t - 1f;
        return 1f + (Overshoot + 1f) * u * u * u + Overshoot * u * u;
    }
}
