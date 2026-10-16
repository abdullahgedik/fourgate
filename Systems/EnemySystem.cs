using System;
using System.Collections.Generic;
using Fourgate.Core;
using Fourgate.Core.Audio;
using Fourgate.Core.Data;
using Fourgate.Core.Pooling;
using Fourgate.Core.Rendering;
using Fourgate.Core.Rendering.Models;
using Fourgate.Entities;
using Fourgate.Entities.Enemies;
using Microsoft.Xna.Framework;

namespace Fourgate.Systems;

/// <summary>Owns all enemies: movement toward players, keeping them apart, and removal on death.</summary>
public sealed class EnemySystem : IHittableSource
{
    // Ambling once the players are gone: slower than a chase, in short legs, sometimes standing still.
    private const float WanderSpeedShare = 0.3f;

    // How often every flow field is rebuilt even when its player stands still.
    private const float FieldRefreshSeconds = 0.5f;

    // A zombie killed by a single hit this big (a share of its full HP) bursts apart.
    private const float GibHitShare = 0.8f;
    private const float WanderMinSeconds = 1.2f;
    private const float WanderMaxSeconds = 3f;
    private const float WanderPauseChance = 0.3f;

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
    private readonly List<FlowField> _fields = new();
    private readonly List<int> _fieldCells = new();
    private readonly List<Vector2> _solids = new();
    private Arena? _fieldArena;
    private float _fieldRefreshLeft;
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

    /// <summary>Where visual particles go; null draws none (tests).</summary>
    public ParticleSystem? Particles { get; set; }

    /// <summary>Multiplies how fast zombies walk (Nightmare); bosses keep their own pace.</summary>
    public float SpeedMultiplier { get; set; } = 1f;

    /// <summary>Where ranged enemies' fireballs go; without it they wind up but throw nothing.</summary>
    public EnemyShotSystem? Fireballs { get; set; }

    /// <summary>
    /// When true, zombies with nobody alive to chase amble about aimlessly (after the players
    /// have died); otherwise they stand still.
    /// </summary>
    public bool WanderWhenAlone { get; set; }

    /// <summary>Share of an enemy's remaining knockback applied per second; 0 applies it at once.</summary>
    public float KnockbackDamping { get; set; }

    /// <summary>Grid spacing of the path finding: the barrel grid, so a barrel fills one cell.</summary>
    public float PathCellSize { get; set; } = 60f;

    /// <summary>Whom each enemy chases (co-op rules); the defaults until the game's are set.</summary>
    public EnemyTargeting Targeting { get; set; } = new(new EnemyAggroConfig());

    /// <summary>Lets enemies far from every player walk faster; null for none.</summary>
    public CatchUpConfig? CatchUp { get; set; }

    public void Update(float deltaSeconds, IReadOnlyList<Player> players, Arena arena)
    {
        RemoveDead();
        UpdateFlowFields(deltaSeconds, players, arena);
        Targeting.ClearChasers();
        for (var i = 0; i < _enemies.Count; i++)
        {
            Targeting.CountChaser(_enemies[i]);
        }

        for (var i = 0; i < _enemies.Count; i++)
        {
            var enemy = _enemies[i];
            enemy.Tick(deltaSeconds);
            if (enemy.IsBurning)
            {
                Particles?.Burning(enemy.Position, enemy.Radius, deltaSeconds);
            }

            if (enemy.ConsumeHit())
            {
                _audio.PlaySfx(SfxIds.EnemyHit);
            }

            // A boss is moved by the boss system.
            if (!enemy.Config.IsBoss)
            {
                var target = Targeting.Choose(enemy, players, deltaSeconds);
                if (!UpdateRangedAttack(enemy, target, arena, deltaSeconds))
                {
                    MoveTowardTarget(enemy, target, players, arena, deltaSeconds);
                }
            }

            enemy.Position += enemy.TakeKnockStep(
                KnockbackDamping > 0f ? Math.Min(1f, KnockbackDamping * deltaSeconds) : 1f);
        }

        RebuildHash();
        for (var i = 0; i < _enemies.Count; i++)
        {
            var enemy = _enemies[i];

            // A body in the air (a leaping boss) neither collides nor hits by touch.
            if (enemy.Elevation > 0f)
            {
                continue;
            }

            Separate(enemy);
            PushOutOfPlayers(enemy, players);
            enemy.Position = arena.ResolveMovement(enemy.Position, enemy.Radius);
            AttackTouchingPlayers(enemy, players);
            AttackTouchingObstacles(enemy, arena);
            SpreadFire(enemy);
        }

        for (var i = 0; i < _enemies.Count; i++)
        {
            _enemies[i].TrackWalk(deltaSeconds);
        }

        // Second rebuild so shots fired this frame are tested against final positions.
        RebuildHash();
    }

    /// <summary>The living boss in the arena, or null.</summary>
    public Enemy? FindBoss()
    {
        for (var i = 0; i < _enemies.Count; i++)
        {
            if (_enemies[i].Config.IsBoss && _enemies[i].IsAlive)
            {
                return _enemies[i];
            }
        }

        return null;
    }

    /// <summary>Glow of enemies winding up a ranged attack; drawn under the enemies.</summary>
    public void DrawTelegraphs(IRenderer renderer)
    {
        for (var i = 0; i < _enemies.Count; i++)
        {
            var enemy = _enemies[i];
            if (!enemy.IsWindingUp || enemy.CurrentAttack is null)
            {
                continue;
            }

            // A red glow that swells and brightens as the throw nears; in 3D a halo round the body.
            var progress = enemy.WindUpProgress;
            var radius = enemy.Radius * MathHelper.Lerp(TelegraphGlowStart, TelegraphGlowEnd, progress);
            renderer.FillCircleRaised(
                enemy.Position, Depth3D.BodyHeight(enemy.Radius) * 0.5f, radius, TelegraphColor * (0.3f + 0.5f * progress));
        }
    }

    public void Draw(IRenderer renderer)
    {
        for (var i = 0; i < _enemies.Count; i++)
        {
            var enemy = _enemies[i];
            if (enemy.Config.Model is { } model)
            {
                // A boss poses from its attack (drawing back, then carrying it out); others from
                // their own wind-up and lunge.
                var visual = enemy.Visual;
                var boss = enemy.Config.IsBoss;
                var glow = enemy.ModelGlow(out var glowColor);
                renderer.DrawModel(
                    new ModelPose(
                        model, enemy.Position, enemy.Facing, enemy.Facing, enemy.WalkDistance, enemy.Speed, enemy.BodyColor,
                        Lunge: boss ? Math.Max(enemy.BossAction, enemy.LungeAmount) : enemy.LungeAmount,
                        Flinch: enemy.FlinchAmount, ShadowRadius: enemy.Radius,
                        MoveHeading: enemy.MoveHeading,
                        Windup: boss ? enemy.BossWindup : enemy.WindupAmount,
                        Elevation: enemy.Elevation, Glow: glow, GlowColor: glowColor),
                    visual);
            }
            else if (enemy.Elevation > 0f)
            {
                renderer.DrawVisualElevated(enemy.Visual, enemy.Position, enemy.Facing, enemy.Elevation);
            }
            else
            {
                renderer.DrawVisual(enemy.Visual, enemy.Position, enemy.Facing);
            }
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
                var gibbed = !enemy.Config.IsBoss && enemy.LastHitDamage >= enemy.MaxHp * GibHitShare;
                _deaths.Add(new EnemyDeath(enemy.Config, enemy.Position, enemy.LastHitBy, enemy.Facing, gibbed));
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
            _hash.Insert(enemy, enemy.Position, enemy.HitReach);
        }
    }

    // A ranged enemy stops to attack when its target is in range, in clear sight past the
    // pillars, and able to see it (so nobody is shot from off-screen). Returns true while it
    // holds still: winding up, or waiting out its cooldown in a good spot.
    private bool UpdateRangedAttack(Enemy enemy, Player? target, Arena arena, float deltaSeconds)
    {
        var attack = enemy.CurrentAttack;
        if (attack is null || !enemy.IsAlive)
        {
            return false;
        }

        // The rest of a stream goes out along the same locked aim.
        if (enemy.IsFiringBursts)
        {
            if (enemy.TickBurst(deltaSeconds, attack.BurstInterval))
            {
                ThrowVolley(enemy, attack);
                enemy.PlayAttackMotion();
                _audio.PlaySfx(SfxIds.EnemyFireball);
            }

            if (!enemy.IsFiringBursts)
            {
                enemy.SwitchAttack();
            }

            return true;
        }

        if (enemy.IsWindingUp)
        {
            if (enemy.TickWindUp(deltaSeconds, attack.CooldownSeconds))
            {
                ThrowVolley(enemy, attack);
                enemy.PlayAttackMotion();
                _audio.PlaySfx(SfxIds.EnemyFireball);
                if (attack.Bursts > 1)
                {
                    enemy.StartBursts(attack.Bursts - 1, attack.BurstInterval);
                }
                else
                {
                    enemy.SwitchAttack();
                }
            }

            return true;
        }

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

    private void MoveTowardTarget(
        Enemy enemy, Player? target, IReadOnlyList<Player> players, Arena arena, float deltaSeconds)
    {
        if (target is null)
        {
            if (WanderWhenAlone)
            {
                Wander(enemy, deltaSeconds);
            }

            return;
        }

        var speed = enemy.Config.Speed * SpeedMultiplier;
        if (CatchUp is not null)
        {
            // Off screen means far from every player, not just from its own target.
            var nearest = NearestLivingPlayer(enemy.Position, players);
            speed *= nearest is null ? 1f : CatchUp.MultiplierAt(Vector2.Distance(enemy.Position, nearest.Position));
        }

        // Something in the way: follow the target's flow field round pillars and barrels.
        var field = FieldFor(target);
        if (field is not null && !field.LineClear(enemy.Position, target.Position)
            && field.TryGetDirection(enemy.Position, out var way))
        {
            enemy.Facing = MathF.Atan2(way.Y, way.X);
            enemy.Position += way * (speed * deltaSeconds);
            return;
        }

        // Off the grid (or no field): head for the corner of a pillar in the way first.
        var waypoint = arena.NextWaypoint(enemy.Position, target.Position, enemy.Radius);
        if (waypoint != target.Position)
        {
            var toCorner = waypoint - enemy.Position;
            var cornerDistance = toCorner.Length();
            if (cornerDistance > 0.0001f)
            {
                enemy.Facing = MathF.Atan2(toCorner.Y, toCorner.X);
                enemy.Position += toCorner / cornerDistance * Math.Min(speed * deltaSeconds, cornerDistance);
            }

            return;
        }

        // Walk up to the player and stop when touching.
        var distance = Vector2.Distance(enemy.Position, target.Position);
        var gap = distance - (enemy.Radius + target.Radius);
        if (gap <= 0f)
        {
            return;
        }

        var step = Math.Min(speed * deltaSeconds, gap);
        enemy.Facing = MathF.Atan2(target.Position.Y - enemy.Position.Y, target.Position.X - enemy.Position.X);
        enemy.Position += (target.Position - enemy.Position) / distance * step;
    }

    /// <summary>The way to a player for anything chasing them; null before the first update or off the grid.</summary>
    public FlowField? FieldFor(Player player)
    {
        return player.Index < _fields.Count && _fields[player.Index].TargetCell >= 0 ? _fields[player.Index] : null;
    }

    // One field per player, rebuilt when the player steps into another cell and every so often
    // anyway (barrels come and go).
    private void UpdateFlowFields(float deltaSeconds, IReadOnlyList<Player> players, Arena arena)
    {
        if (!ReferenceEquals(arena, _fieldArena))
        {
            _fieldArena = arena;
            _fields.Clear();
            _fieldCells.Clear();
        }

        _fieldRefreshLeft -= deltaSeconds;
        var refreshAll = _fieldRefreshLeft <= 0f;
        if (refreshAll)
        {
            _fieldRefreshLeft = FieldRefreshSeconds;
            _solids.Clear();
            arena.Obstacles?.CollectSolids(_solids);
        }

        for (var i = 0; i < players.Count; i++)
        {
            var player = players[i];
            while (_fields.Count <= player.Index)
            {
                _fields.Add(new FlowField(arena, PathCellSize));
                _fieldCells.Add(-1);
            }

            var field = _fields[player.Index];
            var cell = field.CellAt(player.Position);
            if (refreshAll || cell != _fieldCells[player.Index])
            {
                if (!refreshAll)
                {
                    _solids.Clear();
                    arena.Obstacles?.CollectSolids(_solids);
                }

                field.Build(player.Position, _solids);
                _fieldCells[player.Index] = cell;
            }
        }
    }

    // Slow ambles in random directions, now and then standing still a while.
    private void Wander(Enemy enemy, float deltaSeconds)
    {
        enemy.WanderLeft -= deltaSeconds;
        if (enemy.WanderLeft <= 0f)
        {
            var roll = Hash01(enemy.Id * 7919 + enemy.WanderLegs * 104729);
            var angle = Hash01(enemy.Id * 31337 + enemy.WanderLegs * 2741) * MathHelper.TwoPi;
            enemy.WanderLegs++;
            enemy.WanderHeading = roll < WanderPauseChance
                ? Vector2.Zero
                : new Vector2(MathF.Cos(angle), MathF.Sin(angle));
            enemy.WanderLeft = MathHelper.Lerp(WanderMinSeconds, WanderMaxSeconds, roll);
        }

        if (enemy.WanderHeading == Vector2.Zero)
        {
            return;
        }

        enemy.Facing = MathF.Atan2(enemy.WanderHeading.Y, enemy.WanderHeading.X);
        enemy.Position += enemy.WanderHeading * (enemy.Config.Speed * WanderSpeedShare * deltaSeconds);
    }

    // A repeatable 0–1 value from a whole number, so wandering needs no shared random source.
    private static float Hash01(int seed)
    {
        unchecked
        {
            var x = (uint)seed * 2654435761u;
            x ^= x >> 15;
            x *= 2246822519u;
            x ^= x >> 13;
            return (x & 0xFFFFu) / 65536f;
        }
    }

    /// <summary>
    /// Enemy bodies are solid to a walking player: one who walked into a body is put back just
    /// touching it, and the body does not move. A dashing player passes through.
    /// </summary>
    public void BlockPlayer(Player player, Arena arena)
    {
        if (!player.IsAlive || player.IsDashing)
        {
            return;
        }

        for (var i = 0; i < _enemies.Count; i++)
        {
            var enemy = _enemies[i];
            if (!enemy.IsAlive || enemy.Elevation > 0f)
            {
                continue;
            }

            var away = player.Position - enemy.Position;
            var reach = enemy.Radius + player.Radius;
            var distanceSquared = away.LengthSquared();
            if (distanceSquared >= reach * reach)
            {
                continue;
            }

            var distance = MathF.Sqrt(distanceSquared);
            var direction = distance > 0.001f ? away / distance : Vector2.UnitX;
            player.PushOut(direction * (reach - distance), arena);
        }
    }

    // Each overlapping pair is seen once from each side and each side moves half the
    // overlap, so together they end up just touching. Against a boss the other side moves
    // the whole overlap.
    private void Separate(Enemy enemy)
    {
        // A crowd never shoves a boss around; the zombies make way instead.
        if (enemy.Config.IsBoss)
        {
            return;
        }

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
            push += direction * (overlap * (other.Config.IsBoss ? 1f : 0.5f));
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
            if (!player.IsAlive || player.IsDashing)
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
