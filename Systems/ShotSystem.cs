using System;
using System.Collections.Generic;
using Fourgate.Core.Data;
using Fourgate.Core.Pooling;
using Fourgate.Core.Rendering;
using Fourgate.Entities;
using Fourgate.Entities.Projectiles;
using Fourgate.Weapons;
using Microsoft.Xna.Framework;

namespace Fourgate.Systems;

/// <summary>Owns and moves player bullets and resolves what they hit.</summary>
public sealed class ShotSystem
{
    private const int InitialBullets = 128;
    private const float StreakLength = 90f;
    private const float StreakThickness = 4f;
    private const float RocketLength = 30f;
    private const float RocketThickness = 10f;

    // How close to a wall line counts as touching it when working out a bounce.
    private const float WallSlack = 0.5f;

    private static readonly Color BulletColor = new(255, 225, 120);
    private static readonly Color RocketColor = new(200, 205, 215);
    private static readonly Color RocketFlameColor = new(255, 150, 50);

    private readonly Arena _arena;
    private readonly Pool<Bullet> _bullets = new(InitialBullets);
    private readonly List<IHittable> _targets = new();
    private readonly List<IHittableSource> _sources = new();
    private readonly List<IHittable> _candidates = new();

    public ShotSystem(Arena arena)
    {
        _arena = arena;
    }

    public int BulletCount => _bullets.Count;

    /// <summary>Where rockets explode. Without it, rockets hit like ordinary bullets.</summary>
    public ExplosiveSystem? Explosives { get; set; }

    /// <summary>Debug switch: every bullet kills what it hits.</summary>
    public bool OneShotKills { get; set; }

    /// <summary>Registers a single thing bullets can hit.</summary>
    public void AddTarget(IHittable target)
    {
        _targets.Add(target);
    }

    /// <summary>Registers a searchable group of things bullets can hit (the enemies).</summary>
    public void AddSource(IHittableSource source)
    {
        _sources.Add(source);
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
        RocketPayload? rocket = null)
    {
        _bullets.Rent().Launch(
            origin,
            direction,
            projectile.Speed,
            projectile.Radius,
            range ?? projectile.Range,
            damage,
            ownerIndex,
            pierce,
            ricochets,
            ricochetRadius,
            ricochetDamageMult,
            overkill,
            rocket);
    }

    public void Update(float deltaSeconds)
    {
        for (var i = _bullets.Count - 1; i >= 0; i--)
        {
            var bullet = _bullets[i];
            if (bullet.IsSpent)
            {
                _bullets.ReturnAt(i);
            }
            else
            {
                Step(bullet, deltaSeconds);
            }
        }
    }

    public void Draw(IRenderer renderer)
    {
        for (var i = 0; i < _bullets.Count; i++)
        {
            var bullet = _bullets[i];
            if (bullet.Rocket is null)
            {
                renderer.DrawLine(bullet.GetTail(StreakLength), bullet.Position, StreakThickness, BulletColor);
                continue;
            }

            // A rocket: a short, thick body with a flame behind it.
            var tail = bullet.GetTail(RocketLength);
            renderer.DrawLine(bullet.GetTail(RocketLength * 1.8f), tail, RocketThickness * 0.7f, RocketFlameColor);
            renderer.DrawLine(tail, bullet.Position, RocketThickness, RocketColor);
        }
    }

    // Sweeps the whole line the bullet covers this frame, so it cannot skip over a
    // target however fast it flies.
    private void Step(Bullet bullet, float deltaSeconds)
    {
        var origin = bullet.Position;
        var direction = bullet.Direction;

        var distance = bullet.Speed * deltaSeconds;
        var outOfRange = false;
        var atWall = false;

        if (bullet.RangeLeft <= distance)
        {
            distance = bullet.RangeLeft;
            outOfRange = true;
        }

        var wallDistance = _arena.DistanceToWall(origin, direction);
        if (wallDistance <= distance)
        {
            distance = wallDistance;
            atWall = true;
            outOfRange = false;
        }

        // Gather everything near the line travelled this frame, then test each exactly.
        _candidates.Clear();
        _candidates.AddRange(_targets);
        var middle = origin + direction * (distance * 0.5f);
        for (var i = 0; i < _sources.Count; i++)
        {
            _sources[i].Collect(middle, distance * 0.5f + bullet.Radius, _candidates);
        }

        // Hit targets nearest first; a piercing bullet carries on to the next one.
        while (true)
        {
            var hit = FindNearest(bullet, origin, direction, distance, out var hitDistance);
            if (hit is null)
            {
                break;
            }

            if (bullet.Rocket is not null && Explosives is not null)
            {
                // A rocket does not hit directly: its blast hits everything, including this target.
                bullet.Advance(hitDistance);
                ExplodeRocket(bullet);
                return;
            }

            var damage = OneShotKills ? float.MaxValue : bullet.Damage;
            var hpBefore = hit.HitPointsLeft;
            hit.Hit(damage, bullet.OwnerIndex);
            if (bullet.PierceLeft > 0)
            {
                bullet.PassThrough(hit);
                continue;
            }

            if (bullet.Overkill && damage > hpBefore)
            {
                // The kill used up only part of the bullet; the rest flies on.
                bullet.CarryOn(hit, damage - hpBefore);
                continue;
            }

            bullet.Advance(hitDistance);
            if (!TryBounceToEnemy(bullet, hit))
            {
                bullet.Stop();
            }

            return;
        }

        bullet.Advance(distance);
        if (atWall && bullet.RicochetsLeft > 0 && bullet.RangeLeft > 0f)
        {
            // The rest of this frame's travel is dropped; the bullet continues next frame.
            bullet.Bounce(Reflect(bullet.Position, direction));
        }
        else if (atWall || outOfRange)
        {
            if (bullet.Rocket is not null && Explosives is not null)
            {
                ExplodeRocket(bullet);
                return;
            }

            bullet.Stop();
        }
    }

    private void ExplodeRocket(Bullet bullet)
    {
        bullet.Stop();
        Explosives!.QueueRocketBlast(bullet.Position, bullet.Damage, bullet.Rocket!.Value, bullet.OwnerIndex);
    }

    // Sends a bullet that still has a ricochet from the enemy it just hit to the nearest
    // other enemy within its ricochet radius. The rest of this frame's travel is dropped.
    private bool TryBounceToEnemy(Bullet bullet, IHittable from)
    {
        if (bullet.RicochetsLeft <= 0 || bullet.RicochetRadius <= 0f)
        {
            return false;
        }

        _candidates.Clear();
        _candidates.AddRange(_targets);
        for (var i = 0; i < _sources.Count; i++)
        {
            _sources[i].Collect(from.Position, bullet.RicochetRadius, _candidates);
        }

        IHittable? next = null;
        var nextDistance = bullet.RicochetRadius;
        for (var i = 0; i < _candidates.Count; i++)
        {
            var target = _candidates[i];
            if (ReferenceEquals(target, from) || bullet.HasHit(target))
            {
                continue;
            }

            var distance = Vector2.Distance(from.Position, target.Position);
            if (distance <= nextDistance)
            {
                nextDistance = distance;
                next = target;
            }
        }

        if (next is null)
        {
            return false;
        }

        var toNext = next.Position - bullet.Position;
        var length = toNext.Length();
        if (length < 0.001f)
        {
            return false;
        }

        bullet.BounceTo(from, toNext / length, length);
        return true;
    }

    private IHittable? FindNearest(
        Bullet bullet, Vector2 origin, Vector2 direction, float maxDistance, out float nearestDistance)
    {
        IHittable? nearest = null;
        nearestDistance = maxDistance;
        for (var i = 0; i < _candidates.Count; i++)
        {
            var target = _candidates[i];
            if (bullet.HasHit(target))
            {
                continue;
            }

            if (TryIntersect(origin, direction, target.Position, target.Radius + bullet.Radius, out var distance)
                && distance <= nearestDistance)
            {
                nearestDistance = distance;
                nearest = target;
            }
        }

        return nearest;
    }

    // Mirrors the direction on whichever wall (or two, in a corner) the point is touching.
    private Vector2 Reflect(Vector2 position, Vector2 direction)
    {
        if (position.X <= _arena.Left + WallSlack || position.X >= _arena.Right - WallSlack)
        {
            direction.X = -direction.X;
        }

        if (position.Y <= _arena.Top + WallSlack || position.Y >= _arena.Bottom - WallSlack)
        {
            direction.Y = -direction.Y;
        }

        return direction;
    }

    /// <summary>Distance along a ray to where it enters a circle; 0 when the ray starts inside it.</summary>
    private static bool TryIntersect(
        Vector2 origin, Vector2 direction, Vector2 center, float radius, out float distance)
    {
        distance = 0f;

        var toCenter = center - origin;
        var radiusSquared = radius * radius;
        if (toCenter.LengthSquared() <= radiusSquared)
        {
            return true;
        }

        var along = Vector2.Dot(toCenter, direction);
        if (along < 0f)
        {
            return false;
        }

        var offsetSquared = toCenter.LengthSquared() - along * along;
        if (offsetSquared > radiusSquared)
        {
            return false;
        }

        distance = along - MathF.Sqrt(radiusSquared - offsetSquared);
        return true;
    }
}
