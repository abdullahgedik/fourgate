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
    void FireProjectile(Vector2 origin, Vector2 direction, ProjectileConfig projectile, float damage, int ownerIndex);
}
