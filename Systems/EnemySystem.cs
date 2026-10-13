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

    private readonly IAudioManager _audio;
    private readonly Pool<Enemy> _enemies = new(InitialEnemies);
    private readonly SpatialHash<Enemy> _hash = new(CellSize);
    private readonly List<Enemy> _nearby = new();
    private int _nextId;

    public EnemySystem(IAudioManager audio)
    {
        _audio = audio;
    }

    /// <summary>Enemies currently in the arena, including ones killed this frame.</summary>
    public int Count => _enemies.Count;

    /// <summary>Total enemies killed so far.</summary>
    public int KillCount { get; private set; }

    public Enemy this[int index] => _enemies[index];

    public Enemy Spawn(EnemyConfig config, Vector2 position)
    {
        var enemy = _enemies.Rent();
        enemy.Spawn(_nextId++, config, position);
        return enemy;
    }

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

            MoveTowardNearestPlayer(enemy, players, deltaSeconds);
        }

        RebuildHash();
        for (var i = 0; i < _enemies.Count; i++)
        {
            var enemy = _enemies[i];
            Separate(enemy);
            PushOutOfPlayers(enemy, players);
            enemy.Position = arena.ClampInside(enemy.Position, enemy.Radius);
            AttackTouchingPlayers(enemy, players);
        }

        // Second rebuild so shots fired this frame are tested against final positions.
        RebuildHash();
    }

    public void Draw(IRenderer renderer)
    {
        for (var i = 0; i < _enemies.Count; i++)
        {
            var enemy = _enemies[i];
            renderer.DrawVisual(enemy.Visual, enemy.Position, 0f);
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
        for (var i = _enemies.Count - 1; i >= 0; i--)
        {
            if (!_enemies[i].IsAlive)
            {
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

    private static void MoveTowardNearestPlayer(Enemy enemy, IReadOnlyList<Player> players, float deltaSeconds)
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

        // Walk up to the player and stop when touching.
        var distance = MathF.Sqrt(bestDistanceSquared);
        var gap = distance - (enemy.Radius + target.Radius);
        if (gap <= 0f)
        {
            return;
        }

        var step = Math.Min(enemy.Config.Speed * deltaSeconds, gap);
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
