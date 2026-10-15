using System;
using System.Collections.Generic;
using Fourgate.Core.Audio;
using Fourgate.Core.Data;
using Fourgate.Core.Pooling;
using Fourgate.Core.Rendering;
using Fourgate.Core.Rng;
using Fourgate.Entities;
using Fourgate.Entities.Effects;
using Fourgate.Entities.Projectiles;
using Fourgate.Weapons;
using Microsoft.Xna.Framework;

namespace Fourgate.Systems;

/// <summary>Owns thrown explosives in flight and resolves their blasts against enemies and players.</summary>
public sealed class ExplosiveSystem
{
    // How high a throw goes in the 3D view (2D shows the arc by size instead).
    private const float ThrowPeakHeight = 140f;

    private const int InitialThrown = 32;
    private const int InitialEffects = 32;

    // Mini-bombs look smaller than the grenade they come from.
    private const float ClusterVisualScale = 0.6f;

    private static readonly Color ShadowColor = new(0, 0, 0, 90);

    private readonly Arena _arena;
    private readonly FriendlyFireRules _friendlyFire;
    private readonly GameRandom _random;
    private readonly IAudioManager _audio;
    private readonly Pool<ThrownExplosive> _thrown = new(InitialThrown);
    private readonly Pool<ExplosionEffect> _effects = new(InitialEffects);
    private readonly List<IHittable> _targets = new();
    private readonly List<IHittableSource> _sources = new();
    private readonly List<IHittable> _inBlast = new();
    private readonly List<ExplosiveShot> _landed = new();
    private readonly List<QueuedBlast> _queued = new();
    private readonly List<int> _ringOrder = new();

    private readonly HazardSystem? _hazards;

    /// <param name="arena">Keeps landings inside the walls.</param>
    /// <param name="friendlyFire">Self and friendly damage shares.</param>
    /// <param name="random">Cluster scatter.</param>
    /// <param name="audio">Sound events.</param>
    /// <param name="hazards">Receives the fire pools of fire weapons; without it they leave none.</param>
    public ExplosiveSystem(
        Arena arena, FriendlyFireConfig friendlyFire, GameRandom random, IAudioManager audio, HazardSystem? hazards = null)
    {
        _hazards = hazards;
        _arena = arena;
        _friendlyFire = new FriendlyFireRules(friendlyFire);
        _random = random;
        _audio = audio;
    }

    /// <summary>Explosives still in the air.</summary>
    public int InFlightCount => _thrown.Count;

    /// <summary>Debug switch: every blast kills what it reaches.</summary>
    public bool OneShotKills { get; set; }

    /// <summary>Registers a single thing blasts can hit.</summary>
    public void AddTarget(IHittable target)
    {
        _targets.Add(target);
    }

    /// <summary>Registers a searchable group of things blasts can hit (the enemies).</summary>
    public void AddSource(IHittableSource source)
    {
        _sources.Add(source);
    }

    /// <summary>
    /// Sends an explosive on its way. It flies over pillars but lands on open floor, even when
    /// aimed past a wall or onto a pillar.
    /// </summary>
    public void Throw(in ExplosiveShot shot)
    {
        var landing = _arena.PlaceOnFloor(shot.Target, shot.VisualRadius);
        _thrown.Rent().Launch(shot with { Target = landing });
    }

    /// <summary>Blasts waiting to go off: rocket impacts and the rings that follow them.</summary>
    public int QueuedBlastCount => _queued.Count;

    /// <summary>
    /// A rocket's blast, resolved on the next update (which knows the players); a ring of
    /// smaller blasts follows when the payload has one, going off one by one in a random order.
    /// </summary>
    public void QueueRocketBlast(Vector2 center, float damage, in RocketPayload payload, int ownerIndex)
    {
        _queued.Add(new QueuedBlast(center, damage, payload.BlastRadius, ownerIndex, 0f));

        if (payload.RingCount <= 0)
        {
            return;
        }

        // Evenly spaced around the ring, but going off in a random order.
        _ringOrder.Clear();
        for (var i = 0; i < payload.RingCount; i++)
        {
            _ringOrder.Add(i);
        }

        for (var i = _ringOrder.Count - 1; i > 0; i--)
        {
            var swap = _random.NextIndex(i + 1);
            (_ringOrder[i], _ringOrder[swap]) = (_ringOrder[swap], _ringOrder[i]);
        }

        var step = MathF.Tau / payload.RingCount;
        var start = _random.Range(0f, step);
        for (var i = 0; i < payload.RingCount; i++)
        {
            var angle = start + step * _ringOrder[i];
            var offset = new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * payload.RingDistance;
            _queued.Add(new QueuedBlast(
                _arena.ClampInside(center + offset, 1f),
                damage * payload.RingDamageMult,
                payload.BlastRadius * payload.RingRadiusMult,
                ownerIndex,
                payload.RingDelay + payload.RingInterval * i));
        }
    }

    public void Update(float deltaSeconds, IReadOnlyList<Player> players)
    {
        UpdateQueuedBlasts(deltaSeconds, players);

        // Blasts are resolved after the loop so cluster bombs thrown by a blast do not
        // disturb the pool while it is being walked.
        _landed.Clear();
        for (var i = _thrown.Count - 1; i >= 0; i--)
        {
            var thrown = _thrown[i];
            thrown.Advance(deltaSeconds);
            if (thrown.HasLanded)
            {
                _landed.Add(thrown.Shot);
                _thrown.ReturnAt(i);
            }
        }

        for (var i = 0; i < _landed.Count; i++)
        {
            Land(_landed[i], players);
        }

        for (var i = _effects.Count - 1; i >= 0; i--)
        {
            var effect = _effects[i];
            effect.Tick(deltaSeconds);
            if (effect.IsFinished)
            {
                _effects.ReturnAt(i);
            }
        }
    }

    public void Draw(IRenderer renderer)
    {
        for (var i = 0; i < _effects.Count; i++)
        {
            var effect = _effects[i];
            renderer.DrawVisual(effect.Visual, effect.Position, 0f);
        }

        for (var i = 0; i < _thrown.Count; i++)
        {
            var thrown = _thrown[i];
            renderer.FillCircle(thrown.Position, thrown.Shot.VisualRadius, ShadowColor);
            var visual = thrown.Visual;
            renderer.FillCircleRaised(thrown.Position, ThrowPeakHeight * thrown.ArcHeight + visual.Radius, visual.Radius, visual.Color);
        }
    }

    /// <summary>An explosion right here and now (a mine going off), with the usual damage rules.</summary>
    public void Detonate(
        Vector2 center, float damage, float blastRadius, int ownerIndex, IReadOnlyList<Player> players)
    {
        Explode(new ExplosiveShot(center, center, 0f, 0f, damage, blastRadius, ownerIndex), players);
    }

    private void UpdateQueuedBlasts(float deltaSeconds, IReadOnlyList<Player> players)
    {
        for (var i = _queued.Count - 1; i >= 0; i--)
        {
            var blast = _queued[i];
            if (blast.SecondsLeft > 0f)
            {
                _queued[i] = blast with { SecondsLeft = blast.SecondsLeft - deltaSeconds };
                continue;
            }

            Detonate(blast.Center, blast.Damage, blast.Radius, blast.OwnerIndex, players);
            _queued.RemoveAt(i);
        }
    }

    private readonly record struct QueuedBlast(
        Vector2 Center, float Damage, float Radius, int OwnerIndex, float SecondsLeft);

    private void Land(in ExplosiveShot shot, IReadOnlyList<Player> players)
    {
        if (shot.BlastRadius > 0f)
        {
            Explode(shot, players);
        }

        if (shot.FireRadius > 0f && shot.FireSeconds > 0f)
        {
            _hazards?.SpawnFire(
                shot.Target, shot.FireRadius, shot.FireSeconds, shot.FireDamagePerSecond, shot.OwnerIndex,
                shot.IgniteSeconds);
            _audio.PlaySfx(SfxIds.FireStart);
        }
    }

    /// <summary>Where visual particles go; null draws none (tests).</summary>
    public ParticleSystem? Particles { get; set; }

    /// <summary>Called for every explosion with its center and radius (screen shake).</summary>
    public Action<Vector2, float>? Exploded { get; set; }

    /// <summary>World units a blast pushes enemies away from its center.</summary>
    public float KnockbackDistance { get; set; }

    private void Explode(in ExplosiveShot shot, IReadOnlyList<Player> players)
    {
        var center = shot.Target;
        var damage = OneShotKills ? float.MaxValue : shot.Damage;

        _inBlast.Clear();
        for (var i = 0; i < _targets.Count; i++)
        {
            if (Vector2.Distance(_targets[i].Position, center) <= shot.BlastRadius + _targets[i].Radius)
            {
                _inBlast.Add(_targets[i]);
            }
        }

        for (var i = 0; i < _sources.Count; i++)
        {
            _sources[i].Collect(center, shot.BlastRadius, _inBlast);
        }

        for (var i = 0; i < _inBlast.Count; i++)
        {
            _inBlast[i].Hit(damage, shot.OwnerIndex);
            var away = _inBlast[i].Position - center;
            if (away.LengthSquared() > 0.0001f)
            {
                _inBlast[i].Knock(Vector2.Normalize(away) * KnockbackDistance);
            }
        }

        DamagePlayers(shot, players);

        _effects.Rent().Start(center, shot.BlastRadius);
        Particles?.Explosion(center, shot.BlastRadius);
        Exploded?.Invoke(center, shot.BlastRadius);
        _audio.PlaySfx(SfxIds.Explosion);

        if (shot.ClusterCount > 0)
        {
            ScatterCluster(shot);
        }
    }

    // Players are hurt by a capped share of their own and each other's blasts (see
    // FriendlyFireRules). Dash i-frames protect as usual because the damage goes through TakeDamage.
    private void DamagePlayers(in ExplosiveShot shot, IReadOnlyList<Player> players)
    {
        for (var i = 0; i < players.Count; i++)
        {
            var player = players[i];
            if (Vector2.Distance(player.Position, shot.Target) <= shot.BlastRadius + player.Radius)
            {
                _friendlyFire.ApplyExplosion(player, shot.OwnerIndex, shot.Damage);
            }
        }
    }

    // Mini-bombs fly out evenly around the blast, each with a little random turn so the
    // pattern does not look stamped.
    private void ScatterCluster(in ExplosiveShot shot)
    {
        var step = MathF.Tau / shot.ClusterCount;
        var start = _random.Range(0f, step);
        for (var i = 0; i < shot.ClusterCount; i++)
        {
            var angle = start + step * i + _random.Range(-step * 0.2f, step * 0.2f);
            var offset = new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * shot.ClusterDistance;

            Throw(new ExplosiveShot(
                shot.Target,
                shot.Target + offset,
                shot.Speed,
                shot.VisualRadius * ClusterVisualScale,
                shot.Damage * shot.ClusterDamageMult,
                shot.BlastRadius * shot.ClusterRadiusMult,
                shot.OwnerIndex));
        }
    }
}
