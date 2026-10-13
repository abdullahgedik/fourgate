using Microsoft.Xna.Framework;

namespace Fourgate.Core.Rendering;

/// <summary>
/// Drawing surface. Gameplay and UI draw through this so the shape-based
/// implementation can later be replaced by sprites.
/// </summary>
public interface IRenderer
{
    /// <summary>Starts a world-space pass seen through <paramref name="camera"/>, clipped to its view.</summary>
    void BeginWorld(Camera2D camera);

    /// <summary>Starts a screen-space pass: coordinates are virtual-screen pixels (HUD, menus).</summary>
    void BeginScreen();

    void End();

    /// <summary>Draws an entity's visual at a position; <paramref name="rotation"/> is in radians.</summary>
    void DrawVisual(in VisualDescriptor visual, Vector2 position, float rotation);

    void FillRect(Vector2 topLeft, Vector2 size, Color color);

    void FillCircle(Vector2 center, float radius, Color color);

    void DrawLine(Vector2 from, Vector2 to, float thickness, Color color);

    /// <summary>Draws one line of text; <paramref name="position"/> is its top edge at the aligned side.</summary>
    /// <param name="text">Text to draw.</param>
    /// <param name="position">Top-left, top-center or top-right of the text, depending on <paramref name="align"/>.</param>
    /// <param name="size">Font size in pixels.</param>
    /// <param name="color">Text color.</param>
    /// <param name="align">Horizontal alignment relative to <paramref name="position"/>.</param>
    void DrawText(string text, Vector2 position, float size, Color color, TextAlign align = TextAlign.Left);
}
