using Fourgate.Core.Rendering;
using Microsoft.Xna.Framework;

namespace Fourgate.Tests;

/// <summary>Remembers the particles drawn, instead of drawing.</summary>
public sealed class RecordingRenderer : IRenderer
{
    public List<(Vector2 Ground, float BaseHeight, float Lift)> Particles { get; } = new();

    public void BeginWorld(Camera2D camera)
    {
    }

    public void BeginScreen()
    {
    }

    public void End()
    {
    }

    public void DrawVisual(in VisualDescriptor visual, Vector2 position, float rotation)
    {
    }

    public void FillRect(Vector2 topLeft, Vector2 size, Color color)
    {
    }

    public void FillCircleRaised(Vector2 position, float height, float radius, Color color)
    {
    }

    public void DrawLineRaised(Vector2 from, Vector2 to, float height, float thickness, Color color)
    {
    }

    public void DrawParticle(Vector2 ground, float baseHeight, float lift, float radius, Color color)
    {
        Particles.Add((ground, baseHeight, lift));
    }

    public bool IsThreeD => false;

    public void DrawVisualElevated(in VisualDescriptor visual, Vector2 position, float rotation, float elevation)
    {
    }

    public void DrawGlowWall(Vector2 from, Vector2 to, float height, Color color)
    {
    }

    public void DrawShadow(Vector2 position, float height, float radius)
    {
    }

    public void DrawBar(Vector2 from, Vector2 to, float thickness, float bottom, float top, Color color)
    {
    }

    public void FillRectRaised(Vector2 topLeft, Vector2 size, float height, Color color)
    {
    }

    public void DrawVisualRaised(in VisualDescriptor visual, Vector2 position, float rotation, float height)
    {
    }

    public void FillBox(Vector2 topLeft, Vector2 size, float height, Color color)
    {
    }

    public void FillCircle(Vector2 center, float radius, Color color)
    {
    }

    public void DrawLine(Vector2 from, Vector2 to, float thickness, Color color)
    {
    }

    public void DrawTextCentered(string text, Vector2 center, float size, Color color)
    {
    }

    public void DrawText(string text, Vector2 position, float size, Color color, TextAlign align = TextAlign.Left)
    {
    }
}
