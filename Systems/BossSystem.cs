using System;
using System.Collections.Generic;
using Fourgate.Core.Audio;
using Fourgate.Core.Data;
using Fourgate.Core.Rendering;
using Fourgate.Core.Rng;
using Fourgate.Entities;
using Fourgate.Entities.Enemies;
using Microsoft.Xna.Framework;

namespace Fourgate.Systems;

/// <summary>
/// Drives the boss in the arena. The boss body is an ordinary enemy (so every weapon hits it);
/// this system walks it, picks attack patterns, shows their ground markers and carries them out.
/// </summary>
public sealed class BossSystem
{
    private const int RingSegments = 48;

    private static readonly Color MarkerColor = new(255, 60, 40);
    private static readonly Color ShockwaveColor = new(255, 200, 120);

    private readonly EnemySystem _enemies;
    private readonly GameData _data;
    private readonly GameRandom _random;
    private readonly IAudioManager _audio;
    private readonly List<BossPatternConfig> _usable = new();
    private readonly List<Player> _hitThisAttack = new();
    private readonly List<Vector2> _summonPoints = new();

    private State _state;
    private float _stateLeft;
    private BossPatternConfig? _pattern;
    private Vector2 _direction;
    private Vector2 _origin;
    private float _reach;
    private float _travelled;
    private float _ringRadius;

    public BossSystem(EnemySystem enemies, GameData data, GameRandom random, IAudioManager audio)
    {
        _enemies = enemies;
        _data = data;
        _random = random;
        _audio = audio;
    }

    private enum State
    {
        Walking,
        Telegraph,
        Acting,
    }

    /// <summary>Multiplies a boss's HP when it appears (co-op scaling); 1 for one player.</summary>
    public float HpMultiplier { get; set; } = 1f;

    /// <summary>Summons stop while this many enemies are already in the arena.</summary>
    public int MaxAliveEnemies { get; set; } = int.MaxValue;

    /// <summary>The boss in the arena, or null.</summary>
    public Enemy? Boss { get; private set; }

    public BossConfig? Config { get; private set; }

    /// <summary>True while a pattern's ground marker is showing.</summary>
    public bool IsTelegraphing => Boss is not null && _state == State.Telegraph;

    /// <summary>The pattern being warned about or carried out; null while walking.</summary>
    public BossPatternType? CurrentPattern => Boss is not null && _state != State.Walking ? _pattern?.Type : null;

    public void Update(float deltaSeconds, IReadOnlyList<Player> players, Arena arena)
    {
        if (!TrackBoss())
        {
            return;
        }

        var boss = Boss!;
        var target = NearestLivingPlayer(boss.Position, players);
        if (target is null)
        {
            return;
        }

        switch (_state)
        {
            case State.Walking:
                Walk(boss, target, arena, deltaSeconds);
                _stateLeft -= deltaSeconds;
                if (_stateLeft <= 0f && target.CanSee(boss.Position, boss.Radius))
                {
                    TryStartPattern(boss, target, arena);
                }

                break;

            case State.Telegraph:
                _stateLeft -= deltaSeconds;
                if (_stateLeft <= 0f)
                {
                    StartActing(boss);
                }

                break;

            case State.Acting:
                Act(boss, players, arena, deltaSeconds);
                break;
        }
    }

    /// <summary>Ground markers of a pattern about to happen; drawn under the enemies.</summary>
    public void DrawMarkers(IRenderer renderer)
    {
        if (Boss is null || _pattern is null || _state != State.Telegraph)
        {
            return;
        }

        var progress = 1f - Math.Clamp(_stateLeft / _pattern.TelegraphSeconds, 0f, 1f);
        switch (_pattern.Type)
        {
            case BossPatternType.LineCharge:
                var end = _origin + _direction * _reach;
                var width = Boss.Radius * 2f;
                renderer.DrawLine(_origin, end, width, MarkerColor * 0.22f);
                renderer.DrawLine(_origin, Vector2.Lerp(_origin, end, progress), width, MarkerColor * 0.4f);
                break;

            case BossPatternType.GroundSlamRing:
                renderer.FillCircle(_origin, _pattern.Radius, MarkerColor * 0.16f);
                renderer.FillCircle(_origin, _pattern.Radius * progress, MarkerColor * 0.28f);
                break;

            case BossPatternType.Summon:
                var size = _data.GetEnemy(_pattern.EnemyId).Radius;
                for (var i = 0; i < _summonPoints.Count; i++)
                {
                    renderer.FillCircle(_summonPoints[i], size, MarkerColor * 0.2f);
                    renderer.FillCircle(_summonPoints[i], size * progress, MarkerColor * 0.45f);
                }

                break;
        }
    }

    /// <summary>The shockwave of a slam in progress; drawn over the enemies.</summary>
    public void DrawEffects(IRenderer renderer)
    {
        if (Boss is null || _pattern is not { Type: BossPatternType.GroundSlamRing } || _state != State.Acting)
        {
            return;
        }

        var step = MathHelper.TwoPi / RingSegments;
        var previous = _origin + new Vector2(_ringRadius, 0f);
        for (var i = 1; i <= RingSegments; i++)
        {
            var angle = step * i;
            var point = _origin + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * _ringRadius;
            renderer.DrawLine(previous, point, _pattern.RingWidth, ShockwaveColor * 0.75f);
            previous = point;
        }
    }

    // Picks up a boss that has just come in through a door and lets go of one that died.
    private bool TrackBoss()
    {
        if (Boss is not null && (!Boss.IsAlive || !ReferenceEquals(Boss.Config, Config?.AsEnemy)))
        {
            Boss = null;
            Config = null;
        }

        if (Boss is not null)
        {
            return true;
        }

        var found = _enemies.FindBoss();
        if (found is null)
        {
            return false;
        }

        Boss = found;
        Config = FindConfig(found.Config);
        found.ScaleHp(HpMultiplier);
        _state = State.Walking;
        _stateLeft = Config?.IdleSeconds ?? 0f;
        return Config is not null;
    }

    private BossConfig? FindConfig(EnemyConfig body)
    {
        var bosses = _data.Bosses;
        for (var i = 0; i < bosses.Count; i++)
        {
            if (ReferenceEquals(bosses[i].AsEnemy, body))
            {
                return bosses[i];
            }
        }

        return null;
    }

    private static void Walk(Enemy boss, Player target, Arena arena, float deltaSeconds)
    {
        var waypoint = arena.NextWaypoint(boss.Position, target.Position, boss.Radius);
        var toWaypoint = waypoint - boss.Position;
        var distance = toWaypoint.Length();
        var stopAt = waypoint == target.Position ? boss.Radius + target.Radius : 0f;
        if (distance <= stopAt + 0.001f)
        {
            return;
        }

        boss.Facing = MathF.Atan2(toWaypoint.Y, toWaypoint.X);
        var step = Math.Min(boss.Config.Speed * deltaSeconds, distance - stopAt);
        boss.Position += toWaypoint / distance * step;
    }

    // Weighted random pick among the patterns that make sense from here; keeps walking when
    // none does (e.g. a pillar blocks the charge and the player is out of slam reach).
    private void TryStartPattern(Enemy boss, Player target, Arena arena)
    {
        var toTarget = target.Position - boss.Position;
        var distance = toTarget.Length();
        var direction = distance > 0.001f ? toTarget / distance : Vector2.UnitX;
        var clearAhead = arena.Raycast(boss.Position, direction, out _);

        _usable.Clear();
        var totalWeight = 0f;
        var patterns = Config!.Patterns;
        for (var i = 0; i < patterns.Count; i++)
        {
            var pattern = patterns[i];
            var usable = pattern.Type switch
            {
                BossPatternType.LineCharge => clearAhead >= distance && distance <= pattern.Distance + target.Radius,
                BossPatternType.GroundSlamRing => distance <= pattern.Radius,
                BossPatternType.Summon => _enemies.Count + pattern.Count <= MaxAliveEnemies,
                _ => false,
            };

            if (usable)
            {
                _usable.Add(pattern);
                totalWeight += pattern.Weight;
            }
        }

        if (_usable.Count == 0)
        {
            return;
        }

        var roll = _random.Range(0f, totalWeight);
        _pattern = _usable[^1];
        for (var i = 0; i < _usable.Count; i++)
        {
            roll -= _usable[i].Weight;
            if (roll < 0f)
            {
                _pattern = _usable[i];
                break;
            }
        }

        _state = State.Telegraph;
        _stateLeft = _pattern.TelegraphSeconds;
        _origin = boss.Position;
        _direction = direction;
        boss.Facing = MathF.Atan2(direction.Y, direction.X);
        _reach = _pattern.Type == BossPatternType.LineCharge ? Math.Min(_pattern.Distance, clearAhead) : 0f;

        _summonPoints.Clear();
        if (_pattern.Type == BossPatternType.Summon)
        {
            var size = _data.GetEnemy(_pattern.EnemyId).Radius;
            for (var i = 0; i < _pattern.Count; i++)
            {
                var angle = MathHelper.TwoPi * i / _pattern.Count;
                var point = boss.Position + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * _pattern.SpawnRadius;
                _summonPoints.Add(arena.PlaceOnFloor(point, size));
            }
        }

        _audio.PlaySfx(SfxIds.BossTelegraph);
    }

    private void StartActing(Enemy boss)
    {
        _state = State.Acting;
        _travelled = 0f;
        _ringRadius = boss.Radius;
        _origin = boss.Position;
        _hitThisAttack.Clear();
        _audio.PlaySfx(SfxIds.BossAttack);
    }

    private void Act(Enemy boss, IReadOnlyList<Player> players, Arena arena, float deltaSeconds)
    {
        var pattern = _pattern!;
        var done = false;
        switch (pattern.Type)
        {
            case BossPatternType.LineCharge:
                var step = Math.Min(pattern.Speed * deltaSeconds, _reach - _travelled);
                var wanted = boss.Position + _direction * step;
                var reached = arena.ResolveMovement(wanted, boss.Radius);
                boss.Position = reached;
                _travelled += step;
                HitTouching(boss.Position, boss.Radius, players, pattern.Damage);

                // Stops at the end of its run, or early against a wall, pillar or barrel.
                done = _travelled >= _reach - 0.01f || Vector2.DistanceSquared(reached, wanted) > 1f;
                break;

            case BossPatternType.GroundSlamRing:
                _ringRadius += pattern.RingSpeed * deltaSeconds;
                HitInRing(players, pattern);
                done = _ringRadius >= pattern.Radius;
                break;

            case BossPatternType.Summon:
                var config = _data.GetEnemy(pattern.EnemyId);
                for (var i = 0; i < _summonPoints.Count; i++)
                {
                    _enemies.Spawn(config, _summonPoints[i]);
                }

                done = true;
                break;
        }

        if (done)
        {
            _state = State.Walking;
            _stateLeft = Config!.IdleSeconds;
        }
    }

    // A dashing player is not hit and can still be caught later in the same attack.
    private void HitTouching(Vector2 position, float radius, IReadOnlyList<Player> players, float damage)
    {
        for (var i = 0; i < players.Count; i++)
        {
            var player = players[i];
            if (!player.IsAlive || _hitThisAttack.Contains(player))
            {
                continue;
            }

            var reach = radius + player.Radius;
            if (Vector2.DistanceSquared(position, player.Position) <= reach * reach && player.TakeDamage(damage))
            {
                _hitThisAttack.Add(player);
            }
        }
    }

    private void HitInRing(IReadOnlyList<Player> players, BossPatternConfig pattern)
    {
        for (var i = 0; i < players.Count; i++)
        {
            var player = players[i];
            if (!player.IsAlive || _hitThisAttack.Contains(player))
            {
                continue;
            }

            var distance = Vector2.Distance(player.Position, _origin);
            if (Math.Abs(distance - _ringRadius) <= pattern.RingWidth / 2f + player.Radius && player.TakeDamage(pattern.Damage))
            {
                _hitThisAttack.Add(player);
            }
        }
    }

    private static Player? NearestLivingPlayer(Vector2 position, IReadOnlyList<Player> players)
    {
        Player? nearest = null;
        var best = float.MaxValue;
        for (var i = 0; i < players.Count; i++)
        {
            if (!players[i].IsAlive)
            {
                continue;
            }

            var distance = Vector2.DistanceSquared(position, players[i].Position);
            if (distance < best)
            {
                best = distance;
                nearest = players[i];
            }
        }

        return nearest;
    }
}
