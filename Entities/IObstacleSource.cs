using Microsoft.Xna.Framework;

namespace Fourgate.Entities;

/// <summary>Solid things on the floor that players and enemies cannot walk through (barrels).</summary>
public interface IObstacleSource
{
    /// <summary>Moves a circle out of every obstacle it overlaps.</summary>
    Vector2 PushOut(Vector2 position, float radius);
}
