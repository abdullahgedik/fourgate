using System;
using Microsoft.Xna.Framework;

namespace Fourgate.Core.Rendering;

/// <summary>
/// Colours that add light instead of covering: with zero alpha, the premultiplied blending the
/// renderers use adds the colour on top of what is already there (fire, glows, light pools).
/// </summary>
public static class Light
{
    /// <summary>The colour as added light at a strength (0–1).</summary>
    public static Color Of(Color color, float strength)
    {
        var s = Math.Clamp(strength, 0f, 1f);
        return new Color((byte)(color.R * s), (byte)(color.G * s), (byte)(color.B * s), (byte)0);
    }
}
