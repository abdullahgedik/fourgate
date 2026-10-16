using System;
using System.Collections.Generic;
using Fourgate.Core;
using Fourgate.Core.Audio;
using Fourgate.Core.Pooling;
using Fourgate.Core.Rendering;
using Fourgate.Core.Rng;
using Fourgate.Entities;
using Fourgate.Entities.Hazards;
using Fourgate.Weapons;
using Microsoft.Xna.Framework;

namespace Fourgate.Systems;

/// <summary>
/// Owns placed explosives: mines (armed, then set off by enemies coming close) and barrels
/// (set off by players hitting them), with an optional per-player cap, chain detonation,
/// multi-blasts and fire pools.
/// </summary>
public sealed class MineSystem : IHittableSource, IObstacleSource
{
    private const int InitialMines = 32;

    // A mine is a flat puck this tall.
    private const float MineHeight = 10f;

    // How high a thrown mine's arc goes.
    private const float MineThrowPeakHeight = 110f;

    private readonly ExplosiveSystem _explosives;
    private readonly HazardSystem? _hazards;
    private readonly GameRandom _random;
    private readonly IAudioManager _audio;
    private readonly Pool<Mine> _mines = new(InitialMines);
    private readonly List<IHittable> _targets = new();
    private readonly List<IHittableSource> _sources = new();
    private readonly List<IHittable> _nearby = new();
    private readonly List<Mine> _detonating = new();
    private readonly List<FollowUpBlast> _followUps = new();
    private long _placedCount;
    private bool _chainingBlast;

    // Each fuse in a chain burns this much longer or shorter at random, so a ripple looks natural.
    private const float ChainDelayJitter = 0.25f;
    private IReadOnlyList<Player> _players = Array.Empty<Player>();
    private Arena? _arena;

    public MineSystem(ExplosiveSystem explosives, GameRandom random, IAudioManager audio, HazardSystem? hazards = null)
    {
        _explosives = explosives;
        _random = random;
        _audio = audio;
        _hazards = hazards;
    }

    public int Count => _mines.Count;

    /// <summary>The arena, for keeping gridded barrels inside the walls.</summary>
    public Arena? Arena
    {
        get => _arena;
        set => _arena = value;
    }

    public Mine this[int index] => _mines[index];

    /// <summary>Explosions still to come from mines that went off with several blasts.</summary>
    public int PendingBlastCount => _followUps.Count;

    /// <summary>Registers a single thing that sets mines off.</summary>
    public void AddTarget(IHittable target)
    {
        _targets.Add(target);
    }

    /// <summary>Registers a searchable group of things that set mines off (the enemies).</summary>
    public void AddSource(IHittableSource source)
    {
        _sources.Add(source);
    }

    /// <summary>The barrels near a point: any damage from any weapon or equipment sets them off.</summary>
    public void Collect(Vector2 center, float radius, List<IHittable> results)
    {
        // Inside a delayed chain's own blast, the chain lights the fuses instead.
        if (_chainingBlast)
        {
            return;
        }

        for (var i = 0; i < _mines.Count; i++)
        {
            var mine = _mines[i];
            if (mine.Shot.Shootable && !mine.IsDetonating
                && Vector2.Distance(mine.Position, center) <= radius + mine.Radius)
            {
                results.Add(mine);
            }
        }
    }

    /// <summary>
    /// Puts a mine or barrel down. A solid one snaps to the grid cell it was aimed at and is
    /// refused (returns false) when that cell is taken, outside the walls, or overlaps a player.
    /// When it has a cap and the owner is at it, their oldest of that kind disappears.
    /// </summary>
    public bool Place(in MineShot shot)
    {
        var placed = shot;
        if (shot.Solid && shot.GridSize > 0f)
        {
            if (!TryFindFreeCell(shot, out var cell))
            {
                return false;
            }

            placed = shot with { Position = cell };
        }

        if (placed.MaxPlaced > 0 && CountOwned(placed.OwnerIndex, placed.WeaponId) >= placed.MaxPlaced)
        {
            RemoveOldest(placed.OwnerIndex, placed.WeaponId);
        }

        if (placed.ThrowSpeed > 0f && _arena is not null)
        {
            // A thrown one lands on open floor, never inside a pillar or past a wall.
            placed = placed with { Position = _arena.PlaceOnFloor(placed.Position, Mine.VisualRadius) };
        }

        _mines.Rent().Place(placed, _placedCount++);
        if (placed.ThrowSpeed <= 0f)
        {
            _audio.PlaySfx(SfxIds.MinePlaced);
        }

        return true;
    }

    public bool StrikeTouching(Vector2 position, float radius, float slack)
    {
        for (var i = 0; i < _mines.Count; i++)
        {
            var mine = _mines[i];
            if (!mine.CanBeStruck)
            {
                continue;
            }

            var pushed = Collision2D.PushOutOfBox(position, radius + slack, mine.Position, new Vector2(mine.Radius));
            if (pushed != position)
            {
                mine.Strike();
                _audio.PlaySfx(SfxIds.BarrelStruck);
                return true;
            }
        }

        return false;
    }

    public void CollectSolids(List<Vector2> centers)
    {
        for (var i = 0; i < _mines.Count; i++)
        {
            if (_mines[i].Shot.Solid && !_mines[i].IsDetonating)
            {
                centers.Add(_mines[i].Position);
            }
        }
    }

    public Vector2 PushOut(Vector2 position, float radius)
    {
        for (var i = 0; i < _mines.Count; i++)
        {
            var mine = _mines[i];
            if (mine.Shot.Solid && !mine.IsDetonating)
            {
                position = Collision2D.PushOutOfBox(position, radius, mine.Position, new Vector2(mine.Radius));
            }
        }

        return position;
    }

    private bool TryFindFreeCell(in MineShot shot, out Vector2 cell)
    {
        var size = shot.GridSize;
        var originX = _arena?.Left ?? 0f;
        var originY = _arena?.Top ?? 0f;
        var column = MathF.Floor((shot.Position.X - originX) / size);
        var row = MathF.Floor((shot.Position.Y - originY) / size);
        cell = new Vector2(originX + (column + 0.5f) * size, originY + (row + 0.5f) * size);

        var half = size / 2f;
        if (_arena is not null
            && (cell.X - half < _arena.Left || cell.X + half > _arena.Right
                || cell.Y - half < _arena.Top || cell.Y + half > _arena.Bottom
                || _arena.OverlapsPillar(cell, new Vector2(half))))
        {
            return false;
        }

        for (var i = 0; i < _mines.Count; i++)
        {
            if (_mines[i].Shot.Solid && Vector2.DistanceSquared(_mines[i].Position, cell) < 1f)
            {
                return false;
            }
        }

        // Never wall a player in, or drop a barrel on top of one.
        var barrelHalf = size / 2f;
        for (var i = 0; i < _players.Count; i++)
        {
            var player = _players[i];
            if (!player.IsAlive)
            {
                continue;
            }

            var dx = Math.Max(Math.Abs(player.Position.X - cell.X) - barrelHalf, 0f);
            var dy = Math.Max(Math.Abs(player.Position.Y - cell.Y) - barrelHalf, 0f);
            if (dx * dx + dy * dy < player.Radius * player.Radius)
            {
                return false;
            }
        }

        return true;
    }

    public void Update(float deltaSeconds, IReadOnlyList<Player> players)
    {
        _players = players;
        UpdateFollowUps(deltaSeconds, players);

        _detonating.Clear();
        for (var i = 0; i < _mines.Count; i++)
        {
            var mine = _mines[i];
            mine.Tick(deltaSeconds);
            if (mine.JustLanded)
            {
                _audio.PlaySfx(SfxIds.MinePlaced);
            }

            if (mine.IsTriggered || (mine.IsArmed && mine.Shot.TriggerRadius > 0f && IsNearEnemy(mine)))
            {
                mine.IsDetonating = true;
                _detonating.Add(mine);
            }
        }

        // The list grows while it is walked when explosives set each other off.
        for (var i = 0; i < _detonating.Count; i++)
        {
            var mine = _detonating[i];
            var shot = mine.Shot;

            _chainingBlast = shot.ChainDetonation && shot.ChainDelay > 0f;
            _explosives.Detonate(shot.Position, shot.Damage, shot.BlastRadius, shot.OwnerIndex, players);
            _chainingBlast = false;

            QueueFollowUps(shot);
            LeaveFire(shot);

            if (shot.ChainDetonation)
            {
                SetOffMinesInBlast(mine);
            }
        }

        for (var i = _mines.Count - 1; i >= 0; i--)
        {
            if (_mines[i].IsDetonating)
            {
                _mines.ReturnAt(i);
            }
        }
    }

    public void Draw(IRenderer renderer)
    {
        for (var i = 0; i < _mines.Count; i++)
        {
            var mine = _mines[i];
            var body = mine.BodyVisual;
            if (mine.Shot.Shootable)
            {
                renderer.DrawBarrel(mine.Position, body.Radius, Depth3D.BodyHeight(body.Radius), body.Color, Mine.BandColor);
                continue;
            }

            if (mine.IsFlying)
            {
                DrawFlying(renderer, mine);
                continue;
            }

            // A mine lies flat on the floor, its light on top.
            renderer.DrawLowBody(body, mine.Position, MineHeight);
            renderer.DrawVisualRaised(mine.LightVisual, mine.Position, 0f, MineHeight + 0.5f);
        }
    }

    // A thrown mine on its arc, with its shadow on the floor.
    private static void DrawFlying(IRenderer renderer, Mine mine)
    {
        var body = mine.BodyVisual;
        var height = MineThrowPeakHeight * mine.ArcHeight;
        renderer.DrawShadow(mine.Position, height, body.Radius);
        renderer.DrawVisualRaised(body, mine.Position, 0f, height + MineHeight);
        renderer.DrawVisualRaised(mine.LightVisual, mine.Position, 0f, height + MineHeight + 0.5f);
    }

    private void LeaveFire(in MineShot shot)
    {
        if (_hazards is null || shot.FireRadius <= 0f || shot.FireSeconds <= 0f)
        {
            return;
        }

        _hazards.SpawnFire(
            shot.Position, shot.FireRadius, shot.FireSeconds, shot.FireDamagePerSecond, shot.OwnerIndex, 0f,
            shot.FireHurtsPlayers);
    }

    // Later blasts of a multi-blast mine go off in order, each a little off center so the
    // burst covers more ground and does not look stamped.
    private void QueueFollowUps(in MineShot shot)
    {
        for (var i = 1; i < shot.BlastCount; i++)
        {
            var angle = _random.Range(0f, MathHelper.TwoPi);
            var distance = _random.Range(0f, shot.BlastSpread);
            var offset = new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * distance;
            _followUps.Add(new FollowUpBlast(
                shot.Position + offset, shot.Damage, shot.BlastRadius, shot.OwnerIndex, shot.BlastInterval * i));
        }
    }

    private void UpdateFollowUps(float deltaSeconds, IReadOnlyList<Player> players)
    {
        for (var i = _followUps.Count - 1; i >= 0; i--)
        {
            var blast = _followUps[i];
            var secondsLeft = blast.SecondsLeft - deltaSeconds;
            if (secondsLeft > 0f)
            {
                _followUps[i] = blast with { SecondsLeft = secondsLeft };
                continue;
            }

            _explosives.Detonate(blast.Position, blast.Damage, blast.BlastRadius, blast.OwnerIndex, players);
            _followUps.RemoveAt(i);
        }
    }

    private bool IsNearEnemy(Mine mine)
    {
        var reach = mine.Shot.TriggerRadius;
        for (var i = 0; i < _targets.Count; i++)
        {
            if (Vector2.Distance(_targets[i].Position, mine.Position) <= reach + _targets[i].Radius)
            {
                return true;
            }
        }

        _nearby.Clear();
        for (var i = 0; i < _sources.Count; i++)
        {
            _sources[i].Collect(mine.Position, reach, _nearby);
        }

        return _nearby.Count > 0;
    }

    // Every mine or barrel in the blast goes off too, armed or not.
    private void SetOffMinesInBlast(Mine source)
    {
        var reach = source.Shot.BlastRadius;
        for (var i = 0; i < _mines.Count; i++)
        {
            var other = _mines[i];
            if (other.IsDetonating || other.IsFlying || Vector2.Distance(other.Position, source.Position) > reach)
            {
                continue;
            }

            var delay = source.Shot.ChainDelay;
            if (delay > 0f)
            {
                other.LightFuse(delay * _random.Range(1f - ChainDelayJitter, 1f + ChainDelayJitter));
                continue;
            }

            other.IsDetonating = true;
            _detonating.Add(other);
        }
    }

    private int CountOwned(int ownerIndex, string weaponId)
    {
        var count = 0;
        for (var i = 0; i < _mines.Count; i++)
        {
            if (_mines[i].Shot.OwnerIndex == ownerIndex && _mines[i].Shot.WeaponId == weaponId)
            {
                count++;
            }
        }

        return count;
    }

    private void RemoveOldest(int ownerIndex, string weaponId)
    {
        var oldest = -1;
        for (var i = 0; i < _mines.Count; i++)
        {
            var shot = _mines[i].Shot;
            if (shot.OwnerIndex == ownerIndex && shot.WeaponId == weaponId
                && (oldest < 0 || _mines[i].PlacedOrder < _mines[oldest].PlacedOrder))
            {
                oldest = i;
            }
        }

        if (oldest >= 0)
        {
            _mines.ReturnAt(oldest);
        }
    }

    private readonly record struct FollowUpBlast(
        Vector2 Position, float Damage, float BlastRadius, int OwnerIndex, float SecondsLeft);
}
