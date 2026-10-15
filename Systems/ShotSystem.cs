using System;
using System.Collections.Generic;
using Fourgate.Core.Audio;
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

    private static readonly Color BulletColor = new(255, 225, 120);
    private static readonly Color RocketColor = new(200, 205, 215);
    private static readonly Color RocketFlameColor = new(255, 150, 50);
    private static readonly Color BeamGlowColor = new(90, 180, 255);
    private static readonly Color BeamCoreColor = new(225, 245, 255);
    private static readonly Color SparkColor = new(255, 230, 150);
    private static readonly Color SparkCoreColor = new(255, 255, 255);

    // A spark where a shot glances off armor.
    private const float SparkSeconds = 0.2f;
    private const float SparkStartRadius = 5f;
    private const float SparkEndRadius = 22f;
    private const int SparkLines = 6;
    private const float SparkLineLength = 16f;

    // How long a beam stays on screen after firing, and how wide its glow is drawn.
    private const float BeamFadeSeconds = 0.3f;
    private const float BeamGlowScale = 3f;

    // How high shots fly in the 3D view (chest height); ignored in 2D.
    private const float ShotHeight = 18f;

    // Where on a body hit effects burst, as a share of its height.
    private const float HitHeightOnBody = 0.8f;

    private readonly Arena _arena;
    private readonly IAudioManager? _audio;
    private readonly Pool<Bullet> _bullets = new(InitialBullets);
    private readonly List<IHittable> _targets = new();
    private readonly List<IHittableSource> _sources = new();
    private readonly List<IHittable> _candidates = new();
    private readonly HashSet<IHittable> _beamHits = new();
    private readonly List<BeamFlash> _beams = new();
    private readonly List<Spark> _sparks = new();

    public ShotSystem(Arena arena, IAudioManager? audio = null)
    {
        _arena = arena;
        _audio = audio;
    }

    /// <summary>Sparks still showing where shots glanced off armor.</summary>
    public int SparkCount => _sparks.Count;

    public int BulletCount => _bullets.Count;

    /// <summary>Beams still fading on screen.</summary>
    public int BeamCount => _beams.Count;

    /// <summary>Where hit puffs, dust and sparks go; null draws none (tests).</summary>
    public ParticleSystem? Particles { get; set; }

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
        RocketPayload? rocket = null,
        int wallBounces = 0)
    {
        _bullets.Rent().Launch(
            origin,
            direction,
            projectile.Speed,
            projectile.Radius,
            range ?? projectile.Range ?? float.PositiveInfinity,
            damage,
            ownerIndex,
            pierce,
            ricochets,
            ricochetRadius,
            ricochetDamageMult,
            overkill,
            rocket,
            projectile.Knockback,
            projectile.HitEffect,
            wallBounces);
    }

    /// <summary>
    /// Fires an instant beam: everything along it is hit at once, up to the first wall, pillar
    /// or armored front. The whole beam shows at once, so nothing reacts before it is visibly struck.
    /// </summary>
    public void FireBeam(in BeamShot shot)
    {
        var length = Math.Min(shot.Range, _arena.Raycast(shot.Origin, shot.Direction, out _));

        _candidates.Clear();
        _candidates.AddRange(_targets);
        var middle = shot.Origin + shot.Direction * (length * 0.5f);
        for (var i = 0; i < _sources.Count; i++)
        {
            _sources[i].Collect(middle, length * 0.5f + shot.Width, _candidates);
        }

        // A shield band or armor where the beam lands cuts it short.
        var blockedAt = -1f;
        for (var i = 0; i < _candidates.Count; i++)
        {
            var target = _candidates[i];
            if (target.TryHitShield(shot.Origin, shot.Direction, length, shot.Width, out var shieldAt, out _))
            {
                length = shieldAt;
                blockedAt = length;
            }

            if (!TouchesLine(shot.Origin, shot.Direction, length, shot.Width, target))
            {
                continue;
            }

            var along = BeamEntry(shot.Origin, shot.Direction, target);
            if (along < length && target.BlocksShotAt(shot.Origin + shot.Direction * along))
            {
                length = Math.Max(0f, along);
                blockedAt = length;
            }
        }

        var end = shot.Origin + shot.Direction * length;
        var damage = OneShotKills ? float.MaxValue : shot.Damage;
        _beamHits.Clear();
        for (var i = 0; i < _candidates.Count; i++)
        {
            var target = _candidates[i];
            if (!_beamHits.Add(target) || !TouchesLine(shot.Origin, shot.Direction, length, shot.Width, target))
            {
                continue;
            }

            var entry = shot.Origin + shot.Direction * BeamEntry(shot.Origin, shot.Direction, target);
            target.HitAt(damage, shot.OwnerIndex, entry);
            Particles?.BodyHit(shot.HitEffect, entry, shot.Direction, HitHeight(target), damage);
            target.Knock(shot.Direction * shot.Knockback);
        }

        if (shot.EndBlastRadius > 0f && Explosives is not null)
        {
            Explosives.QueueRocketBlast(end, shot.EndBlastDamage, new RocketPayload(shot.EndBlastRadius), shot.OwnerIndex);
        }

        _beams.Add(new BeamFlash(shot.Origin, end, shot.Width, 0f));
        Particles?.BeamFlash(shot.Origin, end, ShotHeight, BeamCoreColor, BeamGlowColor);
        if (blockedAt >= 0f)
        {
            AddSpark(end);
        }
    }

    // Hits show on the upper body (bigger bodies are taller), never below the shot itself.
    private static float HitHeight(IHittable target)
    {
        return MathF.Max(ShotHeight, Depth3D.BodyHeight(target.Radius) * HitHeightOnBody);
    }

    // Distance along the beam to where it first meets a target's body.
    private static float BeamEntry(Vector2 origin, Vector2 direction, IHittable target)
    {
        var toTarget = target.Position - origin;
        var along = Vector2.Dot(toTarget, direction);
        var offsetSquared = toTarget.LengthSquared() - along * along;
        var radiusSquared = target.Radius * target.Radius;
        return offsetSquared >= radiusSquared ? along : along - MathF.Sqrt(radiusSquared - offsetSquared);
    }

    private void AddSpark(Vector2 position)
    {
        _sparks.Add(new Spark(position, 0f));
        _audio?.PlaySfx(SfxIds.ArmorHit);
    }

    public void Update(float deltaSeconds)
    {
        for (var i = _sparks.Count - 1; i >= 0; i--)
        {
            var age = _sparks[i].Age + deltaSeconds;
            if (age >= SparkSeconds)
            {
                _sparks[i] = _sparks[^1];
                _sparks.RemoveAt(_sparks.Count - 1);
            }
            else
            {
                _sparks[i] = _sparks[i] with { Age = age };
            }
        }

        for (var i = _beams.Count - 1; i >= 0; i--)
        {
            var beam = _beams[i];
            var age = beam.Age + deltaSeconds;
            if (age >= BeamFadeSeconds)
            {
                _beams[i] = _beams[^1];
                _beams.RemoveAt(_beams.Count - 1);
            }
            else
            {
                _beams[i] = beam with { Age = age };
            }
        }

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
        for (var i = 0; i < _sparks.Count; i++)
        {
            // A flash that grows and fades, with short rays flying out.
            var spark = _sparks[i];
            var t = spark.Age / SparkSeconds;
            var fade = 1f - t;
            var radius = MathHelper.Lerp(SparkStartRadius, SparkEndRadius, t);
            renderer.FillCircleRaised(spark.Position, ShotHeight, radius, SparkColor * (0.6f * fade));
            renderer.FillCircleRaised(spark.Position, ShotHeight, radius * 0.4f, SparkCoreColor * fade);
            for (var r = 0; r < SparkLines; r++)
            {
                var angle = MathHelper.TwoPi * r / SparkLines + 0.4f;
                var ray = new Vector2(MathF.Cos(angle), MathF.Sin(angle));
                renderer.DrawLineRaised(
                    spark.Position + ray * radius, spark.Position + ray * (radius + SparkLineLength * fade), ShotHeight, 2f,
                    SparkColor * fade);
            }
        }

        for (var i = 0; i < _beams.Count; i++)
        {
            var beam = _beams[i];
            var fade = 1f - beam.Age / BeamFadeSeconds;
            if (renderer.IsThreeD)
            {
                // A wide soft halo and light on the floor below (3D only).
                renderer.DrawLine(beam.From, beam.To, beam.Width * 7f * fade, BeamGlowColor * (0.14f * fade));
                renderer.DrawLineRaised(beam.From, beam.To, ShotHeight, beam.Width * 6f * fade, BeamGlowColor * (0.18f * fade));
            }

            renderer.DrawLineRaised(
                beam.From, beam.To, ShotHeight, beam.Width * BeamGlowScale * fade, BeamGlowColor * (0.6f * fade));
            renderer.DrawLineRaised(beam.From, beam.To, ShotHeight, beam.Width * fade, BeamCoreColor * fade);
        }

        for (var i = 0; i < _bullets.Count; i++)
        {
            var bullet = _bullets[i];
            if (bullet.Rocket is null)
            {
                renderer.DrawLineRaised(bullet.GetTail(StreakLength), bullet.Position, ShotHeight, StreakThickness, BulletColor);
                continue;
            }

            // A rocket: a short, thick body with a flame behind it, and its shadow below in 3D.
            renderer.DrawShadow(bullet.GetTail(RocketLength * 0.5f), ShotHeight, RocketThickness);
            var tail = bullet.GetTail(RocketLength);
            renderer.DrawLineRaised(bullet.GetTail(RocketLength * 1.8f), tail, ShotHeight, RocketThickness * 0.7f, RocketFlameColor);
            renderer.DrawLineRaised(tail, bullet.Position, ShotHeight, RocketThickness, RocketColor);
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
        var wallNormal = Vector2.Zero;

        if (bullet.RangeLeft <= distance)
        {
            distance = bullet.RangeLeft;
            outOfRange = true;
        }

        var wallDistance = _arena.Raycast(origin, direction, out var surfaceNormal);
        if (wallDistance <= distance)
        {
            distance = wallDistance;
            atWall = true;
            wallNormal = surfaceNormal;
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

        // A shield band in the way stops the shot where it meets it.
        var shieldDistance = float.MaxValue;
        var shieldPoint = Vector2.Zero;
        for (var i = 0; i < _candidates.Count; i++)
        {
            if (_candidates[i].TryHitShield(origin, direction, distance, bullet.Radius, out var d, out var p)
                && d < shieldDistance)
            {
                shieldDistance = d;
                shieldPoint = p;
            }
        }

        // Hit targets nearest first; a piercing bullet carries on to the next one.
        while (true)
        {
            var hit = FindNearest(bullet, origin, direction, distance, out var hitDistance);
            if (shieldDistance < float.MaxValue && (hit is null || hitDistance > shieldDistance))
            {
                bullet.Advance(shieldDistance);
                if (bullet.Rocket is not null && Explosives is not null)
                {
                    ExplodeRocket(bullet);
                    return;
                }

                bullet.Stop();
                AddSpark(shieldPoint);
                return;
            }

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
            // The point on the body the bullet meets.
            var touch = origin + direction * hitDistance;
            var impact = hit.Position + SafeNormalize(touch - hit.Position) * hit.Radius;
            if (hit.BlocksShotAt(impact))
            {
                // Armor stops it outright: no damage worth the name, no piercing, carrying on or bouncing.
                hit.HitAt(damage, bullet.OwnerIndex, impact);
                bullet.Advance(hitDistance);
                bullet.Stop();
                AddSpark(impact);
                return;
            }

            hit.HitAt(damage, bullet.OwnerIndex, impact);
            hit.Knock(direction * bullet.Knockback);
            Particles?.BodyHit(bullet.HitEffect, impact, direction, HitHeight(hit), damage);

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
        if (atWall && bullet.WallBouncesLeft > 0 && bullet.RangeLeft > 0f)
        {
            // The rest of this frame's travel is dropped; the bullet continues next frame.
            Particles?.Sparks(bullet.Position);
            _audio?.PlaySfx(SfxIds.BulletRicochet);
            bullet.Bounce(Reflect(direction, wallNormal));
        }
        else if (atWall || outOfRange)
        {
            if (bullet.Rocket is not null && Explosives is not null)
            {
                ExplodeRocket(bullet);
                return;
            }

            if (atWall)
            {
                Particles?.WallHit(bullet.HitEffect, bullet.Position, direction);
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

    // Mirrors the direction on each axis the surface faces; a corner's diagonal normal flips both,
    // sending the bullet straight back.
    private static Vector2 Reflect(Vector2 direction, Vector2 normal)
    {
        if (normal.X != 0f)
        {
            direction.X = -direction.X;
        }

        if (normal.Y != 0f)
        {
            direction.Y = -direction.Y;
        }

        return direction;
    }

    // True when a circle overlaps the beam's line between its start and end.
    private static bool TouchesLine(Vector2 origin, Vector2 direction, float length, float width, IHittable target)
    {
        var toTarget = target.Position - origin;
        var along = Math.Clamp(Vector2.Dot(toTarget, direction), 0f, length);
        var reach = target.Radius + width;
        return Vector2.DistanceSquared(origin + direction * along, target.Position) <= reach * reach;
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

    private static Vector2 SafeNormalize(Vector2 vector)
    {
        return vector.LengthSquared() > 0.000001f ? Vector2.Normalize(vector) : Vector2.Zero;
    }

    // A flash where a shot glanced off armor.
    private readonly record struct Spark(Vector2 Position, float Age);

    // A fired beam fading out on screen.
    private readonly record struct BeamFlash(Vector2 From, Vector2 To, float Width, float Age);
}
