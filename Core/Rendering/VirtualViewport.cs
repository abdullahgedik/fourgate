using System;
using Microsoft.Xna.Framework;

namespace Fourgate.Core.Rendering;

/// <summary>
/// Where the virtual screen lands inside the window (aspect-preserving, centered) and
/// the conversions between window pixels and virtual coordinates.
/// </summary>
public readonly struct VirtualViewport
{
    private VirtualViewport(int virtualWidth, int virtualHeight, Rectangle destination)
    {
        VirtualWidth = virtualWidth;
        VirtualHeight = virtualHeight;
        Destination = destination;
    }

    public int VirtualWidth { get; }

    public int VirtualHeight { get; }

    /// <summary>Window-space rectangle the virtual screen is drawn into; the rest is black bars.</summary>
    public Rectangle Destination { get; }

    /// <summary>Window pixels per virtual unit.</summary>
    public float Scale => VirtualWidth > 0 ? Destination.Width / (float)VirtualWidth : 0f;

    /// <summary>Fits the virtual screen into a window of the given size.</summary>
    public static VirtualViewport Fit(int virtualWidth, int virtualHeight, int windowWidth, int windowHeight)
    {
        if (virtualWidth <= 0 || virtualHeight <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(virtualWidth), "Virtual size must be positive.");
        }

        // A minimized window reports a zero-sized client area.
        windowWidth = Math.Max(windowWidth, 0);
        windowHeight = Math.Max(windowHeight, 0);

        var scale = Math.Min(windowWidth / (float)virtualWidth, windowHeight / (float)virtualHeight);
        var width = (int)MathF.Round(virtualWidth * scale);
        var height = (int)MathF.Round(virtualHeight * scale);
        var destination = new Rectangle((windowWidth - width) / 2, (windowHeight - height) / 2, width, height);

        return new VirtualViewport(virtualWidth, virtualHeight, destination);
    }

    /// <summary>Converts a window-space position (e.g. the mouse) to virtual coordinates.</summary>
    public Vector2 WindowToVirtual(Vector2 window)
    {
        if (Destination.Width <= 0 || Destination.Height <= 0)
        {
            return Vector2.Zero;
        }

        return new Vector2(
            (window.X - Destination.X) * VirtualWidth / Destination.Width,
            (window.Y - Destination.Y) * VirtualHeight / Destination.Height);
    }

    /// <summary>Converts a virtual position to window space.</summary>
    public Vector2 VirtualToWindow(Vector2 virtualPosition)
    {
        return new Vector2(
            Destination.X + virtualPosition.X * Destination.Width / VirtualWidth,
            Destination.Y + virtualPosition.Y * Destination.Height / VirtualHeight);
    }
}
