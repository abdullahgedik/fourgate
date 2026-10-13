using Microsoft.Xna.Framework;

namespace Fourgate.Core.Rendering;

/// <summary>
/// Drawing surface in virtual coordinates. Gameplay and UI draw through this so the
/// shape-based implementation can later be replaced by sprites.
/// </summary>
public interface IRenderer
{
    void FillRect(Vector2 topLeft, Vector2 size, Color color);

    void FillCircle(Vector2 center, float radius, Color color);

    void DrawLine(Vector2 from, Vector2 to, float thickness, Color color);
}
