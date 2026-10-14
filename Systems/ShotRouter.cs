using Fourgate.Core.Data;
using Fourgate.Weapons;
using Microsoft.Xna.Framework;

namespace Fourgate.Systems;

/// <summary>Hands each kind of shot to the system that simulates it.</summary>
public sealed class ShotRouter : IShotHandler
{
    private readonly ShotSystem _bullets;
    private readonly ExplosiveSystem _explosives;
    private readonly MineSystem _mines;
    private readonly FlameSystem _flames;

    public ShotRouter(ShotSystem bullets, ExplosiveSystem explosives, MineSystem mines, FlameSystem flames)
    {
        _flames = flames;
        _bullets = bullets;
        _explosives = explosives;
        _mines = mines;
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
        float ricochetDamageMult = 1f,
        bool overkill = false)
    {
        _bullets.FireProjectile(
            origin, direction, projectile, damage, ownerIndex, range, pierce, ricochets, ricochetRadius, ricochetDamageMult,
            overkill);
    }

    public void ThrowExplosive(in ExplosiveShot shot)
    {
        _explosives.Throw(shot);
    }

    public void PlaceMine(in MineShot shot)
    {
        _mines.Place(shot);
    }

    public void EmitFlame(in FlameShot shot)
    {
        _flames.Emit(shot);
    }
}
