using Fourgate.Core.Data;
using Microsoft.Xna.Framework;

namespace Fourgate.Weapons;

/// <summary>Receives the shots weapons fire into the world.</summary>
public interface IShotHandler
{
    /// <summary>Fires a straight-flying bullet.</summary>
    /// <param name="origin">World position of the muzzle.</param>
    /// <param name="direction">Unit-length flight direction.</param>
    /// <param name="projectile">Flight values.</param>
    /// <param name="damage">Damage dealt on hit.</param>
    /// <param name="ownerIndex">Index of the player who fired.</param>
    /// <param name="range">World units the bullet travels; the projectile's own range when not given.</param>
    /// <param name="pierce">Enemies the bullet passes through before it stops at the next one.</param>
    /// <param name="ricochets">Times the bullet bounces instead of stopping.</param>
    /// <param name="ricochetRadius">How far from an enemy it hit the bullet looks for another enemy to bounce to; 0 bounces off walls only.</param>
    /// <param name="ricochetDamageMult">Damage multiplier applied each time the bullet bounces to an enemy.</param>
    /// <param name="overkill">After a kill, the leftover damage keeps flying to the next enemy in line.</param>
    void FireProjectile(
        Vector2 origin,
        Vector2 direction,
        ProjectileConfig projectile,
        float damage,
        int ownerIndex,
        float? range = null,
        int pierce = 0,
        int ricochets = 0,
        float ricochetRadius = 0f,
        float ricochetDamageMult = 1f,
        bool overkill = false);

    /// <summary>Throws an explosive that flies to its target and explodes where it lands.</summary>
    void ThrowExplosive(in ExplosiveShot shot);
}
