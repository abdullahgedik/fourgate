using System;
using System.Collections.Generic;
using Fourgate.Core.Data;
using Fourgate.Core.Rng;
using Fourgate.Entities;
using Fourgate.Entities.Enemies;
using Microsoft.Xna.Framework;

namespace Fourgate.Systems;

/// <summary>
/// Whom a boss goes after in co-op. Threat builds from damage dealt and from staying close, and
/// fades. The boss commits to a target for a while, then turns to whoever has clearly more
/// threat. There is no announcement: players should stay ready for an attack at any time.
/// Without a config the boss simply chases the nearest player.
/// </summary>
public sealed class BossAggro
{
    private readonly BossAggroConfig? _config;
    private readonly GameRandom _random;
    private readonly List<float> _threat = new();
    private float _targetLeft;

    public BossAggro(BossAggroConfig? config, GameRandom random)
    {
        _config = config;
        _random = random;
    }

    /// <summary>Index of the player the boss is after, or -1.</summary>
    public int TargetIndex { get; private set; } = -1;

    /// <summary>A player's current threat on the boss (0 when unknown).</summary>
    public float GetThreat(int playerIndex)
    {
        return playerIndex >= 0 && playerIndex < _threat.Count ? _threat[playerIndex] : 0f;
    }

    /// <summary>Forgets everything (a new boss).</summary>
    public void Reset()
    {
        _threat.Clear();
        TargetIndex = -1;
        _targetLeft = 0f;
    }

    /// <summary>Updates threat and returns the boss's target, or null when nobody is alive.</summary>
    /// <param name="mayTurn">False while the boss is busy with an attack; it only changes target between attacks.</param>
    public Player? Update(Enemy boss, IReadOnlyList<Player> players, float deltaSeconds, bool mayTurn)
    {
        if (_config is null)
        {
            var nearest = Nearest(boss.Position, players);
            TargetIndex = nearest;
            return nearest >= 0 ? players[nearest] : null;
        }

        while (_threat.Count < players.Count)
        {
            _threat.Add(0f);
        }

        var keep = Math.Max(0f, 1f - _config.ThreatDecayPerSecond * deltaSeconds);
        for (var i = 0; i < players.Count; i++)
        {
            var dealt = boss.TakeDamageDealtBy(i);
            if (!players[i].IsAlive)
            {
                _threat[i] = 0f;
                continue;
            }

            var closeness = Math.Clamp(
                1f - Vector2.Distance(players[i].Position, boss.Position) / _config.ProximityRange, 0f, 1f);
            _threat[i] = _threat[i] * keep + dealt * _config.ThreatPerDamage
                + closeness * _config.ProximityThreatPerSecond * deltaSeconds;
        }

        _targetLeft -= deltaSeconds;
        var targetLost = TargetIndex < 0 || TargetIndex >= players.Count || !players[TargetIndex].IsAlive;
        if (targetLost)
        {
            // A fresh start (or the target died): the nearest player, then threat takes over.
            TargetIndex = Nearest(boss.Position, players);
            _targetLeft = _random.Range(_config.MinTargetSeconds, _config.MaxTargetSeconds);
        }
        else if (_targetLeft <= 0f && mayTurn)
        {
            var top = TargetIndex;
            for (var i = 0; i < players.Count; i++)
            {
                if (players[i].IsAlive && _threat[i] > _threat[top])
                {
                    top = i;
                }
            }

            if (top != TargetIndex && _threat[top] > _threat[TargetIndex] * _config.SwitchMargin)
            {
                TargetIndex = top;
            }

            _targetLeft = _random.Range(_config.MinTargetSeconds, _config.MaxTargetSeconds);
        }

        return TargetIndex >= 0 ? players[TargetIndex] : null;
    }

    private static int Nearest(Vector2 position, IReadOnlyList<Player> players)
    {
        var nearest = -1;
        var best = float.MaxValue;
        for (var i = 0; i < players.Count; i++)
        {
            var distance = Vector2.DistanceSquared(position, players[i].Position);
            if (players[i].IsAlive && distance < best)
            {
                best = distance;
                nearest = i;
            }
        }

        return nearest;
    }
}
