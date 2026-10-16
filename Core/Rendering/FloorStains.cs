using System;
using Microsoft.Xna.Framework;

namespace Fourgate.Core.Rendering;

/// <summary>
/// Blood and other marks that stay on the floor for the whole run. Gameplay only adds stains
/// here; the renderer paints the new ones into a floor image once and then forgets them, so
/// any number of stains costs the same to draw.
/// </summary>
public sealed class FloorStains
{
    // Stains waiting to be painted; more in one frame than this are dropped.
    private const int MaxPending = 4096;

    private readonly Stain[] _pending = new Stain[MaxPending];

    /// <param name="topLeft">World position of the floor's top-left corner.</param>
    /// <param name="size">Width and height of the (square) floor.</param>
    public FloorStains(Vector2 topLeft, float size)
    {
        TopLeft = topLeft;
        Size = size;
    }

    public Vector2 TopLeft { get; }

    public float Size { get; }

    /// <summary>Stains added since they were last painted.</summary>
    public int PendingCount { get; private set; }

    /// <summary>Every stain ever added (for tests and debugging).</summary>
    public int TotalAdded { get; private set; }

    /// <summary>A soft round blot (blood, soot, oil).</summary>
    public void Add(Vector2 position, float radius, Color color)
    {
        Push(new Stain(position, radius, color));
    }

    /// <summary>A hard-edged rectangle turned by <paramref name="rotation"/> (floor slabs, painted marks).</summary>
    public void AddRect(Vector2 center, Vector2 size, float rotation, Color color)
    {
        Push(new Stain(center, 0f, color, StainShape.Rect, size, rotation));
    }

    /// <summary>A straight line (seams, cracks).</summary>
    public void AddLine(Vector2 from, Vector2 to, float thickness, Color color)
    {
        var along = to - from;
        AddRect((from + to) / 2f, new Vector2(along.Length(), thickness), MathF.Atan2(along.Y, along.X), color);
    }

    private void Push(in Stain stain)
    {
        TotalAdded++;
        if (PendingCount < MaxPending)
        {
            _pending[PendingCount++] = stain;
        }
    }

    public Stain GetPending(int index)
    {
        return _pending[index];
    }

    /// <summary>Called once the pending stains are painted.</summary>
    public void ClearPending()
    {
        PendingCount = 0;
    }

    /// <summary>One mark: a soft round blot, or a hard-edged turned rectangle.</summary>
    public readonly record struct Stain(
        Vector2 Position, float Radius, Color Color, StainShape Shape = StainShape.Blot, Vector2 Size = default,
        float Rotation = 0f);

    /// <summary>The shape of a floor mark.</summary>
    public enum StainShape
    {
        Blot,
        Rect,
    }
}
