using Fourgate.Core.Audio;
using Fourgate.Core.Data;
using Fourgate.Core.Rng;
using Fourgate.Entities;
using Fourgate.Entities.Enemies;
using Fourgate.Systems;
using Microsoft.Xna.Framework;

namespace Fourgate.Tests;

public class BossTests
{
    private const float Step = 1f / 60f;

    [Fact]
    public void ShippedButcher_LeadsWaveTen_WithAChargeASlamAndASummon()
    {
        var data = DataLoader.LoadAll(DataLoader.DefaultDirectory);
        var butcher = data.GetBossForWave(10);

        Assert.NotNull(butcher);
        Assert.Equal("butcher", butcher!.Id);
        Assert.Equal(3000f, butcher.Hp);
        Assert.Contains(butcher.Patterns, p => p.Type == BossPatternType.LineCharge && p.TelegraphSeconds >= 0.8f);
        Assert.Contains(butcher.Patterns, p => p.Type == BossPatternType.GroundSlamRing && p.TelegraphSeconds >= 0.8f);
        Assert.Contains(butcher.Patterns, p => p.Type == BossPatternType.Summon && p.EnemyId == "normal");
        Assert.Null(data.GetBossForWave(9));
    }

    [Fact]
    public void AreaAttack_WithAShortWarning_IsRejected()
    {
        const string json = """
            { "bosses": [ { "id": "b", "displayName": "B", "wave": 10, "hp": 100, "speed": 1, "radius": 40,
              "contactDamage": 0, "contactCooldown": 1, "xp": 1, "scoreValue": 1, "addsBudgetFraction": 0.3,
              "idleSeconds": 1, "patterns": [ { "type": "lineCharge", "telegraphSeconds": 0.5, "damage": 40,
              "distance": 800, "speed": 1000 } ] } ] }
            """;

        var error = Assert.Throws<DataLoadException>(() => DataLoader.ParseBosses(json, "bosses.json"));

        Assert.Equal("bosses[0].patterns[0].telegraphSeconds", error.Field);
    }

    [Fact]
    public void BossWave_SendsTheBoss_WithFewerOtherEnemies()
    {
        var shipped = DataLoader.LoadAll(DataLoader.DefaultDirectory);
        var bossWave = StartWave(shipped, 10);
        var normalWave = StartWave(shipped, 11);

        Assert.True(bossWave < normalWave / 2);
    }

    [Fact]
    public void Charge_IsWarnedAbout_ThenHitsThePlayerInItsPath()
    {
        var world = new World(Pattern(BossPatternType.LineCharge));
        var player = world.AddPlayer(new Vector2(1500, 1000));
        var boss = world.SpawnBoss(new Vector2(1000, 1000));

        // 1 s of walking, then a 0.9 s warning: no damage yet.
        world.Run(1.5f);
        Assert.True(world.Bosses.IsTelegraphing);
        Assert.Equal(BossPatternType.LineCharge, world.Bosses.CurrentPattern);
        Assert.Equal(100f, player.Hp);

        world.Run(1.5f);
        Assert.Equal(60f, player.Hp);
        Assert.True(boss.Position.X > 1300f);
    }

    [Fact]
    public void Slam_SendsAShockwave_ThatHitsAPlayerInReachOnce()
    {
        var world = new World(Pattern(BossPatternType.GroundSlamRing));
        var near = world.AddPlayer(new Vector2(1300, 1000));
        world.SpawnBoss(new Vector2(1000, 1000));

        world.Run(4f);

        Assert.Equal(70f, near.Hp);
    }

    [Fact]
    public void Summon_CallsInEnemies_AroundTheBoss()
    {
        var world = new World(Pattern(BossPatternType.Summon));
        world.AddPlayer(new Vector2(1600, 1000));
        world.SpawnBoss(new Vector2(1000, 1000));

        world.Run(2.5f);

        Assert.Equal(1 + 4, world.Enemies.Count);
    }

    [Fact]
    public void CoopBoss_HasMoreHp()
    {
        var world = new World(Pattern(BossPatternType.Summon)) { HpMultiplier = 1.5f };
        world.AddPlayer(new Vector2(1600, 1000));
        var boss = world.SpawnBoss(new Vector2(1000, 1000));

        world.Run(Step);

        Assert.Equal(150f, boss.MaxHp);
        Assert.Equal(150f, boss.Hp);
    }

    // Starts a wave and returns its enemy count; on a boss wave, also checks the boss comes in.
    [Fact]
    public void ShippedPyreWitch_LeadsWaveTwenty_WithFireballsTeleportAndRangedSummons()
    {
        var witch = DataLoader.LoadAll(DataLoader.DefaultDirectory).GetBossForWave(20);

        Assert.NotNull(witch);
        Assert.Equal(5000f, witch!.Hp);
        Assert.Contains(witch.Patterns, p => p.Type == BossPatternType.FireballRing && p.TelegraphSeconds >= 0.6f);
        Assert.Contains(witch.Patterns, p => p.Type == BossPatternType.FireballSpiral && p.TelegraphSeconds >= 0.6f);
        Assert.Contains(witch.Patterns, p => p.Type == BossPatternType.Teleport);
        Assert.Contains(witch.Patterns, p => p.Type == BossPatternType.Summon && p.EnemyId == "ranged");
    }

    [Fact]
    public void FireballAttack_WithAShortWarning_IsRejected()
    {
        const string json = """
            { "bosses": [ { "id": "b", "displayName": "B", "wave": 10, "hp": 100, "speed": 1, "radius": 40,
              "contactDamage": 0, "contactCooldown": 1, "xp": 1, "scoreValue": 1, "addsBudgetFraction": 0.3,
              "idleSeconds": 1, "patterns": [ { "type": "fireballRing", "telegraphSeconds": 0.4, "damage": 10,
              "count": 8, "projectileSpeed": 300, "projectileRadius": 12, "projectileRange": 900 } ] } ] }
            """;

        var error = Assert.Throws<DataLoadException>(() => DataLoader.ParseBosses(json, "bosses.json"));

        Assert.Equal("bosses[0].patterns[0].telegraphSeconds", error.Field);
    }

    [Fact]
    public void FireballRing_ThrowsEveryBurst_AllTheWayRound()
    {
        var pattern = new BossPatternConfig
        {
            Type = BossPatternType.FireballRing, TelegraphSeconds = 0.7f, Damage = 10, Count = 8, Bursts = 2, Interval = 0.3f,
            ProjectileSpeed = 300, ProjectileRadius = 12, ProjectileRange = 900,
        };
        var world = new World(pattern);
        world.AddPlayer(new Vector2(1500, 1000));
        world.SpawnBoss(new Vector2(1000, 1000));

        // 1 s walking + 0.7 s warning: nothing yet.
        world.Run(1.65f);
        Assert.Equal(0, world.Fireballs.FireballCount);

        world.Run(0.5f);
        Assert.Equal(16, world.Fireballs.FireballCount);
    }

    [Fact]
    public void FireballSpiral_KeepsThrowingFromItsArms_ForItsDuration()
    {
        var pattern = new BossPatternConfig
        {
            Type = BossPatternType.FireballSpiral, TelegraphSeconds = 0.7f, Damage = 10, Count = 2, Seconds = 1f,
            Interval = 0.25f, SpinDegreesPerSecond = 120, ProjectileSpeed = 300, ProjectileRadius = 12, ProjectileRange = 900,
        };
        var world = new World(pattern);
        world.AddPlayer(new Vector2(1500, 1000));
        world.SpawnBoss(new Vector2(1000, 1000));

        world.Run(2.8f);

        Assert.InRange(world.Fireballs.FireballCount, 8, 10);
        Assert.False(world.Bosses.IsTelegraphing);
    }

    [Fact]
    public void Teleport_MovesTheBoss_InsideAnotherDoor()
    {
        var world = new World(new BossPatternConfig { Type = BossPatternType.Teleport, TelegraphSeconds = 0.8f });
        world.AddPlayer(new Vector2(1500, 600));
        var boss = world.SpawnBoss(new Vector2(1500, 300));

        world.Run(1.9f);

        // It started next to the top door; it ends up just inside one of the other three.
        var arena = new Arena(TestData.Arena());
        Assert.True(boss.Position.Y > 600f || boss.Position.X < 400f || boss.Position.X > 2600f);
        var nearDoor = false;
        foreach (var door in arena.Doors)
        {
            nearDoor |= Vector2.Distance(door.Center, boss.Position) < 200f;
        }

        Assert.True(nearDoor);
    }

    private static int StartWave(GameData shipped, int wave)
    {
        var config = TestData.Game(startWave: wave);
        var data = new GameData(config, shipped.Weapons, shipped.Enemies, shipped.Waves, shipped.Upgrades, shipped.Bosses);
        var enemies = new EnemySystem(new NullAudioManager());
        var waves = new WaveSystem(data, enemies, new Arena(config.Arena), new GameRandom(3), new NullAudioManager());
        while (waves.Phase != WavePhase.Active)
        {
            waves.Update(Step);
        }

        var count = waves.EnemiesRemaining;
        for (var i = 0; i < 60 * 30 && enemies.FindBoss() is null && wave % 10 == 0; i++)
        {
            waves.Update(Step);
        }

        if (wave % 10 == 0)
        {
            Assert.NotNull(enemies.FindBoss());
        }

        return count;
    }

    private static BossPatternConfig Pattern(BossPatternType type)
    {
        return new BossPatternConfig
        {
            Type = type, TelegraphSeconds = 0.9f, Damage = type == BossPatternType.LineCharge ? 40 : 30,
            Distance = 800, Speed = 1100, Radius = 380, RingSpeed = 650, RingWidth = 40,
            EnemyId = "normal", Count = 4, SpawnRadius = 140,
        };
    }

    private sealed class World
    {
        private readonly Arena _arena;
        private readonly List<Player> _players = new();
        private readonly BossConfig _boss;

        public World(BossPatternConfig pattern)
        {
            _boss = new BossConfig
            {
                Id = "boss", DisplayName = "Boss", Wave = 10, Hp = 100, Speed = 0, Radius = 50, ContactDamage = 0,
                ContactCooldown = 1, Xp = 1, ScoreValue = 1, AddsBudgetFraction = 0.3f, IdleSeconds = 1,
                Patterns = new List<BossPatternConfig> { pattern },
            };
            var data = new GameData(
                TestData.Game(), new[] { TestData.Pistol() }, new[] { TestData.Zombie() }, TestData.Waves(), TestData.Upgrades(),
                new[] { _boss });
            _arena = new Arena(TestData.Arena());
            Enemies = new EnemySystem(new NullAudioManager());
            Bosses = new BossSystem(Enemies, data, new GameRandom(1), new NullAudioManager()) { Fireballs = Fireballs };
        }

        public EnemyShotSystem Fireballs { get; } = new();

        public EnemySystem Enemies { get; }

        public BossSystem Bosses { get; }

        public float HpMultiplier
        {
            set => Bosses.HpMultiplier = value;
        }

        public Player AddPlayer(Vector2 position)
        {
            var player = new Player(_players.Count, new FakeInput(), TestData.Game().Player, new NullAudioManager(), position);
            _players.Add(player);
            return player;
        }

        public Enemy SpawnBoss(Vector2 position)
        {
            return Enemies.Spawn(_boss.AsEnemy, position);
        }

        public void Run(float seconds)
        {
            var steps = Math.Max(1, (int)MathF.Round(seconds / Step));
            for (var i = 0; i < steps; i++)
            {
                Enemies.Update(Step, _players, _arena);
                Bosses.Update(Step, _players, _arena);
            }
        }
    }
}
