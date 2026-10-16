using System;
using System.Collections.Generic;
using Fourgate.Core.Data;
using Fourgate.Weapons;
using Microsoft.Xna.Framework;

namespace Fourgate.Entities.Projectiles;

/// <summary>A fast, straight-flying player bullet. Pooled; <see cref="Launch"/> resets all state.</summary>
public sealed class Bullet
{
    private readonly List<IHittable> _alreadyHit = new();
    private Vector2 _origin;
    private float _travelled;
    private float _flown;

    /// <summary>Unit-length flight direction.</summary>
    public Vector2 Direction { get; private set; }

    /// <summary>World units per second.</summary>
    public float Speed { get; private set; }

    public float Radius { get; private set; }

    public float Damage { get; private set; }

    /// <summary>Index of the player who fired it.</summary>
    public int OwnerIndex { get; private set; }

    /// <summary>What a hit throws off (purely visual).</summary>
    public HitEffect HitEffect { get; private set; }

    /// <summary>World units a hit pushes an enemy along the bullet's path.</summary>
    public float Knockback { get; private set; }

    /// <summary>World units left before the bullet runs out of range.</summary>
    public float RangeLeft { get; private set; }

    /// <summary>Enemies the bullet can still pass through.</summary>
    public int PierceLeft { get; private set; }

    /// <summary>Bounces the bullet has left.</summary>
    public int RicochetsLeft { get; private set; }

    /// <summary>The 3D model it flies as (a rocket); null for a streak.</summary>
    public string? Model { get; private set; }

    /// <summary>Bounces off walls and pillars left.</summary>
    public int WallBouncesLeft { get; private set; }

    /// <summary>How far from an enemy it hit the bullet looks for another to bounce to; 0 bounces off walls only.</summary>
    public float RicochetRadius { get; private set; }

    /// <summary>Damage multiplier applied each time the bullet bounces to an enemy.</summary>
    public float RicochetDamageMult { get; private set; }

    /// <summary>After a kill, the leftover damage keeps flying to the next enemy in line.</summary>
    public bool Overkill { get; private set; }

    /// <summary>Set when close hits hit harder and push further.</summary>
    public PointBlank? PointBlank { get; private set; }

    /// <summary>Set for a rocket: it explodes where it stops instead of hitting directly.</summary>
    public RocketPayload? Rocket { get; private set; }

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
        int ricochets,
        float ricochetRadius,
        float ricochetDamageMult,
        bool overkill,
        RocketPayload? rocket,
        float knockback = 0f,
        HitEffect hitEffect = HitEffect.Light,
        int wallBounces = 0,
        string? model = null,
        PointBlank? pointBlank = null)
    {
        PointBlank = pointBlank;
        _flown = 0f;
        WallBouncesLeft = wallBounces;
        Model = model;
        Knockback = knockback;
        HitEffect = hitEffect;
        Rocket = rocket;
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
        RicochetRadius = ricochetRadius;
        RicochetDamageMult = ricochetDamageMult;
        Overkill = overkill;
        IsSpent = false;
    }

    /// <summary>Moves forward along the flight direction.</summary>
    public void Advance(float distance)
    {
        _travelled += distance;
        _flown += distance;
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

    /// <summary>Carries on through a target the bullet just killed, with only the damage it had left.</summary>
    public void CarryOn(IHittable target, float damageLeft)
    {
        Damage = damageLeft;
        _alreadyHit.Add(target);
    }

    /// <summary>Uses up one pierce on a target the bullet now passes through.</summary>
    public void PassThrough(IHittable target)
    {
        PierceLeft--;
        _alreadyHit.Add(target);
    }

    /// <summary>Uses up one wall bounce and continues from the current position in a new direction.</summary>
    public void Bounce(Vector2 newDirection)
    {
        Turn(newDirection);
        WallBouncesLeft--;
    }

    /// <summary>
    /// Uses up one ricochet to bounce from an enemy just hit toward another one.
    /// The bullet gets exactly the range it needs for the bounce.
    /// </summary>
    /// <param name="from">The enemy just hit; it is not hit again.</param>
    /// <param name="newDirection">Unit direction toward the next enemy.</param>
    /// <param name="range">World units the bullet may travel after the bounce.</param>
    public void BounceTo(IHittable from, Vector2 newDirection, float range)
    {
        Turn(newDirection);
        RicochetsLeft--;
        _alreadyHit.Add(from);
        RangeLeft = range;
        Damage *= RicochetDamageMult;
    }

    private void Turn(Vector2 newDirection)
    {
        _origin = Position;
        _travelled = 0f;
        _alreadyHit.Clear();
        Direction = newDirection;
    }

    /// <summary>Distance flown since it was fired, bounces included.</summary>
    public float Flown => _flown;

    /// <summary>Distance flown since it was fired or last bounced.</summary>
    public float Travelled => _travelled;

    /// <summary>Trailing end of a streak of the given length; never behind where the bullet started or last bounced.</summary>
    public Vector2 GetTail(float length)
    {
        return _origin + Direction * Math.Max(0f, _travelled - length);
    }
}
