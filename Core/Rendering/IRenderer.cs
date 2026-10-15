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

    /// <summary>
    /// Something flying off the floor (a fireball, a thrown grenade): drawn at its position in 2D,
    /// lifted to <paramref name="height"/> and facing the camera in 3D.
    /// </summary>
    void FillCircleRaised(Vector2 position, float height, float radius, Color color);

    /// <summary>A streak flying off the floor (bullets, beams): flat in 2D, lifted to <paramref name="height"/> in 3D.</summary>
    void DrawLineRaised(Vector2 from, Vector2 to, float height, float thickness, Color color);

    /// <summary>
    /// A particle over the floor point <paramref name="ground"/>, born at <paramref name="baseHeight"/>
    /// and risen <paramref name="lift"/> since: in 2D only the rise shows (drawn that far up the screen),
    /// in 3D it floats at both together, facing the camera.
    /// </summary>
    void DrawParticle(Vector2 ground, float baseHeight, float lift, float radius, Color color);

    /// <summary>A block standing on the floor (walls, pillars); flat like a rectangle in 2D.</summary>
    void FillBox(Vector2 topLeft, Vector2 size, float height, Color color);

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
