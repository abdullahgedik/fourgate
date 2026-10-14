using Fourgate.Core.Data;
using Fourgate.Weapons;
using Microsoft.Xna.Framework;

namespace Fourgate.Systems;

/// <summary>Hands each kind of shot to the system that simulates it.</summary>
public sealed class ShotRouter : IShotHandler
{
    private readonly ShotSystem _bullets;
    private readonly ExplosiveSystem _explosives;

    public ShotRouter(ShotSystem bullets, ExplosiveSystem explosives)
    {
        _bullets = bullets;
        _explosives = explosives;
    }

    public void FireProjectile(
        Vector2 origin,
        Vector2 direction,
        ProjectileConfig projectile,
        float damage,
        int ownerIndex,
        float? range = null,
        int pierce = 0,
        int ricochets = 0,
        float ricochetRadius = 0f,
        float ricochetDamageMult = 1f)
    {
        _bullets.FireProjectile(
            origin, direction, projectile, damage, ownerIndex, range, pierce, ricochets, ricochetRadius, ricochetDamageMult);
    }

    public void ThrowExplosive(in ExplosiveShot shot)
    {
        _explosives.Throw(shot);
    }
}
