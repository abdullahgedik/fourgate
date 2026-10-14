using Microsoft.Xna.Framework;

namespace Fourgate.Entities;

/// <summary>Solid things on the floor that players and enemies cannot walk through (barrels).</summary>
public interface IObstacleSource
{
    /// <summary>Moves a circle out of every obstacle it overlaps.</summary>
    Vector2 PushOut(Vector2 position, float radius);

    /// <summary>
    /// An enemy attack on an obstacle it is touching (within <paramref name="slack"/>).
    /// Returns true when something was hit, so the attack is used up.
    /// </summary>
    bool StrikeTouching(Vector2 position, float radius, float slack);
}
