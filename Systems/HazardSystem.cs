using System;
using System.Collections.Generic;
using Fourgate.Core.Data;
using Fourgate.Core.Pooling;
using Fourgate.Core.Rendering;
using Fourgate.Entities;
using Fourgate.Entities.Hazards;
using Microsoft.Xna.Framework;

namespace Fourgate.Systems;

/// <summary>Owns hazards on the floor (fire pools) and applies their damage over time.</summary>
public sealed class HazardSystem
{
    private const int InitialPools = 16;

    private readonly FriendlyFireRules _friendlyFire;
    private readonly Pool<FirePool> _pools = new(InitialPools);
    private readonly List<IHittable> _targets = new();
    private readonly List<IHittableSource> _sources = new();
    private readonly List<IHittable> _inFire = new();

    public HazardSystem(FriendlyFireConfig friendlyFire)
    {
        _friendlyFire = new FriendlyFireRules(friendlyFire);
    }

    public int FirePoolCount => _pools.Count;

    /// <summary>Registers a single thing fire can burn.</summary>
    public void AddTarget(IHittable target)
    {
        _targets.Add(target);
    }

    /// <summary>Registers a searchable group of things fire can burn (the enemies).</summary>
    public void AddSource(IHittableSource source)
    {
        _sources.Add(source);
    }

    /// <param name="position">Center of the pool.</param>
    /// <param name="radius">Reach of the fire.</param>
    /// <param name="seconds">How long it burns.</param>
    /// <param name="damagePerSecond">Damage per second to everything inside.</param>
    /// <param name="ownerIndex">Index of the player who started it.</param>
    /// <param name="igniteSeconds">How long enemies keep burning after being caught; 0 for not at all.</param>
    /// <param name="hurtsPlayers">False for fire that burns enemies only.</param>
    public void SpawnFire(
        Vector2 position,
        float radius,
        float seconds,
        float damagePerSecond,
        int ownerIndex,
        float igniteSeconds,
        bool hurtsPlayers = true)
    {
        _pools.Rent().Start(position, radius, seconds, damagePerSecond, ownerIndex, igniteSeconds, hurtsPlayers);
    }

    /// <summary>Where visual particles go; null draws none (tests).</summary>
    public ParticleSystem? Particles { get; set; }

    public void Update(float deltaSeconds, IReadOnlyList<Player> players)
    {
        for (var i = _pools.Count - 1; i >= 0; i--)
        {
            var pool = _pools[i];

            // Damage only for the part of the frame the pool still exists.
            var burnSeconds = Math.Min(deltaSeconds, Math.Max(0f, pool.SecondsLeft));
            Burn(pool, burnSeconds, players);

            Particles?.FirePool(pool.Position, pool.Radius, pool.Fade, deltaSeconds);
            pool.Tick(deltaSeconds);
            if (pool.IsFinished)
            {
                _pools.ReturnAt(i);
            }
        }
    }

    /// <summary>Draws the pools; call before the things standing in them.</summary>
    public void Draw(IRenderer renderer)
    {
        for (var i = 0; i < _pools.Count; i++)
        {
            var pool = _pools[i];
            renderer.DrawVisual(pool.OuterVisual, pool.Position, 0f);
            renderer.DrawVisual(pool.InnerVisual, pool.Position, 0f);
        }
    }

    private void Burn(FirePool pool, float seconds, IReadOnlyList<Player> players)
    {
        if (seconds <= 0f)
        {
            return;
        }

        var damage = pool.DamagePerSecond * seconds;

        _inFire.Clear();
        for (var i = 0; i < _targets.Count; i++)
        {
            if (Vector2.Distance(_targets[i].Position, pool.Position) <= pool.Radius + _targets[i].Radius)
            {
                _inFire.Add(_targets[i]);
            }
        }

        for (var i = 0; i < _sources.Count; i++)
        {
            _sources[i].Collect(pool.Position, pool.Radius, _inFire);
        }

        for (var i = 0; i < _inFire.Count; i++)
        {
            _inFire[i].Burn(damage, pool.OwnerIndex);
            if (pool.IgniteSeconds > 0f)
            {
                _inFire[i].Ignite(pool.IgniteSeconds, pool.DamagePerSecond, pool.OwnerIndex);
            }
        }

        if (!pool.HurtsPlayers)
        {
            return;
        }

        // Players burn by the capped friendly-fire rules (see FriendlyFireRules).
        for (var i = 0; i < players.Count; i++)
        {
            var player = players[i];
            if (Vector2.Distance(player.Position, pool.Position) <= pool.Radius + player.Radius)
            {
                _friendlyFire.ApplyFire(player, pool.OwnerIndex, pool.DamagePerSecond, seconds);
            }
        }
    }
}
