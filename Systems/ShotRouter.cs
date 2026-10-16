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
        bool overkill = false,
        RocketPayload? rocket = null,
        int wallBounces = 0,
        PointBlank? pointBlank = null,
        float executeBelow = 0f)
    {
        _bullets.FireProjectile(
            origin, direction, projectile, damage, ownerIndex, range, pierce, ricochets, ricochetRadius, ricochetDamageMult,
            overkill,
            rocket,
            wallBounces,
            pointBlank,
            executeBelow);
    }

    public Vector2 ClearMuzzle(Vector2 body, Vector2 muzzle)
    {
        return _bullets.ClearMuzzle(body, muzzle);
    }

    public void ThrowExplosive(in ExplosiveShot shot)
    {
        _explosives.Throw(shot);
    }

    public bool PlaceMine(in MineShot shot)
    {
        return _mines.Place(shot);
    }

    public void EmitFlame(in FlameShot shot)
    {
        _flames.Emit(shot);
    }

    public void FireBeam(in BeamShot shot)
    {
        _bullets.FireBeam(shot);
    }
}
