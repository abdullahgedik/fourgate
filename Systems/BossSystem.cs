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
public sealed partial class BossSystem
{
    private const int RingSegments = 48;
    private const float SpoutDistance = 22f;
    private const float SpoutSize = 7f;

    private static readonly Color MarkerColor = new(255, 60, 40);

    // A lethal attack's warning: crimson, stronger, throbbing.
    private static readonly Color LethalColor = new(235, 0, 85);
    private const float LethalBoost = 1.5f;
    private const float LethalPulseSpeed = 14f;
    private const float LethalBeamMarkDistance = 260f;

    // How long a player usually stays in a sweeping fire stream when crossing it.
    private const float FireCrossingSeconds = 0.15f;

    // How quickly a boss's body follows its attacks (per second).
    private const float BodyPoseSpeed = 10f;
    private static readonly Color ShockwaveColor = new(255, 200, 120);
    private static readonly Color FireColor = new(255, 120, 30);
    private static readonly Color FireCoreColor = new(255, 220, 120);
    private static readonly Color ArmorColor = new(200, 205, 215);
    private static readonly Color HasteColor = new(255, 230, 80);
    private static readonly Color MissileColor = new(255, 240, 200);
    private static readonly Color MissileGlowColor = new(255, 120, 40);
    private static readonly Color LaserColor = new(255, 40, 60);
    private static readonly Color LaserCoreColor = new(255, 235, 240);
    private static readonly Color TransitionColor = new(255, 240, 200);

    // A turning boss only charges at a target this close to straight ahead (degrees either side).
    private const float ChargeAheadDegrees = 30f;
    private const int ArmorPlateSegments = 14;

    // In the 3D view: beams leave the boss at about mid-body, and the shield band stands this tall.
    private const float BeamHeightPerRadius = 0.8f;
    private const float ShieldHeightPerRadius = 1.3f;
    private const float ShockwaveHeight = 22f;

    // How high a leap goes, and how far missiles fall, in world units.
    private const float LeapPeakHeight = 260f;
    private const float MissileDropHeight = 700f;
    private const float MissileRadius = 12f;

    // Warning edges rise up to this height as an attack nears.
    private const float WarningWallHeight = 40f;

    // Beam glow: the halo and the floor light, as multiples of the beam's width.
    private const float BeamHaloWidth = 5f;
    private const float BeamFloorLightWidth = 7f;
    private const int WarningRingSegments = 40;

    private readonly EnemySystem _enemies;
    private readonly GameData _data;
    private readonly GameRandom _random;
    private readonly IAudioManager _audio;
    private readonly List<BossPatternConfig> _usable = new();
    private readonly List<Player> _hitThisAttack = new();
    private readonly BossAggro _aggro;
    private Player? _aimedAt;
    private IReadOnlyList<Player>? _players;
    private Color _markerColor = MarkerColor;
    private float _time;
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
    private Vector2 _leapTo;
    private readonly List<Vector2> _missileTargets = new();
    private BossPatternConfig? _queuedFollowUp;
    private int _nextInSequence;
    private int _phase = -1;
    private float _sweepSign = 1f;
    private float _transitionAge;
    private readonly List<BossPatternConfig> _round = new();
    private BossPatternConfig? _lastFromRound;
    private float _hasteLeft;
    private float _hasteMultiplier = 1f;

    public BossSystem(EnemySystem enemies, GameData data, GameRandom random, IAudioManager audio)
    {
        _enemies = enemies;
        _data = data;
        _random = random;
        _audio = audio;
        _aggro = new BossAggro(data.Game.BossAggro, random);
    }

    private enum State
    {
        Walking,
        Telegraph,
        Acting,
        ChangingPhase,
    }

    /// <summary>Multiplies a boss's HP when it appears (co-op scaling); 1 for one player.</summary>
    public float HpMultiplier { get; set; } = 1f;

    /// <summary>Where beam and fire effects go; null draws none (tests).</summary>
    public ParticleSystem? Particles { get; set; }

    /// <summary>Called for every landing slam and missile blast (centre, radius), e.g. for screen shake.</summary>
    public Action<Vector2, float>? Impact { get; set; }

    /// <summary>Where fireball patterns throw; without it they warn but throw nothing.</summary>
    public EnemyShotSystem? Fireballs { get; set; }

    /// <summary>Summons bring in no more enemies once this many are in the arena.</summary>
    public int MaxAliveEnemies { get; set; } = int.MaxValue;

    /// <summary>The boss in the arena, or null.</summary>
    public Enemy? Boss { get; private set; }

    public BossConfig? Config { get; private set; }

    /// <summary>0 for the first phase, then 1, 2... as HP thresholds are crossed.</summary>
    public int Phase => _phase + 1;

    /// <summary>True while the boss is changing phase (it cannot be hurt).</summary>
    public bool IsChangingPhase => Boss is not null && _state == State.ChangingPhase;

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
        _time += deltaSeconds;
        UpdateLethalWarning(players);
        _hasteLeft = Math.Max(0f, _hasteLeft - deltaSeconds);
        if (_state != State.ChangingPhase && TryEnterNextPhase(boss))
        {
            return;
        }
        UpdateBodyPose(boss, deltaSeconds);
        _players = players;
        var target = _aggro.Update(boss, players, deltaSeconds, mayTurn: _state == State.Walking);
        if (target is null)
        {
            return;
        }

        switch (_state)
        {
            case State.Walking:
                Walk(boss, target, arena, deltaSeconds, _hasteLeft > 0f ? _hasteMultiplier : 1f);
                TurnToward(boss, target.Position, deltaSeconds);
                _stateLeft -= deltaSeconds;
                if (_queuedFollowUp is not null)
                {
                    // The attack that goes with the last pattern comes straight away.
                    var followUp = _queuedFollowUp;
                    _queuedFollowUp = null;
                    StartPattern(boss, target, arena, followUp);
                }
                else if (_stateLeft <= 0f && target.CanSee(boss.Position, boss.Radius))
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

            case State.ChangingPhase:
                _stateLeft -= deltaSeconds;
                _transitionAge += deltaSeconds;
                if (_stateLeft <= 0f)
                {
                    boss.IsInvulnerable = false;
                    _state = State.Walking;
                    _stateLeft = ActiveIdleSeconds;
                }

                break;

            case State.Acting:
                Act(boss, players, arena, deltaSeconds);
                break;
        }
    }

    // The body draws back while an attack is warned and commits while it is carried out (and
    // while it charges, streams or flies); both ease, so poses blend instead of snapping.
    private void UpdateBodyPose(Enemy boss, float deltaSeconds)
    {
        var warning = _state == State.Telegraph && _pattern is not null
            ? 1f - Math.Clamp(_stateLeft / _pattern.TelegraphSeconds, 0f, 1f)
            : 0f;
        var acting = _state == State.Acting ? 1f : 0f;
        var ease = Math.Min(1f, deltaSeconds * BodyPoseSpeed);
        boss.BossWindup += (warning - boss.BossWindup) * ease;
        boss.BossAction += (acting - boss.BossAction) * ease;
    }

    /// <summary>Index of the player the boss is after, or -1.</summary>
    public int TargetIndex => _aggro.TargetIndex;

    /// <summary>A player's current threat on the boss (0 when unknown).</summary>
    public float GetThreat(int playerIndex)
    {
        return _aggro.GetThreat(playerIndex);
    }

    private static Player? FarthestLivingPlayer(Vector2 position, IReadOnlyList<Player> players)
    {
        Player? farthest = null;
        var best = -1f;
        for (var i = 0; i < players.Count; i++)
        {
            var distance = Vector2.DistanceSquared(position, players[i].Position);
            if (players[i].IsAlive && distance > best)
            {
                best = distance;
                farthest = players[i];
            }
        }

        return farthest;
    }

    /// <summary>
    /// True while the coming attack would kill a player it hits (its hit is at least the lowest
    /// HP among living players): its warning turns crimson and the views mark it as lethal.
    /// </summary>
    public bool IsLethalWarning { get; private set; }

    /// <summary>Where the lethal attack will land, for the skull drawn over it.</summary>
    public Vector2 LethalPoint { get; private set; }

    /// <summary>Seconds since the boss appeared, for pulsing warnings.</summary>
    public float Time => _time;

    private void UpdateLethalWarning(IReadOnlyList<Player> players)
    {
        var warned = IsLethalWarning;
        var showing = _pattern is not null
            && (_state == State.Telegraph
                || (_state == State.Acting && _pattern.Type is BossPatternType.LeapSlam or BossPatternType.MissileRain));
        IsLethalWarning = showing && HitDamage(_pattern!) >= LowestLivingHp(players);
        if (!IsLethalWarning)
        {
            return;
        }

        LethalPoint = _pattern!.Type switch
        {
            BossPatternType.LineCharge => _origin + _direction * (_reach * 0.5f),
            BossPatternType.LeapSlam => _leapTo,
            BossPatternType.MissileRain when _missileTargets.Count > 0 => _missileTargets[0],
            BossPatternType.LaserSweep or BossPatternType.FireStream => _origin + _direction * LethalBeamMarkDistance,
            _ => Boss!.Position,
        };

        if (!warned)
        {
            _audio.PlaySfx(SfxIds.BossLethalWarning);
        }
    }

    // What one hit of a pattern takes; fire counts what a quick crossing would burn.
    private static float HitDamage(BossPatternConfig pattern)
    {
        return pattern.Type switch
        {
            BossPatternType.Summon or BossPatternType.Teleport or BossPatternType.Haste => 0f,
            BossPatternType.FireStream => pattern.Damage * FireCrossingSeconds,
            _ => pattern.Damage,
        };
    }

    private static float LowestLivingHp(IReadOnlyList<Player> players)
    {
        var lowest = float.MaxValue;
        for (var i = 0; i < players.Count; i++)
        {
            if (players[i].IsAlive)
            {
                lowest = Math.Min(lowest, players[i].Hp);
            }
        }

        return lowest;
    }

    /// <summary>
    /// A boss that died bursts into its brood, if it has one, spread evenly around where it fell.
    /// </summary>
    public void OnBossDied(in EnemyDeath death, Arena arena)
    {
        _audio.PlaySfx(SfxIds.BossDeath);
        _audio.PlayMusic(MusicIds.Gameplay);
        var config = FindConfig(death.Config);
        var spawn = config?.DeathSpawn;
        if (spawn is null)
        {
            return;
        }

        var enemy = _data.GetEnemy(spawn.EnemyId);
        for (var i = 0; i < spawn.Count; i++)
        {
            var angle = MathHelper.TwoPi * i / spawn.Count;
            var point = death.Position + Direction(angle) * spawn.SpawnRadius;
            _enemies.Spawn(enemy, arena.ResolveMovement(point, enemy.Radius));
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
        _aggro.Reset();
        _aimedAt = null;
        _audio.PlaySfx(SfxIds.BossAppear);
        _audio.PlayMusic(MusicIds.Boss);
        Config = FindConfig(found.Config);
        found.ScaleHp(HpMultiplier);
        _state = State.Walking;
        _queuedFollowUp = null;
        _hasteLeft = 0f;
        _nextInSequence = 0;
        _round.Clear();
        _lastFromRound = null;
        _phase = -1;
        _stateLeft = Config?.IdleSeconds ?? 0f;
        _phase = -1;
        found.IsInvulnerable = false;
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

    private static void Walk(Enemy boss, Player target, Arena arena, float deltaSeconds, float speedMultiplier)
    {
        var waypoint = arena.NextWaypoint(boss.Position, target.Position, boss.Radius);
        var toWaypoint = waypoint - boss.Position;
        var distance = toWaypoint.Length();
        var stopAt = waypoint == target.Position ? boss.Radius + target.Radius : 0f;
        if (distance <= stopAt + 0.001f)
        {
            return;
        }

        var step = Math.Min(boss.Config.Speed * speedMultiplier * deltaSeconds, distance - stopAt);
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
        var patterns = ActivePatterns;
        for (var i = 0; i < patterns.Count; i++)
        {
            var pattern = patterns[i];
            var usable = pattern.Type switch
            {
                BossPatternType.LineCharge => clearAhead >= distance && distance <= pattern.Distance + target.Radius
                    && IsAhead(boss, direction),
                BossPatternType.GroundSlamRing => distance <= pattern.Radius,
                // At the enemy cap a summon still happens (and its follow-up); it brings in only what fits.
                BossPatternType.Summon => true,
                BossPatternType.FireballRing => distance <= pattern.ProjectileRange,
                BossPatternType.FireballSpiral => distance <= pattern.ProjectileRange,
                BossPatternType.Teleport => arena.Doors.Count > 1,
                BossPatternType.FireStream => distance <= pattern.Distance,
                BossPatternType.LaserSweep => distance <= pattern.Distance,
                BossPatternType.LeapSlam => distance <= pattern.Distance + boss.Radius + target.Radius && distance > boss.Radius + target.Radius,
                BossPatternType.MissileRain => true,
                BossPatternType.FireballStream => distance <= pattern.ProjectileRange,
                BossPatternType.Haste => true,
                BossPatternType.FireballVolley => distance <= pattern.ProjectileRange,
                _ => false,
            };

            if (usable)
            {
                _usable.Add(pattern);
                totalWeight += pattern.Weight;
            }
        }

        if (ActiveOrder == BossPatternOrder.Shuffled)
        {
            if (_round.Count == 0)
            {
                DealRound(patterns);
            }

            // The next one of this round; it waits (walking) until it makes sense from here.
            var drawn = _round[^1];
            if (_usable.Contains(drawn))
            {
                _round.RemoveAt(_round.Count - 1);
                _lastFromRound = drawn;
                StartPattern(boss, target, arena, drawn);
            }

            return;
        }

        if (ActiveOrder == BossPatternOrder.Sequence)
        {
            // The next one in the list; it waits (walking) until it makes sense from here.
            var next = patterns[_nextInSequence % patterns.Count];
            if (_usable.Contains(next))
            {
                _nextInSequence++;
                StartPattern(boss, target, arena, next);
            }

            return;
        }

        if (_usable.Count == 0)
        {
            return;
        }

        var roll = _random.Range(0f, totalWeight);
        var picked = _usable[^1];
        for (var i = 0; i < _usable.Count; i++)
        {
            roll -= _usable[i].Weight;
            if (roll < 0f)
            {
                picked = _usable[i];
                break;
            }
        }

        StartPattern(boss, target, arena, picked);
    }

    private IReadOnlyList<BossPatternConfig> ActivePatterns =>
        _phase >= 0 ? Config!.Phases[_phase].Patterns : Config!.Patterns;

    private BossPatternOrder ActiveOrder => _phase >= 0 ? Config!.Phases[_phase].PatternOrder : Config!.PatternOrder;

    private float ActiveIdleSeconds =>
        _phase >= 0 ? Config!.Phases[_phase].IdleSeconds ?? Config.IdleSeconds : Config!.IdleSeconds;

    // When HP falls to the next phase's threshold, whatever it was doing is dropped and it
    // changes phase: it stands still, cannot be hurt, and glows.
    private bool TryEnterNextPhase(Enemy boss)
    {
        var phases = Config!.Phases;
        if (_phase + 1 >= phases.Count || boss.MaxHp <= 0f || boss.Hp / boss.MaxHp > phases[_phase + 1].HpFraction)
        {
            return false;
        }

        _phase++;
        _state = State.ChangingPhase;
        _stateLeft = phases[_phase].TransitionSeconds;
        _transitionAge = 0f;
        _queuedFollowUp = null;
        _round.Clear();
        _lastFromRound = null;
        _nextInSequence = 0;
        boss.IsInvulnerable = true;
        boss.Elevation = 0f;
        _audio.PlaySfx(SfxIds.BossPhase);
        return true;
    }

    // Every pattern once, shuffled; drawn from the end. The first one drawn is never the one
    // that ended the last round.
    private void DealRound(IReadOnlyList<BossPatternConfig> patterns)
    {
        _round.Clear();
        _round.AddRange(patterns);
        for (var i = _round.Count - 1; i > 0; i--)
        {
            var j = _random.NextIndex(i + 1);
            (_round[i], _round[j]) = (_round[j], _round[i]);
        }

        if (_round.Count > 1 && ReferenceEquals(_round[^1], _lastFromRound))
        {
            (_round[^1], _round[0]) = (_round[0], _round[^1]);
        }
    }

    // Shows the pattern's warning; it is carried out when the warning has run.
    private void StartPattern(Enemy boss, Player target, Arena arena, BossPatternConfig pattern)
    {
        if (pattern.Target == BossTargetChoice.Farthest && _players is not null)
        {
            target = FarthestLivingPlayer(boss.Position, _players) ?? target;
        }

        _aimedAt = target;
        var toTarget = target.Position - boss.Position;
        var distance = toTarget.Length();
        var direction = distance > 0.001f ? toTarget / distance : Vector2.UnitX;
        var clearAhead = arena.Raycast(boss.Position, direction, out _);

        _pattern = pattern;
        _state = State.Telegraph;
        _stateLeft = _pattern.TelegraphSeconds;
        _origin = boss.Position;
        _direction = direction;

        // A fire stream starts where a slow turner is facing; everything else is aimed at the target.
        if (_pattern.Type == BossPatternType.FireStream && Config!.TurnDegreesPerSecond > 0f)
        {
            _direction = new Vector2(MathF.Cos(boss.Facing), MathF.Sin(boss.Facing));
        }

        // A laser starts to one side of the target and sweeps across it, either way round.
        if (_pattern.Type == BossPatternType.LaserSweep)
        {
            _sweepSign = _random.Chance(0.5f) ? 1f : -1f;
            var half = MathHelper.ToRadians(_pattern.SpinDegreesPerSecond) * _pattern.Seconds * 0.5f;
            _direction = Direction(MathF.Atan2(direction.Y, direction.X) - half * _sweepSign);
        }

        boss.Facing = MathF.Atan2(_direction.Y, _direction.X);
        _streamLength = _pattern.Type is BossPatternType.FireStream or BossPatternType.LaserSweep
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

        if (_pattern.Type == BossPatternType.LeapSlam)
        {
            // Just short of the player (landing on top would shove the two apart), no further than
            // the longest leap, and on open floor; the player is still well inside the slam.
            var leap = Math.Min(distance - boss.Radius - target.Radius, _pattern.Distance);
            _leapTo = arena.PlaceOnFloor(boss.Position + direction * leap, boss.Radius);
        }

        if (_pattern.Type == BossPatternType.MissileRain)
        {
            // The first missile on the player, the rest scattered round them.
            _missileTargets.Clear();
            _missileTargets.Add(arena.PlaceOnFloor(target.Position, _pattern.Radius * 0.5f));
            for (var i = 1; i < _pattern.Count; i++)
            {
                var angle = _random.Range(0f, MathHelper.TwoPi);
                var spot = target.Position + Direction(angle) * (_pattern.SpawnRadius * MathF.Sqrt(_random.NextFloat()));
                _missileTargets.Add(arena.PlaceOnFloor(spot, _pattern.Radius * 0.5f));
            }
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
                for (var i = 0; i < _summonPoints.Count && _enemies.Count < MaxAliveEnemies; i++)
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

            case BossPatternType.LeapSlam:
                {
                    _elapsed += deltaSeconds;
                    var t = Math.Min(1f, _elapsed / pattern.Seconds);
                    boss.Position = Vector2.Lerp(_origin, _leapTo, t);
                    boss.Elevation = MathF.Sin(MathF.PI * t) * LeapPeakHeight;
                    if (t >= 1f)
                    {
                        boss.Elevation = 0f;
                        HitPlayersAround(_leapTo, pattern.Radius, pattern.Damage, players, arena);
                        Particles?.Explosion(_leapTo, pattern.Radius * 0.7f);
                        Impact?.Invoke(_leapTo, pattern.Radius);
                        _audio.PlaySfx(SfxIds.Explosion);
                        done = true;
                    }

                    break;
                }

            case BossPatternType.MissileRain:
                _elapsed += deltaSeconds;
                if (_elapsed >= pattern.Seconds)
                {
                    for (var i = 0; i < _missileTargets.Count; i++)
                    {
                        HitPlayersAround(_missileTargets[i], pattern.Radius, pattern.Damage, players, arena);
                        Particles?.Explosion(_missileTargets[i], pattern.Radius);
                        Impact?.Invoke(_missileTargets[i], pattern.Radius);
                    }

                    _audio.PlaySfx(SfxIds.Explosion);
                    done = true;
                }

                break;

            case BossPatternType.FireballStream:
                {
                    // Follows whom it was aimed at, but only as fast as it can turn.
                    _elapsed += deltaSeconds;
                    var chased = _aimedAt is { IsAlive: true } ? _aimedAt : NearestLivingPlayer(boss.Position, players);
                    if (chased is not null)
                    {
                        var aim = MathF.Atan2(chased.Position.Y - boss.Position.Y, chased.Position.X - boss.Position.X);
                        var current = MathF.Atan2(_direction.Y, _direction.X);
                        var most = MathHelper.ToRadians(pattern.SpinDegreesPerSecond) * deltaSeconds;
                        _direction = Direction(current + Math.Clamp(MathHelper.WrapAngle(aim - current), -most, most));
                    }

                    boss.Facing = MathF.Atan2(_direction.Y, _direction.X);
                    _nextThrowIn -= deltaSeconds;
                    if (_nextThrowIn <= 0f && Fireballs is not null)
                    {
                        Fireballs.Fire(
                            boss.Position + _direction * boss.Radius, _direction, pattern.ProjectileSpeed,
                            pattern.ProjectileRadius, pattern.Damage, pattern.ProjectileRange);
                        _audio.PlaySfx(SfxIds.EnemyFireball);
                        _nextThrowIn += pattern.Interval;
                    }

                    done = _elapsed >= pattern.Seconds;
                    break;
                }

            case BossPatternType.Haste:
                _hasteLeft = pattern.Seconds;
                _hasteMultiplier = pattern.SpeedMultiplier;
                done = true;
                break;

            case BossPatternType.FireballVolley:
                _nextThrowIn -= deltaSeconds;
                if (_nextThrowIn <= 0f)
                {
                    ThrowFan(boss, pattern);
                    _burstsLeft--;
                    _nextThrowIn = pattern.Interval;
                }

                done = _burstsLeft <= 0;
                break;

            case BossPatternType.FireStream:
            case BossPatternType.LaserSweep:
                // Sweeps round from where it started; pillars and walls cut it short.
                _elapsed += deltaSeconds;
                var start = MathF.Atan2(_direction.Y, _direction.X);
                var turn = pattern.Type == BossPatternType.LaserSweep ? _sweepSign : 1f;
                var angle = start + MathHelper.ToRadians(pattern.SpinDegreesPerSecond) * turn * deltaSeconds;
                _direction = new Vector2(MathF.Cos(angle), MathF.Sin(angle));
                boss.Facing = angle;
                _streamLength = Math.Min(pattern.Distance, arena.Raycast(boss.Position, _direction, out _));
                BurnAlongStream(boss, players, pattern, deltaSeconds);
                EmitStreamEffects(boss, pattern, deltaSeconds);
                done = _elapsed >= pattern.Seconds;
                break;
        }

        if (done)
        {
            _state = State.Walking;
            _stateLeft = ActiveIdleSeconds;
            _queuedFollowUp = pattern.FollowUp;
        }
    }

    // Sparks and glints for a laser, flames for a fire stream, and a burst where either lands.
    private void EmitStreamEffects(Enemy boss, BossPatternConfig pattern, float deltaSeconds)
    {
        if (Particles is null)
        {
            return;
        }

        var height = boss.Radius * BeamHeightPerRadius;
        var end = boss.Position + _direction * _streamLength;
        if (pattern.Type == BossPatternType.LaserSweep)
        {
            Particles.AlongBeam(boss.Position, end, height, LaserCoreColor, LaserColor, deltaSeconds);
            Particles.BeamImpact(end, height, LaserCoreColor, LaserColor, deltaSeconds);
        }
        else
        {
            Particles.FireAlong(boss.Position, end, height, deltaSeconds);
            Particles.BeamImpact(end, height, FireCoreColor, FireColor, deltaSeconds);
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
            if (Vector2.DistanceSquared(boss.Position + _direction * along, player.Position) > reach * reach)
            {
                continue;
            }

            // The laser cuts: one heavy hit per sweep (a sweep crosses a player in a blink, so
            // damage per second would barely scratch). Fire burns for as long as you stand in it.
            if (pattern.Type == BossPatternType.LaserSweep)
            {
                if (!_hitThisAttack.Contains(player) && player.TakeDamage(pattern.Damage))
                {
                    _hitThisAttack.Add(player);
                }

                continue;
            }

            player.TakeDamage(pattern.Damage * deltaSeconds, silent: true);
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

    // Count fireballs fanned evenly across the spread around the locked aim.
    private void ThrowFan(Enemy boss, BossPatternConfig pattern)
    {
        if (Fireballs is null)
        {
            return;
        }

        var aim = MathF.Atan2(_direction.Y, _direction.X);
        var spread = MathHelper.ToRadians(pattern.SpreadDegrees);
        for (var i = 0; i < pattern.Count; i++)
        {
            var angle = pattern.Count > 1 ? aim + MathHelper.Lerp(-spread, spread, i / (pattern.Count - 1f)) : aim;
            var direction = Direction(angle);
            Fireballs.Fire(
                boss.Position + direction * boss.Radius, direction, pattern.ProjectileSpeed, pattern.ProjectileRadius,
                pattern.Damage, pattern.ProjectileRange);
        }

        _audio.PlaySfx(SfxIds.EnemyFireball);
    }

    // Everyone within reach of a landing or a blast, once each; dashing dodges it and a pillar
    // between the blast and a player shelters them.
    private static void HitPlayersAround(
        Vector2 center, float radius, float damage, IReadOnlyList<Player> players, Arena arena)
    {
        for (var i = 0; i < players.Count; i++)
        {
            var player = players[i];
            var reach = radius + player.Radius;
            if (player.IsAlive && Vector2.DistanceSquared(player.Position, center) <= reach * reach
                && arena.PillarsClear(center, player.Position))
            {
                player.TakeDamage(damage);
            }
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
