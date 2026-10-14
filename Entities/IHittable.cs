using Microsoft.Xna.Framework;

namespace Fourgate.Entities;

/// <summary>Something a shot can hit: a circle in the world that reacts to damage.</summary>
public interface IHittable
{
    Vector2 Position { get; }

    float Radius { get; }

    /// <summary>Damage it can still take before it dies; infinite for things that cannot die.</summary>
    float HitPointsLeft { get; }

    /// <param name="damage">Damage dealt.</param>
    /// <param name="ownerIndex">Index of the player who fired.</param>
    void Hit(float damage, int ownerIndex);

    /// <summary>Damage over time (fire): no hit flash or hit sound for every small tick.</summary>
    /// <param name="damage">Damage dealt this frame.</param>
    /// <param name="ownerIndex">Index of the player whose fire it is.</param>
    void Burn(float damage, int ownerIndex);

    /// <summary>Sets it on fire: it keeps burning for a while after leaving the flames.</summary>
    /// <param name="seconds">How long it keeps burning.</param>
    /// <param name="damagePerSecond">Damage per second while burning.</param>
    /// <param name="ownerIndex">Index of the player whose fire it is.</param>
    void Ignite(float seconds, float damagePerSecond, int ownerIndex);
}
