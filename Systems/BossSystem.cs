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
    private const float SpoutDistance = 22f;
    private const float SpoutSize = 7f;

    private static readonly Color MarkerColor = new(255, 60, 40);
    private static readonly Color ShockwaveColor = new(255, 200, 120);
    private static readonly Color FireColor = new(255, 120, 30);
    private static readonly Color FireCoreColor = new(255, 220, 120);
    private static readonly Color ArmorColor = new(200, 205, 215);

    // A turning boss only charges at a target this close to straight ahead (degrees either side).
    private const float ChargeAheadDegrees = 30f;
    private const int ArmorPlateSegments = 14;

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
    private int _burstsLeft;
    private float _nextThrowIn;
    private float _elapsed;
    private Vector2 _teleportTo;
    private float _streamLength;

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

    /// <summary>Where fireball patterns throw; without it they warn but throw nothing.</summary>
    public EnemyShotSystem? Fireballs { get; set; }

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
                TurnToward(boss, target.Position, deltaSeconds);
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

            case BossPatternType.FireballRing:
            case BossPatternType.FireballSpiral:
                // A swelling glow, with a dot where each fireball (or spiral arm) will leave.
                renderer.FillCircle(_origin, Boss.Radius * (1.2f + 0.6f * progress), MarkerColor * (0.25f + 0.4f * progress));
                var spouts = _pattern.Count;
                var start = MathF.Atan2(_direction.Y, _direction.X);
                for (var i = 0; i < spouts; i++)
                {
                    var angle = start + MathHelper.TwoPi * i / spouts;
                    var dot = _origin + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * (Boss.Radius + SpoutDistance);
                    renderer.FillCircle(dot, SpoutSize * (0.5f + 0.5f * progress), MarkerColor);
                }

                break;

            case BossPatternType.FireStream:
                // The reach it can sweep, and the lane where the fire starts.
                renderer.FillCircle(_origin, _pattern.Distance, MarkerColor * 0.1f);
                var streamEnd = _origin + _direction * _streamLength;
                renderer.DrawLine(_origin, streamEnd, _pattern.Width, MarkerColor * 0.25f);
                renderer.DrawLine(_origin, Vector2.Lerp(_origin, streamEnd, progress), _pattern.Width, MarkerColor * 0.45f);
                break;

            case BossPatternType.Teleport:
                renderer.FillCircle(_origin, Boss.Radius * (1.5f - 0.5f * progress), MarkerColor * 0.2f);
                renderer.FillCircle(_teleportTo, Boss.Radius, MarkerColor * 0.2f);
                renderer.FillCircle(_teleportTo, Boss.Radius * progress, MarkerColor * 0.45f);
                break;
        }
    }

    /// <summary>The shockwave of a slam in progress; drawn over the enemies.</summary>
    public void DrawEffects(IRenderer renderer)
    {
        if (Boss is null)
        {
            return;
        }

        DrawArmor(renderer, Boss);
        if (_pattern is { Type: BossPatternType.FireStream } && _state == State.Acting)
        {
            var end = Boss.Position + _direction * _streamLength;
            renderer.DrawLine(Boss.Position, end, _pattern.Width, FireColor * 0.85f);
            renderer.DrawLine(Boss.Position, end, _pattern.Width * 0.4f, FireCoreColor);
            return;
        }

        if (_pattern is not { Type: BossPatternType.GroundSlamRing } || _state != State.Acting)
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

    // A plate across the armored front, so players can see which side to avoid. Its inner edge
    // overlaps the body, where blocked shots stop.
    private void DrawArmor(IRenderer renderer, Enemy boss)
    {
        if (!boss.HasShieldBand)
        {
            return;
        }

        // A curved band along exactly the armored arc, where shots are stopped.
        var halfArc = MathHelper.ToRadians(boss.Config.FrontArmorArcDegrees / 2f);
        var radius = (boss.ShieldInnerRadius + boss.ShieldOuterRadius) / 2f;
        var thickness = boss.Config.FrontArmorThickness;
        var previous = boss.Position + Direction(boss.Facing - halfArc) * radius;
        for (var i = 1; i <= ArmorPlateSegments; i++)
        {
            var angle = boss.Facing - halfArc + 2f * halfArc * i / ArmorPlateSegments;
            var point = boss.Position + Direction(angle) * radius;
            renderer.DrawLine(previous, point, thickness, ArmorColor);
            previous = point;
        }
    }

    private static Vector2 Direction(float angle)
    {
        return new Vector2(MathF.Cos(angle), MathF.Sin(angle));
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

    // Turns at the boss's turn rate (at once when it has none), so a slow turner can be flanked.
    private void TurnToward(Enemy boss, Vector2 point, float deltaSeconds)
    {
        var desired = MathF.Atan2(point.Y - boss.Position.Y, point.X - boss.Position.X);
        var rate = Config?.TurnDegreesPerSecond ?? 0f;
        if (rate <= 0f)
        {
            boss.Facing = desired;
            return;
        }

        var difference = MathHelper.WrapAngle(desired - boss.Facing);
        var most = MathHelper.ToRadians(rate) * deltaSeconds;
        boss.Facing = MathHelper.WrapAngle(boss.Facing + Math.Clamp(difference, -most, most));
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
                BossPatternType.LineCharge => clearAhead >= distance && distance <= pattern.Distance + target.Radius
                    && IsAhead(boss, direction),
                BossPatternType.GroundSlamRing => distance <= pattern.Radius,
                BossPatternType.Summon => _enemies.Count + pattern.Count <= MaxAliveEnemies,
                BossPatternType.FireballRing => distance <= pattern.ProjectileRange,
                BossPatternType.FireballSpiral => distance <= pattern.ProjectileRange,
                BossPatternType.Teleport => arena.Doors.Count > 1,
                BossPatternType.FireStream => distance <= pattern.Distance,
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

        // A fire stream starts where a slow turner is facing; everything else is aimed at the target.
        if (_pattern.Type == BossPatternType.FireStream && Config.TurnDegreesPerSecond > 0f)
        {
            _direction = new Vector2(MathF.Cos(boss.Facing), MathF.Sin(boss.Facing));
        }

        boss.Facing = MathF.Atan2(_direction.Y, _direction.X);
        _streamLength = _pattern.Type == BossPatternType.FireStream
            ? Math.Min(_pattern.Distance, arena.Raycast(boss.Position, _direction, out _))
            : 0f;
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

        if (_pattern.Type == BossPatternType.Teleport)
        {
            _teleportTo = PickTeleportSpot(boss, arena);
        }

        _audio.PlaySfx(SfxIds.BossTelegraph);
    }

    private bool IsAhead(Enemy boss, Vector2 direction)
    {
        if (Config is not { TurnDegreesPerSecond: > 0f })
        {
            return true;
        }

        var facing = new Vector2(MathF.Cos(boss.Facing), MathF.Sin(boss.Facing));
        return Vector2.Dot(facing, direction) >= MathF.Cos(MathHelper.ToRadians(ChargeAheadDegrees));
    }

    // Just inside a random door other than the one the boss is nearest to.
    private Vector2 PickTeleportSpot(Enemy boss, Arena arena)
    {
        var doors = arena.Doors;
        var nearest = 0;
        for (var i = 1; i < doors.Count; i++)
        {
            if (Vector2.DistanceSquared(doors[i].Center, boss.Position)
                < Vector2.DistanceSquared(doors[nearest].Center, boss.Position))
            {
                nearest = i;
            }
        }

        var pick = _random.NextIndex(doors.Count - 1);
        if (pick >= nearest)
        {
            pick++;
        }

        // Far enough in that the whole body is on the floor.
        var door = doors[pick];
        return arena.PlaceOnFloor(arena.GetDoorSpawnPoint(door, boss.Radius, 0f) + door.Inward * boss.Radius, boss.Radius);
    }

    private void StartActing(Enemy boss)
    {
        _state = State.Acting;
        _travelled = 0f;
        _ringRadius = boss.Radius;
        _origin = boss.Position;
        _burstsLeft = _pattern!.Bursts;
        _nextThrowIn = 0f;
        _elapsed = 0f;
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

            case BossPatternType.FireballRing:
                _nextThrowIn -= deltaSeconds;
                if (_nextThrowIn <= 0f)
                {
                    // Each burst is turned half a gap, so standing in a gap of the last one is not safe.
                    var burst = pattern.Bursts - _burstsLeft;
                    var gap = MathHelper.TwoPi / pattern.Count;
                    ThrowSpread(boss, pattern, MathF.Atan2(_direction.Y, _direction.X) + gap * 0.5f * burst);
                    _burstsLeft--;
                    _nextThrowIn = pattern.Interval;
                }

                done = _burstsLeft <= 0;
                break;

            case BossPatternType.FireballSpiral:
                _elapsed += deltaSeconds;
                _nextThrowIn -= deltaSeconds;
                if (_nextThrowIn <= 0f)
                {
                    var spin = MathHelper.ToRadians(pattern.SpinDegreesPerSecond) * _elapsed;
                    ThrowSpread(boss, pattern, MathF.Atan2(_direction.Y, _direction.X) + spin);
                    _nextThrowIn += pattern.Interval;
                }

                done = _elapsed >= pattern.Seconds;
                break;

            case BossPatternType.Teleport:
                boss.Position = _teleportTo;
                done = true;
                break;

            case BossPatternType.FireStream:
                // Sweeps round from where it started; pillars and walls cut it short.
                _elapsed += deltaSeconds;
                var start = MathF.Atan2(_direction.Y, _direction.X);
                var angle = start + MathHelper.ToRadians(pattern.SpinDegreesPerSecond) * deltaSeconds;
                _direction = new Vector2(MathF.Cos(angle), MathF.Sin(angle));
                boss.Facing = angle;
                _streamLength = Math.Min(pattern.Distance, arena.Raycast(boss.Position, _direction, out _));
                BurnAlongStream(boss, players, pattern, deltaSeconds);
                done = _elapsed >= pattern.Seconds;
                break;
        }

        if (done)
        {
            _state = State.Walking;
            _stateLeft = Config!.IdleSeconds;
        }
    }

    private void BurnAlongStream(Enemy boss, IReadOnlyList<Player> players, BossPatternConfig pattern, float deltaSeconds)
    {
        for (var i = 0; i < players.Count; i++)
        {
            var player = players[i];
            if (!player.IsAlive)
            {
                continue;
            }

            var along = Math.Clamp(Vector2.Dot(player.Position - boss.Position, _direction), 0f, _streamLength);
            var reach = pattern.Width / 2f + player.Radius;
            if (Vector2.DistanceSquared(boss.Position + _direction * along, player.Position) <= reach * reach)
            {
                player.TakeDamage(pattern.Damage * deltaSeconds, silent: true);
            }
        }
    }

    // Count fireballs spread evenly all the way round, the first along the given angle.
    private void ThrowSpread(Enemy boss, BossPatternConfig pattern, float firstAngle)
    {
        if (Fireballs is null)
        {
            return;
        }

        for (var i = 0; i < pattern.Count; i++)
        {
            var angle = firstAngle + MathHelper.TwoPi * i / pattern.Count;
            var direction = new Vector2(MathF.Cos(angle), MathF.Sin(angle));
            Fireballs.Fire(
                boss.Position + direction * boss.Radius, direction, pattern.ProjectileSpeed, pattern.ProjectileRadius,
                pattern.Damage, pattern.ProjectileRange);
        }

        _audio.PlaySfx(SfxIds.EnemyFireball);
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
