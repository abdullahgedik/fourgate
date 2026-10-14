using Microsoft.Xna.Framework;

namespace Fourgate.Entities;

/// <summary>A solid block inside the arena, in world coordinates.</summary>
/// <param name="Center">Middle of the block.</param>
/// <param name="HalfSize">Half its width and half its height.</param>
public readonly record struct Pillar(Vector2 Center, Vector2 HalfSize);
