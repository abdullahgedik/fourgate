using System;
using System.Collections.Generic;
using Fourgate.Core.Data;
using Fourgate.Entities;
using Fourgate.Entities.Enemies;
using Microsoft.Xna.Framework;

namespace Fourgate.Systems;

/// <summary>
/// Whom each regular enemy chases. With one player there is nothing to choose; in co-op each
/// enemy type follows its own rule (<see cref="EnemyAggro"/>), targets are sticky so nobody
/// twitches between two players, a fresh enemy goes for the player with the fewest chasers so both
/// stay under pressure, and a boss's minions go for the player the boss is not after.
/// </summary>
public sealed class EnemyTargeting
{
    private readonly EnemyAggroConfig _config;
    private readonly int[] _chasers = new int[MaxPlayers];

    private const int MaxPlayers = 8;

    public EnemyTargeting(EnemyAggroConfig config)
    {
        _config = config;
    }

    /// <summary>The player the boss is after, or -1; its minions go for someone else.</summary>
    public int BossTargetIndex { get; set; } = -1;

    /// <summary>Starts a fresh count of who is chasing whom; call once a frame, then <see cref="CountChaser"/> for each enemy.</summary>
    public void ClearChasers()
    {
        Array.Clear(_chasers);
    }

    /// <summary>Counts one enemy toward its target's chasers (for sending fresh enemies where they are needed most).</summary>
    public void CountChaser(Enemy enemy)
    {
        var target = enemy.TargetIndex;
        if (target >= 0 && target < MaxPlayers && enemy.IsAlive)
        {
            _chasers[target]++;
        }
    }

    /// <summary>Picks (or keeps) an enemy's target and returns it; null when nobody is alive.</summary>
    public Player? Choose(Enemy enemy, IReadOnlyList<Player> players, float deltaSeconds)
    {
        enemy.TargetHeldFor += deltaSeconds;
        var current = Alive(players, enemy.TargetIndex);

        int chosen;
        if (current < 0)
        {
            chosen = Fresh(enemy, players);
        }
        else if (enemy.IsMinion && BossTargetIndex >= 0 && current == BossTargetIndex && OtherAlive(players, BossTargetIndex))
        {
            chosen = NearestExcept(enemy.Position, players, BossTargetIndex);
        }
        else
        {
            chosen = enemy.Config.Aggro switch
            {
                EnemyAggro.Weakest => KeepWeakest(players, current),
                EnemyAggro.Grudge => Grudge(enemy, players, current),
                EnemyAggro.Committed => Committed(enemy, players, current),
                _ => KeepNearest(enemy.Position, players, current),
            };
        }

        if (chosen != enemy.TargetIndex)
        {
            enemy.TargetIndex = chosen;
            enemy.TargetHeldFor = 0f;
        }

        return chosen >= 0 ? players[chosen] : null;
    }

    // A new chaser (or one whose target fell): a minion avoids the boss's target; anyone else goes
    // for the player the fewest are chasing, the nearest of those on a tie.
    private int Fresh(Enemy enemy, IReadOnlyList<Player> players)
    {
        if (enemy.IsMinion && BossTargetIndex >= 0 && OtherAlive(players, BossTargetIndex))
        {
            return NearestExcept(enemy.Position, players, BossTargetIndex);
        }

        var best = -1;
        for (var i = 0; i < players.Count; i++)
        {
            if (!players[i].IsAlive)
            {
                continue;
            }

            if (best < 0 || Chasers(i) < Chasers(best)
                || (Chasers(i) == Chasers(best)
                    && Vector2.DistanceSquared(enemy.Position, players[i].Position)
                    < Vector2.DistanceSquared(enemy.Position, players[best].Position)))
            {
                best = i;
            }
        }

        if (best >= 0 && best < MaxPlayers)
        {
            _chasers[best]++;
        }

        return best;
    }

    private int KeepNearest(Vector2 position, IReadOnlyList<Player> players, int current)
    {
        var nearest = NearestExcept(position, players, -1);
        if (nearest == current || nearest < 0)
        {
            return current;
        }

        var keep = Vector2.Distance(position, players[current].Position);
        var other = Vector2.Distance(position, players[nearest].Position);
        return other < keep * (1f - _config.SwitchCloserShare) ? nearest : current;
    }

    private int KeepWeakest(IReadOnlyList<Player> players, int current)
    {
        var weakest = current;
        for (var i = 0; i < players.Count; i++)
        {
            if (players[i].IsAlive && HealthShare(players[i]) < HealthShare(players[weakest]))
            {
                weakest = i;
            }
        }

        return HealthShare(players[weakest]) < HealthShare(players[current]) - _config.WeakerShare ? weakest : current;
    }

    private int Grudge(Enemy enemy, IReadOnlyList<Player> players, int current)
    {
        var attacker = Alive(players, enemy.LastHitBy);
        if (enemy.SecondsSinceHurt <= _config.GrudgeSeconds && attacker >= 0)
        {
            return attacker;
        }

        return KeepNearest(enemy.Position, players, current);
    }

    // Held for a few seconds (a steady share of the window per enemy, so they do not all look at
    // once), then the nearest player is taken again.
    private int Committed(Enemy enemy, IReadOnlyList<Player> players, int current)
    {
        var spread = (enemy.Id * 0.618034f) % 1f;
        var hold = _config.MinCommitSeconds + (_config.MaxCommitSeconds - _config.MinCommitSeconds) * spread;
        if (enemy.TargetHeldFor < hold)
        {
            return current;
        }

        enemy.TargetHeldFor = 0f;
        return NearestExcept(enemy.Position, players, -1);
    }

    private int Chasers(int player)
    {
        return player < MaxPlayers ? _chasers[player] : 0;
    }

    private static float HealthShare(Player player)
    {
        return player.MaxHp > 0f ? player.Hp / player.MaxHp : 1f;
    }

    private static int Alive(IReadOnlyList<Player> players, int index)
    {
        return index >= 0 && index < players.Count && players[index].IsAlive ? index : -1;
    }

    private static bool OtherAlive(IReadOnlyList<Player> players, int except)
    {
        for (var i = 0; i < players.Count; i++)
        {
            if (i != except && players[i].IsAlive)
            {
                return true;
            }
        }

        return false;
    }

    private static int NearestExcept(Vector2 position, IReadOnlyList<Player> players, int except)
    {
        var nearest = -1;
        var best = float.MaxValue;
        for (var i = 0; i < players.Count; i++)
        {
            if (i == except || !players[i].IsAlive)
            {
                continue;
            }

            var distance = Vector2.DistanceSquared(position, players[i].Position);
            if (distance < best)
            {
                best = distance;
                nearest = i;
            }
        }

        return nearest;
    }
}
