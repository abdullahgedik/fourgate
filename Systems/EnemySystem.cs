using System;
using System.Collections.Generic;
using Fourgate.Core;
using Fourgate.Core.Audio;
using Fourgate.Core.Data;
using Fourgate.Core.Pooling;
using Fourgate.Core.Rendering;
using Fourgate.Entities;
using Fourgate.Entities.Enemies;
using Microsoft.Xna.Framework;

namespace Fourgate.Systems;

/// <summary>Owns all enemies: movement toward players, keeping them apart, and removal on death.</summary>
public sealed class EnemySystem : IHittableSource
{
    private const int InitialEnemies = 128;
    private const float CellSize = 96f;

    // How close to touching counts as contact; absorbs float error in the stop distance.
    private const float TouchSlack = 1.5f;
    private const float TelegraphGlowStart = 1.2f;
    private const float TelegraphGlowEnd = 1.9f;

    private static readonly Color TelegraphColor = new(255, 70, 50);

    private readonly IAudioManager _audio;
    private readonly Pool<Enemy> _enemies = new(InitialEnemies);
    private readonly SpatialHash<Enemy> _hash = new(CellSize);
    private readonly List<Enemy> _nearby = new();
    private readonly List<EnemyDeath> _deaths = new();
    private int _nextId;

    public EnemySystem(IAudioManager audio)
    {
        _audio = audio;
    }

    /// <summary>Enemies currently in the arena, including ones killed this frame.</summary>
    public int Count => _enemies.Count;

    /// <summary>Total enemies killed so far.</summary>
    public int KillCount { get; private set; }

    /// <summary>Enemies removed by the latest <see cref="Update"/>, for rewards and drops.</summary>
    public IReadOnlyList<EnemyDeath> Deaths => _deaths;

    public Enemy this[int index] => _enemies[index];

    public Enemy Spawn(EnemyConfig config, Vector2 position)
    {
        var enemy = _enemies.Rent();
        enemy.Spawn(_nextId++, config, position);
        return enemy;
    }

    /// <summary>Where ranged enemies' fireballs go; without it they wind up but throw nothing.</summary>
    public EnemyShotSystem? Fireballs { get; set; }

    /// <summary>Share of an enemy's remaining knockback applied per second; 0 applies it at once.</summary>
    public float KnockbackDamping { get; set; }

    public void Update(float deltaSeconds, IReadOnlyList<Player> players, Arena arena)
    {
        RemoveDead();

        for (var i = 0; i < _enemies.Count; i++)
        {
            var enemy = _enemies[i];
            enemy.Tick(deltaSeconds);
            if (enemy.ConsumeHit())
            {
                _audio.PlaySfx(SfxIds.EnemyHit);
            }

            if (!UpdateRangedAttack(enemy, players, arena, deltaSeconds))
            {
                MoveTowardNearestPlayer(enemy, players, arena, deltaSeconds);
            }

            enemy.Position += enemy.TakeKnockStep(
                KnockbackDamping > 0f ? Math.Min(1f, KnockbackDamping * deltaSeconds) : 1f);
        }

        RebuildHash();
        for (var i = 0; i < _enemies.Count; i++)
        {
            var enemy = _enemies[i];
            Separate(enemy);
            PushOutOfPlayers(enemy, players);
            enemy.Position = arena.ResolveMovement(enemy.Position, enemy.Radius);
            AttackTouchingPlayers(enemy, players);
            AttackTouchingObstacles(enemy, arena);
            SpreadFire(enemy);
        }

        // Second rebuild so shots fired this frame are tested against final positions.
        RebuildHash();
    }

    /// <summary>Glow of enemies winding up a ranged attack; drawn under the enemies.</summary>
    public void DrawTelegraphs(IRenderer renderer)
    {
        for (var i = 0; i < _enemies.Count; i++)
        {
            var enemy = _enemies[i];
            if (!enemy.IsWindingUp || enemy.Config.Attack is null)
            {
                continue;
            }

            // A red glow that swells and brightens as the throw nears.
            var progress = enemy.WindUpProgress;
            var radius = enemy.Radius * MathHelper.Lerp(TelegraphGlowStart, TelegraphGlowEnd, progress);
            renderer.FillCircle(enemy.Position, radius, TelegraphColor * (0.3f + 0.5f * progress));
        }
    }

    public void Draw(IRenderer renderer)
    {
        for (var i = 0; i < _enemies.Count; i++)
        {
            var enemy = _enemies[i];
            renderer.DrawVisual(enemy.Visual, enemy.Position, enemy.Facing);
        }
    }

    public void Collect(Vector2 center, float radius, List<IHittable> results)
    {
        _nearby.Clear();
        _hash.Query(center, radius, _nearby);
        for (var i = 0; i < _nearby.Count; i++)
        {
            if (_nearby[i].IsAlive)
            {
                results.Add(_nearby[i]);
            }
        }
    }

    private void RemoveDead()
    {
        _deaths.Clear();
        for (var i = _enemies.Count - 1; i >= 0; i--)
        {
            var enemy = _enemies[i];
            if (!enemy.IsAlive)
            {
                _deaths.Add(new EnemyDeath(enemy.Config, enemy.Position, enemy.LastHitBy));
                KillCount++;
                _audio.PlaySfx(SfxIds.EnemyDeath);
                _enemies.ReturnAt(i);
            }
        }
    }

    private void RebuildHash()
    {
        _hash.Clear();
        for (var i = 0; i < _enemies.Count; i++)
        {
            var enemy = _enemies[i];
            _hash.Insert(enemy, enemy.Position, enemy.Radius);
        }
    }

    // A ranged enemy stops to attack when its target is in range, in clear sight past the
    // pillars, and able to see it (so nobody is shot from off-screen). Returns true while it
    // holds still: winding up, or waiting out its cooldown in a good spot.
    private bool UpdateRangedAttack(Enemy enemy, IReadOnlyList<Player> players, Arena arena, float deltaSeconds)
    {
        var attack = enemy.Config.Attack;
        if (attack is null || !enemy.IsAlive)
        {
            return false;
        }

        if (enemy.IsWindingUp)
        {
            if (enemy.TickWindUp(deltaSeconds, attack.CooldownSeconds))
            {
                ThrowVolley(enemy, attack);
                _audio.PlaySfx(SfxIds.EnemyFireball);
            }

            return true;
        }

        var target = NearestLivingPlayer(enemy.Position, players);
        if (target is null)
        {
            return false;
        }

        var toTarget = target.Position - enemy.Position;
        var distance = toTarget.Length();
        if (distance > attack.Range || distance < 0.0001f || !target.CanSee(enemy.Position, enemy.Radius))
        {
            return false;
        }

        var direction = toTarget / distance;
        if (arena.Raycast(enemy.Position, direction, out _) < distance)
        {
            return false;
        }

        enemy.Facing = MathF.Atan2(direction.Y, direction.X);
        if (enemy.CanStartRangedAttack)
        {
            enemy.StartWindUp(direction, attack.TelegraphSeconds);
            _audio.PlaySfx(SfxIds.EnemyTelegraph);
        }
        else if (attack.StrafeSpeed > 0f)
        {
            // Sidestep across the line to the target, harder to pin down between throws.
            var side = new Vector2(-direction.Y, direction.X) * enemy.StrafeSide;
            enemy.Position += side * (attack.StrafeSpeed * deltaSeconds);
        }

        return true;
    }

    // One fireball along the locked aim, or a volley fanned evenly across the spread.
    private void ThrowVolley(Enemy enemy, EnemyAttackConfig attack)
    {
        if (Fireballs is null)
        {
            return;
        }

        var aim = enemy.AimDirection;
        var origin = enemy.Position + aim * enemy.Radius;
        if (attack.Count <= 1)
        {
            Fireballs.Fire(origin, aim, attack);
            return;
        }

        var spread = MathHelper.ToRadians(attack.SpreadDegrees);
        for (var i = 0; i < attack.Count; i++)
        {
            var angle = MathHelper.Lerp(-spread, spread, i / (attack.Count - 1f));
            var cos = MathF.Cos(angle);
            var sin = MathF.Sin(angle);
            Fireballs.Fire(origin, new Vector2(aim.X * cos - aim.Y * sin, aim.X * sin + aim.Y * cos), attack);
        }
    }

    private static Player? NearestLivingPlayer(Vector2 position, IReadOnlyList<Player> players)
    {
        Player? nearest = null;
        var bestDistanceSquared = float.MaxValue;
        for (var i = 0; i < players.Count; i++)
        {
            if (!players[i].IsAlive)
            {
                continue;
            }

            var distanceSquared = Vector2.DistanceSquared(position, players[i].Position);
            if (distanceSquared < bestDistanceSquared)
            {
                bestDistanceSquared = distanceSquared;
                nearest = players[i];
            }
        }

        return nearest;
    }

    private static void MoveTowardNearestPlayer(
        Enemy enemy, IReadOnlyList<Player> players, Arena arena, float deltaSeconds)
    {
        Player? target = null;
        var bestDistanceSquared = float.MaxValue;
        for (var i = 0; i < players.Count; i++)
        {
            if (!players[i].IsAlive)
            {
                continue;
            }

            var distanceSquared = Vector2.DistanceSquared(enemy.Position, players[i].Position);
            if (distanceSquared < bestDistanceSquared)
            {
                bestDistanceSquared = distanceSquared;
                target = players[i];
            }
        }

        if (target is null)
        {
            return;
        }

        // With a pillar in the way, head for its corner first.
        var waypoint = arena.NextWaypoint(enemy.Position, target.Position, enemy.Radius);
        if (waypoint != target.Position)
        {
            var toCorner = waypoint - enemy.Position;
            var cornerDistance = toCorner.Length();
            if (cornerDistance > 0.0001f)
            {
                enemy.Facing = MathF.Atan2(toCorner.Y, toCorner.X);
                enemy.Position += toCorner / cornerDistance * Math.Min(enemy.Config.Speed * deltaSeconds, cornerDistance);
            }

            return;
        }

        // Walk up to the player and stop when touching.
        var distance = MathF.Sqrt(bestDistanceSquared);
        var gap = distance - (enemy.Radius + target.Radius);
        if (gap <= 0f)
        {
            return;
        }

        var step = Math.Min(enemy.Config.Speed * deltaSeconds, gap);
        enemy.Facing = MathF.Atan2(target.Position.Y - enemy.Position.Y, target.Position.X - enemy.Position.X);
        enemy.Position += (target.Position - enemy.Position) / distance * step;
    }

    // Each overlapping pair is seen once from each side and each side moves half the
    // overlap, so together they end up just touching.
    private void Separate(Enemy enemy)
    {
        _nearby.Clear();
        _hash.Query(enemy.Position, enemy.Radius, _nearby);

        var push = Vector2.Zero;
        for (var i = 0; i < _nearby.Count; i++)
        {
            var other = _nearby[i];
            if (ReferenceEquals(other, enemy))
            {
                continue;
            }

            var away = enemy.Position - other.Position;
            var distance = away.Length();
            var overlap = enemy.Radius + other.Radius - distance;
            if (overlap <= 0f)
            {
                continue;
            }

            // Exactly stacked enemies have no "away" direction; split them by spawn order.
            var direction = distance > 0.001f
                ? away / distance
                : (enemy.Id < other.Id ? Vector2.UnitX : -Vector2.UnitX);
            push += direction * (overlap * 0.5f);
        }

        enemy.Position += push;
    }

    // One contact attack per cooldown. The attempt uses up the cooldown even when the
    // player dodges it with dash i-frames.
    private static void AttackTouchingPlayers(Enemy enemy, IReadOnlyList<Player> players)
    {
        if (!enemy.IsAlive || !enemy.CanAttack || enemy.Config.ContactDamage <= 0f)
        {
            return;
        }

        for (var i = 0; i < players.Count; i++)
        {
            var player = players[i];
            if (!player.IsAlive)
            {
                continue;
            }

            var reach = enemy.Radius + player.Radius + TouchSlack;
            if (Vector2.DistanceSquared(enemy.Position, player.Position) <= reach * reach)
            {
                player.TakeDamage(enemy.Config.ContactDamage);
                enemy.StartAttackCooldown();
                return;
            }
        }
    }

    // Zombies hit barrels in their way (and any they brush past) with their contact attack;
    // a player in reach is always hit first.
    private static void AttackTouchingObstacles(Enemy enemy, Arena arena)
    {
        if (!enemy.IsAlive || !enemy.CanAttack || enemy.Config.ContactDamage <= 0f || arena.Obstacles is null)
        {
            return;
        }

        if (arena.Obstacles.StrikeTouching(enemy.Position, enemy.Radius, TouchSlack))
        {
            enemy.StartAttackCooldown();
        }
    }

    // A burning enemy that caught fire from the flames themselves sets the enemies touching
    // it alight; those cannot pass it on again.
    private void SpreadFire(Enemy enemy)
    {
        if (!enemy.CanSpreadFire)
        {
            return;
        }

        _nearby.Clear();
        _hash.Query(enemy.Position, enemy.Radius + TouchSlack, _nearby);
        for (var i = 0; i < _nearby.Count; i++)
        {
            var other = _nearby[i];
            if (!ReferenceEquals(other, enemy) && other.IsAlive && !other.IsBurning)
            {
                other.Ignite(enemy.BurnSeconds, enemy.BurnDamagePerSecond, enemy.BurnOwner, canSpread: false);
            }
        }
    }

    private static void PushOutOfPlayers(Enemy enemy, IReadOnlyList<Player> players)
    {
        for (var i = 0; i < players.Count; i++)
        {
            var player = players[i];
            if (!player.IsAlive)
            {
                continue;
            }

            var away = enemy.Position - player.Position;
            var distance = away.Length();
            var overlap = enemy.Radius + player.Radius - distance;
            if (overlap > 0f)
            {
                var direction = distance > 0.001f ? away / distance : Vector2.UnitX;
                enemy.Position += direction * overlap;
            }
        }
    }
}
