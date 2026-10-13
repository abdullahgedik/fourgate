using Microsoft.Xna.Framework;

namespace Fourgate.Core.Rendering;

/// <summary>
/// What an entity looks like, independent of how it is drawn. The size is visual only;
/// hitboxes are defined by the entity.
/// </summary>
/// <param name="Shape">Placeholder shape used by the geometric renderer.</param>
/// <param name="Radius">Half the visual size, in world units.</param>
/// <param name="Color">Tint.</param>
/// <param name="SpriteId">Sprite to use once sprites replace shapes.</param>
public readonly record struct VisualDescriptor(ShapeKind Shape, float Radius, Color Color, string? SpriteId = null);
