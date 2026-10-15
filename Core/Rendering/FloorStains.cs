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

    public void Add(Vector2 position, float radius, Color color)
    {
        TotalAdded++;
        if (PendingCount < MaxPending)
        {
            _pending[PendingCount++] = new Stain(position, radius, color);
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

    /// <summary>One mark: a soft round blot.</summary>
    public readonly record struct Stain(Vector2 Position, float Radius, Color Color);
}
