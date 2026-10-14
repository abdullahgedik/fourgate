using System;
using System.Collections.Generic;
using Fourgate.Core.Audio;
using Fourgate.Core.Pooling;
using Fourgate.Core.Rendering;
using Fourgate.Core.Rng;
using Fourgate.Entities;
using Fourgate.Entities.Hazards;
using Fourgate.Weapons;
using Microsoft.Xna.Framework;

namespace Fourgate.Systems;

/// <summary>Owns placed mines: arming, triggering by enemies, an optional per-player cap and chain detonation.</summary>
public sealed class MineSystem
{
    private const int InitialMines = 32;

    private readonly ExplosiveSystem _explosives;
    private readonly IAudioManager _audio;
    private readonly Pool<Mine> _mines = new(InitialMines);
    private readonly List<IHittable> _targets = new();
    private readonly List<IHittableSource> _sources = new();
    private readonly List<IHittable> _nearby = new();
    private readonly List<Mine> _detonating = new();
    private readonly List<FollowUpBlast> _followUps = new();
    private readonly GameRandom _random;
    private long _placedCount;

    public MineSystem(ExplosiveSystem explosives, GameRandom random, IAudioManager audio)
    {
        _random = random;
        _explosives = explosives;
        _audio = audio;
    }

    public int Count => _mines.Count;

    public Mine this[int index] => _mines[index];

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

    /// <summary>
    /// Puts a mine down. When the mine has a cap and the owner is at it, their oldest mine of
    /// that kind disappears.
    /// </summary>
    public void Place(in MineShot shot)
    {
        if (shot.MaxPlaced > 0 && CountOwned(shot.OwnerIndex, shot.WeaponId) >= shot.MaxPlaced)
        {
            RemoveOldest(shot.OwnerIndex, shot.WeaponId);
        }

        _mines.Rent().Place(shot, _placedCount++);
        _audio.PlaySfx(SfxIds.MinePlaced);
    }

    /// <summary>Explosions still to come from mines that went off with several blasts.</summary>
    public int PendingBlastCount => _followUps.Count;

    public void Update(float deltaSeconds, IReadOnlyList<Player> players)
    {
        UpdateFollowUps(deltaSeconds, players);

        _detonating.Clear();
        for (var i = 0; i < _mines.Count; i++)
        {
            var mine = _mines[i];
            mine.Tick(deltaSeconds);
            if (mine.IsArmed && IsTriggered(mine))
            {
                mine.IsDetonating = true;
                _detonating.Add(mine);
            }
        }

        // The list grows while it is walked when mines set each other off.
        for (var i = 0; i < _detonating.Count; i++)
        {
            var mine = _detonating[i];
            var shot = mine.Shot;
            _explosives.Detonate(shot.Position, shot.Damage, shot.BlastRadius, shot.OwnerIndex, players);
            QueueFollowUps(shot);

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
            renderer.DrawVisual(mine.BodyVisual, mine.Position, 0f);
            renderer.DrawVisual(mine.LightVisual, mine.Position, 0f);
        }
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

    private bool IsTriggered(Mine mine)
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

    // Every mine in the blast goes off too, armed or not.
    private void SetOffMinesInBlast(Mine source)
    {
        var reach = source.Shot.BlastRadius;
        for (var i = 0; i < _mines.Count; i++)
        {
            var other = _mines[i];
            if (!other.IsDetonating && Vector2.Distance(other.Position, source.Position) <= reach)
            {
                other.IsDetonating = true;
                _detonating.Add(other);
            }
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

    private readonly record struct FollowUpBlast(
        Vector2 Position, float Damage, float BlastRadius, int OwnerIndex, float SecondsLeft);

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
}
