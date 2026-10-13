using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;

namespace Fourgate.Entities.Projectiles;

/// <summary>A fast, straight-flying player bullet. Pooled; <see cref="Launch"/> resets all state.</summary>
public sealed class Bullet
{
    private readonly List<IHittable> _alreadyHit = new();
    private Vector2 _origin;
    private float _travelled;

    /// <summary>Unit-length flight direction.</summary>
    public Vector2 Direction { get; private set; }

    /// <summary>World units per second.</summary>
    public float Speed { get; private set; }

    public float Radius { get; private set; }

    public float Damage { get; private set; }

    /// <summary>Index of the player who fired it.</summary>
    public int OwnerIndex { get; private set; }

    /// <summary>World units left before the bullet runs out of range.</summary>
    public float RangeLeft { get; private set; }

    /// <summary>Enemies the bullet can still pass through.</summary>
    public int PierceLeft { get; private set; }

    /// <summary>Wall bounces the bullet has left.</summary>
    public int RicochetsLeft { get; private set; }

    /// <summary>
    /// True once the bullet has hit something or run out of range. It stays for one more
    /// frame so it is seen arriving at the point where it stopped.
    /// </summary>
    public bool IsSpent { get; private set; }

    public Vector2 Position => _origin + Direction * _travelled;

    public void Launch(
        Vector2 origin,
        Vector2 direction,
        float speed,
        float radius,
        float range,
        float damage,
        int ownerIndex,
        int pierce,
        int ricochets)
    {
        _origin = origin;
        _travelled = 0f;
        _alreadyHit.Clear();
        Direction = direction;
        Speed = speed;
        Radius = radius;
        RangeLeft = range;
        Damage = damage;
        OwnerIndex = ownerIndex;
        PierceLeft = pierce;
        RicochetsLeft = ricochets;
        IsSpent = false;
    }

    /// <summary>Moves forward along the flight direction.</summary>
    public void Advance(float distance)
    {
        _travelled += distance;
        RangeLeft -= distance;
    }

    /// <summary>Marks the bullet as stopped at its current position.</summary>
    public void Stop()
    {
        IsSpent = true;
    }

    /// <summary>True when the bullet has passed through this target and must not hit it again.</summary>
    public bool HasHit(IHittable target)
    {
        return _alreadyHit.Contains(target);
    }

    /// <summary>Uses up one pierce on a target the bullet now passes through.</summary>
    public void PassThrough(IHittable target)
    {
        PierceLeft--;
        _alreadyHit.Add(target);
    }

    /// <summary>Uses up one ricochet and continues from the current position in a new direction.</summary>
    public void Bounce(Vector2 newDirection)
    {
        _origin = Position;
        _travelled = 0f;
        _alreadyHit.Clear();
        Direction = newDirection;
        RicochetsLeft--;
    }

    /// <summary>Trailing end of a streak of the given length; never behind where the bullet started or last bounced.</summary>
    public Vector2 GetTail(float length)
    {
        return _origin + Direction * Math.Max(0f, _travelled - length);
    }
}
