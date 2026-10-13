using System;
using Microsoft.Xna.Framework;

namespace Fourgate.Entities.Projectiles;

/// <summary>A fast, straight-flying player bullet. Pooled; <see cref="Launch"/> resets all state.</summary>
public sealed class Bullet
{
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

    /// <summary>
    /// True once the bullet has hit something or run out of range. It stays for one more
    /// frame so it is seen arriving at the point where it stopped.
    /// </summary>
    public bool IsSpent { get; private set; }

    public Vector2 Position => _origin + Direction * _travelled;

    public void Launch(Vector2 origin, Vector2 direction, float speed, float radius, float range, float damage, int ownerIndex)
    {
        _origin = origin;
        _travelled = 0f;
        Direction = direction;
        Speed = speed;
        Radius = radius;
        RangeLeft = range;
        Damage = damage;
        OwnerIndex = ownerIndex;
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

    /// <summary>Trailing end of a streak of the given length; never behind the muzzle.</summary>
    public Vector2 GetTail(float length)
    {
        return _origin + Direction * Math.Max(0f, _travelled - length);
    }
}
