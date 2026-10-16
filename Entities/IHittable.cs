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

    /// <summary>
    /// A bullet or beam hit landing at a point on the body, for things whose armor depends on
    /// where they are struck. Most things treat it as a plain hit.
    /// </summary>
    /// <param name="damage">Damage dealt.</param>
    /// <param name="ownerIndex">Index of the player who fired.</param>
    /// <param name="impact">Where on (or next to) the body the shot lands.</param>
    void HitAt(float damage, int ownerIndex, Vector2 impact)
    {
        Hit(damage, ownerIndex);
    }

    /// <summary>
    /// True when a shot landing at this point meets armor that stops it there (even a piercing
    /// one). Most things have no armor.
    /// </summary>
    bool BlocksShotAt(Vector2 impact)
    {
        return false;
    }

    /// <summary>
    /// Where a shot moving along a ray first meets a shield standing out from the body, if it
    /// does within <paramref name="reach"/>. Most things have no shield.
    /// </summary>
    bool TryHitShield(Vector2 origin, Vector2 direction, float reach, float shotRadius, out float distance, out Vector2 point)
    {
        distance = 0f;
        point = Vector2.Zero;
        return false;
    }

    /// <summary>
    /// The damage that executes it when it is at or below <paramref name="hpShare"/> of its full
    /// HP (its full HP, so the kill lands as one big hit); 0 when it cannot be executed now.
    /// Most things cannot be.
    /// </summary>
    float ExecutionDamage(float hpShare)
    {
        return 0f;
    }

    /// <summary>Damage over time (fire): no hit flash or hit sound for every small tick.</summary>
    /// <param name="damage">Damage dealt this frame.</param>
    /// <param name="ownerIndex">Index of the player whose fire it is.</param>
    void Burn(float damage, int ownerIndex);

    /// <summary>Sets it on fire: it keeps burning for a while after leaving the flames.</summary>
    /// <param name="seconds">How long it keeps burning.</param>
    /// <param name="damagePerSecond">Damage per second while burning.</param>
    /// <param name="ownerIndex">Index of the player whose fire it is.</param>
    void Ignite(float seconds, float damagePerSecond, int ownerIndex);

    /// <summary>Pushes it by a distance and direction; most things do not move.</summary>
    /// <param name="push">Direction times the distance in world units.</param>
    void Knock(Vector2 push)
    {
    }
}
