using Fourgate.Core.Data;
using Fourgate.Weapons;
using Microsoft.Xna.Framework;

namespace Fourgate.Tests;

/// <summary>Records fired shots instead of resolving them.</summary>
public sealed class FakeShotHandler : IShotHandler
{
    public int Count { get; private set; }

    public Vector2 LastOrigin { get; private set; }

    public Vector2 LastDirection { get; private set; }

    public float LastDamage { get; private set; }

    public void FireProjectile(
        Vector2 origin, Vector2 direction, ProjectileConfig projectile, float damage, int ownerIndex)
    {
        Count++;
        LastOrigin = origin;
        LastDirection = direction;
        LastDamage = damage;
    }
}
