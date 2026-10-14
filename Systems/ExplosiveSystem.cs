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
    private const int InitialThrown = 32;
    private const int InitialEffects = 32;

    // Mini-bombs look smaller than the grenade they come from.
    private const float ClusterVisualScale = 0.6f;

    private static readonly Color ShadowColor = new(0, 0, 0, 90);

    private readonly Arena _arena;
    private readonly FriendlyFireConfig _friendlyFire;
    private readonly GameRandom _random;
    private readonly IAudioManager _audio;
    private readonly Pool<ThrownExplosive> _thrown = new(InitialThrown);
    private readonly Pool<ExplosionEffect> _effects = new(InitialEffects);
    private readonly List<IHittable> _targets = new();
    private readonly List<IHittableSource> _sources = new();
    private readonly List<IHittable> _inBlast = new();
    private readonly List<ExplosiveShot> _landed = new();

    public ExplosiveSystem(Arena arena, FriendlyFireConfig friendlyFire, GameRandom random, IAudioManager audio)
    {
        _arena = arena;
        _friendlyFire = friendlyFire;
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

    /// <summary>Sends an explosive on its way. It lands inside the arena even when aimed past a wall.</summary>
    public void Throw(in ExplosiveShot shot)
    {
        var landing = _arena.ClampInside(shot.Target, shot.VisualRadius);
        _thrown.Rent().Launch(shot with { Target = landing });
    }

    public void Update(float deltaSeconds, IReadOnlyList<Player> players)
    {
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
            Explode(_landed[i], players);
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
            renderer.DrawVisual(thrown.Visual, thrown.Position, 0f);
        }
    }

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
        }

        DamagePlayers(shot, players);

        _effects.Rent().Start(center, shot.BlastRadius);
        _audio.PlaySfx(SfxIds.Explosion);

        if (shot.ClusterCount > 0)
        {
            ScatterCluster(shot);
        }
    }

    // The thrower is hurt by a share of their own blast, the other player by the friendly-fire
    // share. Dash i-frames protect as usual because the damage goes through TakeDamage.
    private void DamagePlayers(in ExplosiveShot shot, IReadOnlyList<Player> players)
    {
        for (var i = 0; i < players.Count; i++)
        {
            var player = players[i];
            if (Vector2.Distance(player.Position, shot.Target) > shot.BlastRadius + player.Radius)
            {
                continue;
            }

            float share;
            if (player.Index == shot.OwnerIndex)
            {
                share = _friendlyFire.SelfExplosiveDamage;
            }
            else
            {
                share = _friendlyFire.Enabled ? _friendlyFire.DamageToOther : 0f;
            }

            if (share > 0f)
            {
                player.TakeDamage(shot.Damage * share);
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
