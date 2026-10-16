using Fourgate.Core.Audio;
using Fourgate.Core.Data;
using Fourgate.Core.Rng;
using Fourgate.Entities;
using Fourgate.Entities.Enemies;
using Fourgate.Systems;
using Microsoft.Xna.Framework;

namespace Fourgate.Tests;

public class EnemyAggroTests
{
    private const float Step = 1f / 60f;

    private static readonly EnemyAggroConfig Rules = new()
    {
        SwitchCloserShare = 0.3f, WeakerShare = 0.15f, GrudgeSeconds = 4f, MinCommitSeconds = 4f, MaxCommitSeconds = 4f,
    };

    [Fact]
    public void NearestChaser_KeepsItsTarget_UntilAnotherPlayerIsClearlyCloser()
    {
        var (targeting, enemy, players) = Setup(EnemyAggro.Nearest, new Vector2(1000, 1000), new Vector2(1500, 1000), new Vector2(500, 1000));
        enemy.TargetIndex = 0;

        // Player 2 a little closer (450 vs 500): it stays on player 1.
        players[1].PushOut(new Vector2(50, 0), new Arena(TestData.Arena()));
        Assert.Same(players[0], targeting.Choose(enemy, players, Step));

        // Player 2 much closer: it switches.
        players[1].PushOut(new Vector2(300, 0), new Arena(TestData.Arena()));
        Assert.Same(players[1], targeting.Choose(enemy, players, Step));
    }

    [Fact]
    public void Runner_GoesForTheWeakerPlayer()
    {
        var (targeting, enemy, players) = Setup(EnemyAggro.Weakest, new Vector2(1000, 1000), new Vector2(1100, 1000), new Vector2(2500, 1000));
        enemy.TargetIndex = 0;

        players[1].TakeDamage(players[1].MaxHp * 0.5f);

        Assert.Same(players[1], targeting.Choose(enemy, players, Step));
    }

    [Fact]
    public void Brute_TurnsOnWhoeverHurtIt_ThenForgets()
    {
        var (targeting, enemy, players) = Setup(EnemyAggro.Grudge, new Vector2(1000, 1000), new Vector2(1100, 1000), new Vector2(2500, 1000));
        enemy.TargetIndex = 0;

        enemy.Hit(1f, 1);
        Assert.Same(players[1], targeting.Choose(enemy, players, Step));

        // The grudge fades; player 1 is far nearer, so it goes back to them.
        for (var i = 0; i < 300; i++)
        {
            enemy.Tick(Step);
        }

        Assert.Same(players[0], targeting.Choose(enemy, players, Step));
    }

    [Fact]
    public void Spitter_HoldsItsTarget_ForItsCommitment()
    {
        var (targeting, enemy, players) = Setup(EnemyAggro.Committed, new Vector2(1000, 1000), new Vector2(2000, 1000), new Vector2(1050, 1000));
        enemy.TargetIndex = 0;

        Assert.Same(players[0], targeting.Choose(enemy, players, 3f));
        Assert.Same(players[1], targeting.Choose(enemy, players, 1.5f));
    }

    [Fact]
    public void FreshEnemies_SplitBetweenThePlayers()
    {
        var enemies = new EnemySystem(new NullAudioManager()) { Targeting = new EnemyTargeting(Rules) };
        var players = Players(new Vector2(1000, 1000), new Vector2(2000, 1000));
        for (var i = 0; i < 6; i++)
        {
            enemies.Spawn(Config(EnemyAggro.Nearest), new Vector2(1500, 500));
        }

        enemies.Update(Step, players, new Arena(TestData.Arena()));

        var onFirst = 0;
        for (var i = 0; i < enemies.Count; i++)
        {
            onFirst += enemies[i].TargetIndex == 0 ? 1 : 0;
        }

        Assert.Equal(3, onFirst);
    }

    [Fact]
    public void BossMinions_GoForThePlayerTheBossIsNotAfter()
    {
        var (targeting, enemy, players) = Setup(EnemyAggro.Nearest, new Vector2(1000, 1000), new Vector2(1050, 1000), new Vector2(2500, 1000));
        enemy.IsMinion = true;
        targeting.BossTargetIndex = 0;

        Assert.Same(players[1], targeting.Choose(enemy, players, Step));
    }

    [Fact]
    public void Alone_EveryEnemyChasesTheOnlyPlayer()
    {
        var targeting = new EnemyTargeting(Rules) { BossTargetIndex = 0 };
        var enemies = new EnemySystem(new NullAudioManager());
        var enemy = enemies.Spawn(Config(EnemyAggro.Weakest), new Vector2(1000, 1000));
        enemy.IsMinion = true;
        var players = Players(new Vector2(1200, 1000));

        Assert.Same(players[0], targeting.Choose(enemy, players, Step));
    }

    [Fact]
    public void BossPhaseChange_ShrinksEveryonesThreat()
    {
        var config = new BossAggroConfig
        {
            MinTargetSeconds = 5, MaxTargetSeconds = 9, ThreatPerDamage = 1, ProximityThreatPerSecond = 0,
            ProximityRange = 500, ThreatDecayPerSecond = 0, SwitchMargin = 1.3f, PhaseThreatKeep = 0.3f,
        };
        var aggro = new BossAggro(config, new GameRandom(1));
        var enemies = new EnemySystem(new NullAudioManager());
        var boss = enemies.Spawn(Config(EnemyAggro.Nearest), new Vector2(1000, 1000));
        var players = Players(new Vector2(1200, 1000), new Vector2(800, 1000));
        boss.Hit(100f, 0);
        aggro.Update(boss, players, Step, mayTurn: true);
        var before = aggro.GetThreat(0);

        aggro.OnPhaseChange();

        Assert.Equal(before * 0.3f, aggro.GetThreat(0), 2);
    }

    [Fact]
    public void ShippedEnemies_EachHaveTheirOwnRule()
    {
        var data = DataLoader.LoadAll(DataLoader.DefaultDirectory);

        Assert.Equal(EnemyAggro.Nearest, data.GetEnemy("normal").Aggro);
        Assert.Equal(EnemyAggro.Weakest, data.GetEnemy("fast").Aggro);
        Assert.Equal(EnemyAggro.Committed, data.GetEnemy("ranged").Aggro);
        Assert.Equal(EnemyAggro.Grudge, data.GetEnemy("tank").Aggro);
        Assert.Equal(EnemyAggro.Grudge, data.GetEnemy("elite").Aggro);
        Assert.True(data.Game.BossAggro!.PhaseThreatKeep < 1f);
    }

    private static (EnemyTargeting Targeting, Enemy Enemy, List<Player> Players) Setup(
        EnemyAggro rule, Vector2 enemyAt, params Vector2[] playersAt)
    {
        var enemies = new EnemySystem(new NullAudioManager());
        var enemy = enemies.Spawn(Config(rule), enemyAt);
        return (new EnemyTargeting(Rules), enemy, Players(playersAt));
    }

    private static List<Player> Players(params Vector2[] positions)
    {
        var players = new List<Player>();
        for (var i = 0; i < positions.Length; i++)
        {
            players.Add(new Player(i, new FakeInput(), TestData.Game().Player, new NullAudioManager(), positions[i]));
        }

        return players;
    }

    private static EnemyConfig Config(EnemyAggro rule)
    {
        return new EnemyConfig
        {
            Id = "chaser", DisplayName = "Chaser", Hp = 1000, Speed = 0, Radius = 20, ContactDamage = 0,
            ContactCooldown = 0.8f, Xp = 1, ScoreValue = 1, Cost = 1, UnlockWave = 1, DropChance = 0f, Aggro = rule,
        };
    }
}
