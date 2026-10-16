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
    /// <param name="ricochets">Times the bullet bounces from an enemy it hits on to another enemy.</param>
    /// <param name="ricochetRadius">How far from an enemy it hit the bullet looks for another enemy to bounce to.</param>
    /// <param name="ricochetDamageMult">Damage multiplier applied each time the bullet bounces to an enemy.</param>
    /// <param name="overkill">After a kill, the leftover damage keeps flying to the next enemy in line.</param>
    /// <param name="rocket">Makes it a rocket: it explodes where it stops instead of hitting directly.</param>
    /// <param name="wallBounces">Times the bullet bounces off walls and pillars instead of stopping.</param>
    /// <param name="pointBlank">Makes hits close to the muzzle hit harder and push further.</param>
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
        bool overkill = false,
        RocketPayload? rocket = null,
        int wallBounces = 0,
        PointBlank? pointBlank = null);

    /// <summary>Throws an explosive that flies to its target and explodes where it lands.</summary>
    void ThrowExplosive(in ExplosiveShot shot);

    /// <summary>Puts a mine or barrel down. Returns false when it could not be placed there.</summary>
    bool PlaceMine(in MineShot shot);

    /// <summary>Sprays one flame.</summary>
    void EmitFlame(in FlameShot shot);

    void FireBeam(in BeamShot shot);

    /// <summary>
    /// A gun's muzzle, pulled back toward the body when a wall, a pillar or an enemy is between
    /// them, so a shot never starts on the far side of something it should have hit.
    /// </summary>
    Vector2 ClearMuzzle(Vector2 body, Vector2 muzzle);
}
