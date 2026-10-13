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

    public void FireProjectile(
        Vector2 origin,
        Vector2 direction,
        ProjectileConfig projectile,
        float damage,
        int ownerIndex,
        float? range = null,
        int pierce = 0,
        int ricochets = 0)
    {
        Count++;
        LastOrigin = origin;
        LastDirection = direction;
        LastDamage = damage;
        LastRange = range;
        LastPierce = pierce;
        LastRicochets = ricochets;
    }
}
