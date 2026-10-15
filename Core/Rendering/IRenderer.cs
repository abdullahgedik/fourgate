using Fourgate.Core.Rendering.Models;
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

    /// <summary>
    /// A solid bar along a line between two heights (a shield band, a gun barrel, a shockwave
    /// ring piece): a line in 2D, a block from <paramref name="bottom"/> to <paramref name="top"/> in 3D.
    /// </summary>
    void DrawBar(Vector2 from, Vector2 to, float thickness, float bottom, float top, Color color);

    /// <summary>
    /// A glowing wall standing on a floor line, fading out towards its top (the rising edges of an
    /// attack warning): nothing in 2D, where the floor marking alone shows the warning.
    /// </summary>
    void DrawGlowWall(Vector2 from, Vector2 to, float height, Color color);

    /// <summary>
    /// A body in the air (a leaping boss): in 2D drawn larger the higher it is, over a shadow; in
    /// 3D lifted to <paramref name="elevation"/> with its shadow on the floor.
    /// </summary>
    void DrawVisualElevated(in VisualDescriptor visual, Vector2 position, float rotation, float elevation);

    /// <summary>
    /// Lays the floor-stain image (blood that stays for the run) over the floor; drawn right
    /// after the floor so everything else goes on top.
    /// </summary>
    void DrawFloorStains(Vector2 topLeft, Vector2 size);

    /// <summary>
    /// A low body lying on the floor (a mine): in 3D a short solid of the given height with no
    /// shadow; in 2D the plain shape.
    /// </summary>
    void DrawLowBody(in VisualDescriptor visual, Vector2 position, float height);

    /// <summary>
    /// An explosive barrel: in 3D a drum with rims, two bands and a lid; in 2D a plain square
    /// (the 2D look draws its own band on top).
    /// </summary>
    void DrawBarrel(Vector2 position, float radius, float height, Color body, Color band);

    /// <summary>
    /// Soot on a wall or pillar face (3D only): a soft patch from the floor up to the given
    /// height, darkest at the bottom, fading up and to both sides.
    /// </summary>
    void DrawWallStain(Vector2 from, Vector2 to, float height, Color color);

    /// <summary>
    /// A character drawn as its blocky 3D model, posed from what it reports; the 2D look (or a
    /// missing model) draws <paramref name="fallback"/> instead.
    /// </summary>
    void DrawModel(in ModelPose pose, in VisualDescriptor fallback);

    /// <summary>True for the 3D look, for the few things drawn differently there.</summary>
    bool IsThreeD { get; }

    /// <summary>
    /// The floor shadow of something flying at <paramref name="height"/> (a grenade, a rocket): in 3D
    /// a soft dark disc thrown away from the light; nothing in 2D.
    /// </summary>
    void DrawShadow(Vector2 position, float height, float radius);

    /// <summary>A rectangle lying flat at a height (marks on top of a crate, a bar over a head): flat in 2D.</summary>
    void FillRectRaised(Vector2 topLeft, Vector2 size, float height, Color color);

    /// <summary>A shape lying flat at a height (a light on top of a mine): drawn like any visual in 2D.</summary>
    void DrawVisualRaised(in VisualDescriptor visual, Vector2 position, float rotation, float height);

    /// <summary>A block standing on the floor (walls, pillars); flat like a rectangle in 2D.</summary>
    /// <param name="castShadow">False for a block set into a wall (gates), whose shadow would only double the wall's.</param>
    void FillBox(Vector2 topLeft, Vector2 size, float height, Color color, bool castShadow = true);

    void FillCircle(Vector2 center, float radius, Color color);

    void DrawLine(Vector2 from, Vector2 to, float thickness, Color color);

    /// <summary>Draws one line of text; <paramref name="position"/> is its top edge at the aligned side.</summary>
    /// <param name="text">Text to draw.</param>
    /// <param name="position">Top-left, top-center or top-right of the text, depending on <paramref name="align"/>.</param>
    /// <param name="size">Font size in pixels.</param>
    /// <param name="color">Text color.</param>
    /// <param name="align">Horizontal alignment relative to <paramref name="position"/>.</param>
    void DrawText(string text, Vector2 position, float size, Color color, TextAlign align = TextAlign.Left);

    /// <summary>Text whose visible letters are centered on a point, both ways (button labels).</summary>
    void DrawTextCentered(string text, Vector2 center, float size, Color color);
}
