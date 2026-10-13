using Microsoft.Xna.Framework;

namespace Fourgate.Entities;

/// <summary>Something a shot can hit: a circle in the world that reacts to damage.</summary>
public interface IHittable
{
    Vector2 Position { get; }

    float Radius { get; }

    /// <param name="damage">Damage dealt.</param>
    /// <param name="ownerIndex">Index of the player who fired.</param>
    void Hit(float damage, int ownerIndex);
}
