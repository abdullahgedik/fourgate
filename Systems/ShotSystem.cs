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
public sealed class ShotSystem : IShotHandler
{
    private const int InitialBullets = 128;
    private const float StreakLength = 90f;
    private const float StreakThickness = 4f;

    private static readonly Color BulletColor = new(255, 225, 120);

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
        Vector2 origin, Vector2 direction, ProjectileConfig projectile, float damage, int ownerIndex)
    {
        _bullets.Rent().Launch(
            origin, direction, projectile.Speed, projectile.Radius, projectile.Range, damage, ownerIndex);
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
            renderer.DrawLine(bullet.GetTail(StreakLength), bullet.Position, StreakThickness, BulletColor);
        }
    }

    // Sweeps the whole line the bullet covers this frame, so it cannot skip over a
    // target however fast it flies.
    private void Step(Bullet bullet, float deltaSeconds)
    {
        var origin = bullet.Position;
        var direction = bullet.Direction;

        var distance = bullet.Speed * deltaSeconds;
        var stops = false;

        if (bullet.RangeLeft <= distance)
        {
            distance = bullet.RangeLeft;
            stops = true;
        }

        var wallDistance = _arena.DistanceToWall(origin, direction);
        if (wallDistance <= distance)
        {
            distance = wallDistance;
            stops = true;
        }

        // Gather everything near the line travelled this frame, then test each exactly.
        _candidates.Clear();
        _candidates.AddRange(_targets);
        var middle = origin + direction * (distance * 0.5f);
        for (var i = 0; i < _sources.Count; i++)
        {
            _sources[i].Collect(middle, distance * 0.5f + bullet.Radius, _candidates);
        }

        IHittable? hit = null;
        for (var i = 0; i < _candidates.Count; i++)
        {
            var target = _candidates[i];
            if (TryIntersect(origin, direction, target.Position, target.Radius + bullet.Radius, out var targetDistance)
                && targetDistance <= distance)
            {
                distance = targetDistance;
                hit = target;
            }
        }

        bullet.Advance(distance);
        if (hit is not null)
        {
            hit.Hit(bullet.Damage, bullet.OwnerIndex);
            stops = true;
        }

        if (stops)
        {
            bullet.Stop();
        }
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
