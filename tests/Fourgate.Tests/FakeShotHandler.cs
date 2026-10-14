using Fourgate.Core.Data;
using Fourgate.Weapons;
using Microsoft.Xna.Framework;

namespace Fourgate.Tests;

/// <summary>Records fired shots instead of simulating them.</summary>
public sealed class FakeShotHandler : IShotHandler
{
    public int Count { get; private set; }

    public Vector2 LastOrigin { get; private set; }

    public Vector2 LastDirection { get; private set; }

    public float LastDamage { get; private set; }

    public float? LastRange { get; private set; }

    public int LastPierce { get; private set; }

    public int LastRicochets { get; private set; }

    public float LastRicochetRadius { get; private set; }

    public bool LastOverkill { get; private set; }

    public RocketPayload? LastRocket { get; private set; }

    /// <summary>Every beam fired, in order.</summary>
    public List<BeamShot> Beams { get; } = new();

    /// <summary>Every flame sprayed, in order.</summary>
    public List<FlameShot> Flames { get; } = new();

    /// <summary>Every mine placed, in order.</summary>
    public List<MineShot> Mines { get; } = new();

    /// <summary>Every explosive thrown, in order.</summary>
    public List<ExplosiveShot> Throws { get; } = new();

    /// <summary>Direction of every shot fired, in order.</summary>
    public List<Vector2> Directions { get; } = new();

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
        RocketPayload? rocket = null)
    {
        Count++;
        LastOrigin = origin;
        LastDirection = direction;
        Directions.Add(direction);
        LastDamage = damage;
        LastRange = range;
        LastPierce = pierce;
        LastRicochets = ricochets;
        LastRicochetRadius = ricochetRadius;
        LastOverkill = overkill;
        LastRocket = rocket;
    }

    public void ThrowExplosive(in ExplosiveShot shot)
    {
        Throws.Add(shot);
    }

    public bool PlaceMine(in MineShot shot)
    {
        Mines.Add(shot);
        return true;
    }

    public void FireBeam(in BeamShot shot)
    {
        Count++;
        Beams.Add(shot);
    }

    public void EmitFlame(in FlameShot shot)
    {
        Flames.Add(shot);
    }
}
