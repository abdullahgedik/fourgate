using Fourgate.Core.Audio;
using Fourgate.Core.Data;
using Fourgate.Scenes;
using Fourgate.Core.Input;
using Fourgate.Core.Rng;
using Fourgate.Entities;
using Fourgate.Entities.Enemies;
using Fourgate.Systems;
using Fourgate.Weapons;
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

    [Fact]
    public void ShippedJuggernaut_LeadsWaveThirty_ArmoredInFront_WithAFireStreamAndACharge()
    {
        var juggernaut = DataLoader.LoadAll(DataLoader.DefaultDirectory).GetBossForWave(30);

        Assert.NotNull(juggernaut);
        Assert.Equal(8000f, juggernaut!.Hp);
        Assert.True(juggernaut.FrontArmorArcDegrees >= 120f);
        Assert.Equal(0f, juggernaut.FrontArmorDamageMult);
        Assert.True(juggernaut.TurnDegreesPerSecond > 0f);
        Assert.Contains(juggernaut.Patterns, p => p.Type == BossPatternType.FireStream && p.TelegraphSeconds >= 0.8f);
        Assert.Contains(juggernaut.Patterns, p => p.Type == BossPatternType.LineCharge);
    }

    [Fact]
    public void FrontArmor_TakesMostOfAShotFromTheFront_ButNotFromBehindOrFromABlast()
    {
        var world = new World(Pattern(BossPatternType.Summon));
        var boss = world.SpawnBoss(new Vector2(1000, 1000));
        boss.Facing = 0f;

        // Landing on the +X side: the armored front.
        boss.HitAt(10f, 0, boss.Position + Vector2.UnitX * 50f);
        Assert.Equal(98f, boss.Hp, 3);

        boss.HitAt(10f, 0, boss.Position - Vector2.UnitX * 50f);
        Assert.Equal(88f, boss.Hp, 3);

        boss.Hit(10f, 0);
        Assert.Equal(78f, boss.Hp, 3);
    }

    [Theory]
    [InlineData(true, 0)]
    [InlineData(false, 1)]
    public void PiercingBullet_StopsOnTheArmoredFront_ButPassesThroughFromBehind(bool fromTheFront, int hitsBehind)
    {
        var world = new World(Pattern(BossPatternType.Summon));
        var boss = world.SpawnBoss(new Vector2(1000, 1000));
        boss.Facing = fromTheFront ? 0f : MathF.PI;
        world.Enemies.Update(Step, new List<Player>(), new Arena(TestData.Arena()));
        var shots = new ShotSystem(new Arena(TestData.Arena()));
        shots.AddSource(world.Enemies);
        var behind = new PracticeTarget(new Vector2(700, 1000), 20);
        shots.AddTarget(behind);

        // Flying toward -X: it meets the +X side of the boss first.
        shots.FireProjectile(
            new Vector2(1400, 1000), -Vector2.UnitX, new ProjectileConfig { Speed = 6000, Radius = 4, Range = 1100 }, 10, 0,
            pierce: 3);
        for (var i = 0; i < 20; i++)
        {
            shots.Update(Step);
        }

        Assert.Equal(hitsBehind, behind.HitCount);
        Assert.Equal(fromTheFront ? 98f : 90f, boss.Hp, 3);
    }

    [Fact]
    public void Shot_IsBlocked_ByWhereItLands_NotTheWayItFlies()
    {
        var world = new World(Pattern(BossPatternType.Summon));
        var boss = world.SpawnBoss(new Vector2(1000, 1000));
        boss.Facing = 0f;
        world.Enemies.Update(Step, new List<Player>(), new Arena(TestData.Arena()));
        var shots = new ShotSystem(new Arena(TestData.Arena()));
        shots.AddSource(world.Enemies);

        // Flying straight down at the boss's upper-right edge: it lands about 40 degrees off the
        // facing, inside the 90-degree armored arc, so the plate takes it.
        shots.FireProjectile(
            new Vector2(1038, 700), Vector2.UnitY, new ProjectileConfig { Speed = 6000, Radius = 1, Range = 1100 }, 10, 0,
            pierce: 3);
        for (var i = 0; i < 10; i++)
        {
            shots.Update(Step);
        }

        Assert.Equal(98f, boss.Hp, 3);
        Assert.Equal(1, shots.SparkCount);
    }

    [Fact]
    public void ArmorWithNoDamageThrough_LeavesTheBossUntouched()
    {
        var shipped = DataLoader.LoadAll(DataLoader.DefaultDirectory).GetBossForWave(30)!;
        var enemies = new EnemySystem(new NullAudioManager());
        var boss = enemies.Spawn(shipped.AsEnemy, new Vector2(1000, 1000));
        boss.Facing = 0f;

        boss.HitAt(500f, 0, boss.Position + Vector2.UnitX * boss.Radius);

        Assert.Equal(boss.MaxHp, boss.Hp);
        Assert.False(boss.ConsumeHit());
    }

    [Fact]
    public void ShieldBand_StopsShotsOutInFrontOfTheBody()
    {
        var world = new World(Pattern(BossPatternType.Summon), armorGap: 20f, armorThickness: 20f);
        var boss = world.SpawnBoss(new Vector2(1000, 1000));
        boss.Facing = 0f;
        world.Enemies.Update(Step, new List<Player>(), new Arena(TestData.Arena()));
        var shots = new ShotSystem(new Arena(TestData.Arena()));
        shots.AddSource(world.Enemies);
        var behind = new PracticeTarget(new Vector2(700, 1000), 20);
        shots.AddTarget(behind);

        shots.FireProjectile(
            new Vector2(1400, 1000), -Vector2.UnitX, new ProjectileConfig { Speed = 600, Radius = 4, Range = 1100 }, 10, 0,
            pierce: 3);
        for (var i = 0; i < 60 && shots.BulletCount > 0; i++)
        {
            shots.Update(Step);
        }

        // Body radius 50 + gap 20 + band 20: it stops 90 out (plus its own radius), untouched behind.
        Assert.Equal(0, shots.BulletCount);
        Assert.Equal(1, shots.SparkCount);
        Assert.Equal(100f, boss.Hp);
        Assert.Equal(0, behind.HitCount);

        // A beam stops at the band too.
        shots.FireBeam(new BeamShot(new Vector2(1400, 1000), -Vector2.UnitX, 10, 2400, 10, 0, 0));
        Assert.Equal(0, behind.HitCount);
        Assert.Equal(100f, boss.Hp);
    }

    [Fact]
    public void ShieldBand_LeavesTheSidesOpen()
    {
        var world = new World(Pattern(BossPatternType.Summon), armorGap: 20f, armorThickness: 20f);
        var boss = world.SpawnBoss(new Vector2(1000, 1000));
        boss.Facing = 0f;
        world.Enemies.Update(Step, new List<Player>(), new Arena(TestData.Arena()));
        var shots = new ShotSystem(new Arena(TestData.Arena()));
        shots.AddSource(world.Enemies);

        // Straight down onto the top of the body, 90 degrees from the facing.
        shots.FireProjectile(
            new Vector2(1000, 600), Vector2.UnitY, new ProjectileConfig { Speed = 6000, Radius = 4, Range = 1100 }, 10, 0);
        for (var i = 0; i < 10; i++)
        {
            shots.Update(Step);
        }

        Assert.Equal(90f, boss.Hp, 3);
        Assert.Equal(0, shots.SparkCount);
    }

    [Fact]
    public void Beam_StopsOnTheArmoredFront()
    {
        var world = new World(Pattern(BossPatternType.Summon));
        var boss = world.SpawnBoss(new Vector2(1000, 1000));
        boss.Facing = 0f;
        world.Enemies.Update(Step, new List<Player>(), new Arena(TestData.Arena()));
        var shots = new ShotSystem(new Arena(TestData.Arena()));
        shots.AddSource(world.Enemies);
        var behind = new PracticeTarget(new Vector2(700, 1000), 20);
        shots.AddTarget(behind);

        shots.FireBeam(new BeamShot(new Vector2(1400, 1000), -Vector2.UnitX, 10, 2400, 10, 0, 0));

        Assert.Equal(0, behind.HitCount);
        Assert.Equal(98f, boss.Hp, 3);
    }

    [Fact]
    public void SlowTurner_CanBeFlanked_AndDoesNotChargeAtATargetBehindIt()
    {
        var world = new World(Pattern(BossPatternType.LineCharge), turnDegreesPerSecond: 60f);
        var boss = world.SpawnBoss(new Vector2(1000, 1000));
        boss.Facing = 0f;
        world.AddPlayer(new Vector2(700, 1000));

        // Behind it: half a second turns it only 30 degrees, and it does not charge yet.
        world.Run(0.5f);
        Assert.InRange(MathF.Abs(boss.Facing), 0.45f, 0.6f);

        world.Run(0.6f);
        Assert.False(world.Bosses.IsTelegraphing);
    }

    [Fact]
    public void FireStream_BurnsAPlayerInItsPath_OverTime()
    {
        var pattern = new BossPatternConfig
        {
            Type = BossPatternType.FireStream, TelegraphSeconds = 0.8f, Damage = 40, Distance = 420, Width = 34,
            Seconds = 1f, SpinDegreesPerSecond = 0,
        };
        var world = new World(pattern);
        var inFront = world.AddPlayer(new Vector2(1300, 1000));
        var farAway = world.AddPlayer(new Vector2(1000, 1600));
        world.SpawnBoss(new Vector2(1000, 1000));

        world.Run(3f);

        Assert.InRange(inFront.Hp, 58f, 62f);
        Assert.Equal(100f, farAway.Hp);
    }

    [Fact]
    public void ShippedSwarmMother_LeadsWaveForty_SpawningEveryKindOfZombie_AndSplittingOnDeath()
    {
        var swarm = DataLoader.LoadAll(DataLoader.DefaultDirectory).GetBossForWave(40);

        Assert.NotNull(swarm);
        Assert.Equal(10000f, swarm!.Hp);
        foreach (var kind in new[] { "fast", "normal", "ranged", "tank", "elite" })
        {
            Assert.Contains(swarm.Patterns, p => p.Type == BossPatternType.Summon && p.EnemyId == kind);
        }

        // Each brood brings its own attack: fast → haste, ranged → ring, tank → charge, elite → volley.
        BossPatternType? FollowUpOf(string kind) => swarm.Patterns.Find(p => p.EnemyId == kind)!.FollowUp?.Type;
        Assert.Null(FollowUpOf("normal"));
        Assert.Equal(BossPatternType.Haste, FollowUpOf("fast"));
        Assert.Equal(BossPatternType.FireballRing, FollowUpOf("ranged"));
        Assert.Equal(BossPatternType.LineCharge, FollowUpOf("tank"));
        Assert.Equal(BossPatternType.FireballVolley, FollowUpOf("elite"));
        Assert.NotNull(swarm.DeathSpawn);
        Assert.Equal("fast", swarm.DeathSpawn!.EnemyId);
        Assert.True(swarm.DeathSpawn.Count >= 8);
    }

    [Fact]
    public void BossWithADeathSpawn_BurstsIntoItsBrood_WhenItDies()
    {
        var data = DataLoader.LoadAll(DataLoader.DefaultDirectory);
        var swarm = data.GetBossForWave(40)!;
        var arena = new Arena(data.Game.Arena);
        var enemies = new EnemySystem(new NullAudioManager());
        var bosses = new BossSystem(enemies, data, new GameRandom(1), new NullAudioManager());
        var mother = enemies.Spawn(swarm.AsEnemy, new Vector2(1500, 1500));

        mother.Hit(float.MaxValue, 0);
        enemies.Update(Step, new List<Player>(), arena);
        for (var i = 0; i < enemies.Deaths.Count; i++)
        {
            bosses.OnBossDied(enemies.Deaths[i], arena);
        }

        Assert.Equal(swarm.DeathSpawn!.Count, enemies.Count);
        Assert.Null(enemies.FindBoss());
    }

    [Fact]
    public void DeathSpawn_OfAnUnknownEnemy_IsRejected()
    {
        var swarm = new BossConfig
        {
            Id = "b", DisplayName = "B", Wave = 40, Hp = 100, Speed = 0, Radius = 50, ContactDamage = 0, ContactCooldown = 1,
            Xp = 1, ScoreValue = 1, AddsBudgetFraction = 0.3f, IdleSeconds = 1,
            Patterns = new List<BossPatternConfig> { Pattern(BossPatternType.Summon) },
            DeathSpawn = new BossDeathSpawnConfig { EnemyId = "nobody", Count = 3, SpawnRadius = 50 },
        };

        var error = Assert.Throws<DataLoadException>(() => new GameData(
            TestData.Game(), new[] { TestData.Pistol() }, new[] { TestData.Zombie() }, TestData.Waves(), TestData.Upgrades(),
            new[] { swarm }));

        Assert.Equal("bosses[0].deathSpawn.enemyId", error.Field);
    }

    [Fact]
    public void Summon_IsFollowedRightAway_ByTheAttackThatGoesWithIt()
    {
        var summon = new BossPatternConfig
        {
            Type = BossPatternType.Summon, TelegraphSeconds = 0.8f, EnemyId = "normal", Count = 2, SpawnRadius = 140,
            FollowUp = new BossPatternConfig
            {
                Type = BossPatternType.FireballVolley, TelegraphSeconds = 0.7f, Damage = 10, Count = 5, SpreadDegrees = 25,
                Bursts = 3, Interval = 0.3f, ProjectileSpeed = 420, ProjectileRadius = 12, ProjectileRange = 900,
            },
        };
        var world = new World(summon);
        world.AddPlayer(new Vector2(1600, 1000));
        world.SpawnBoss(new Vector2(1000, 1000));

        // 1 s walk + 0.8 s summon warning; the volley's warning starts at once, with no idle walk.
        world.Run(1.95f);
        Assert.Equal(3, world.Enemies.Count);
        Assert.True(world.Bosses.IsTelegraphing);
        Assert.Equal(BossPatternType.FireballVolley, world.Bosses.CurrentPattern);

        world.Run(1.4f);
        Assert.Equal(15, world.Fireballs.FireballCount);
    }

    [Fact]
    public void Haste_MakesTheBossWalkFaster_ForAWhile()
    {
        var haste = new BossPatternConfig
        {
            Type = BossPatternType.Haste, TelegraphSeconds = 0.5f, SpeedMultiplier = 3f, Seconds = 2f,
        };
        var world = new World(haste, speed: 50f);
        world.AddPlayer(new Vector2(2500, 1000));
        var boss = world.SpawnBoss(new Vector2(500, 1000));

        // 1 s at 50, then a 0.5 s warning standing still: 50 units.
        world.Run(1.5f);
        var before = boss.Position.X;

        world.Run(1f);
        Assert.InRange(boss.Position.X - before, 140f, 160f);
    }

    [Fact]
    public void SequenceOrder_UsesThePatternsOneAfterAnother()
    {
        BossPatternConfig Summon(int count) => new()
        {
            Type = BossPatternType.Summon, TelegraphSeconds = 0.8f, EnemyId = "normal", Count = count, SpawnRadius = 140,
        };
        var patterns = new List<BossPatternConfig> { Summon(1), Summon(2), Summon(3) };
        var world = new World(patterns[0], patterns: patterns, order: BossPatternOrder.Sequence);
        world.AddPlayer(new Vector2(1600, 1000));
        world.SpawnBoss(new Vector2(1000, 1000));

        // Each summon takes 1 s of walking plus a 0.8 s warning; look once per 2 s.
        var counts = new List<int>();
        for (var i = 0; i < 4; i++)
        {
            world.Run(2f);
            counts.Add(world.Enemies.Count - 1);
        }

        Assert.Equal(new[] { 1, 3, 6, 7 }, counts);
    }

    [Fact]
    public void ShuffledOrder_UsesEveryPatternOncePerRound_InAChangingOrder()
    {
        BossPatternConfig Summon(int count) => new()
        {
            Type = BossPatternType.Summon, TelegraphSeconds = 0.8f, EnemyId = "normal", Count = count, SpawnRadius = 140,
        };
        var patterns = new List<BossPatternConfig> { Summon(1), Summon(2), Summon(4), Summon(8) };
        var world = new World(patterns[0], patterns: patterns, order: BossPatternOrder.Shuffled);
        var player = world.AddPlayer(new Vector2(1600, 1000));
        world.SpawnBoss(new Vector2(1000, 1000));

        // Read each summon's size from how many enemies it added; 4 rounds of 4. The summoned
        // zombies would kill the player, which stops the boss, so the player is kept topped up.
        var sizes = new List<int>();
        var before = 0;
        for (var frame = 0; frame < 60 * 60 && sizes.Count < 16; frame++)
        {
            player.HealToFull();
            world.Run(Step);
            var now = world.Enemies.Count - 1;
            if (now != before)
            {
                sizes.Add(now - before);
                before = now;
            }
        }

        Assert.Equal(16, sizes.Count);
        var orders = new HashSet<string>();
        for (var round = 0; round < 4; round++)
        {
            var slice = sizes.GetRange(round * 4, 4);
            Assert.Equal(new[] { 1, 2, 4, 8 }, slice.OrderBy(x => x));
            orders.Add(string.Join(",", slice));
        }

        for (var i = 1; i < sizes.Count; i++)
        {
            Assert.NotEqual(sizes[i - 1], sizes[i]);
        }

        Assert.True(orders.Count > 1);
    }

    [Fact]
    public void Crowd_CannotShoveTheBoss_TheZombiesMakeWayInstead()
    {
        var enemies = new EnemySystem(new NullAudioManager());
        var arena = new Arena(TestData.Arena());
        var boss = new World(Pattern(BossPatternType.Summon)).SpawnBossInto(enemies, new Vector2(1500, 1500));
        var zombies = new List<Enemy>();
        for (var i = 0; i < 12; i++)
        {
            var angle = MathHelper.TwoPi * i / 12;
            zombies.Add(enemies.Spawn(TestData.Zombie(), boss.Position + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * 40f));
        }

        for (var i = 0; i < 10; i++)
        {
            enemies.Update(Step, new List<Player>(), arena);
        }

        Assert.Equal(new Vector2(1500, 1500), boss.Position);
        foreach (var zombie in zombies)
        {
            Assert.True(Vector2.Distance(zombie.Position, boss.Position) >= boss.Radius + zombie.Radius - 1f);
        }
    }

    [Fact]
    public void ShippedWarden_LeadsWaveFifty_InThreePhases_BorrowingFromEveryBoss()
    {
        var warden = DataLoader.LoadAll(DataLoader.DefaultDirectory).GetBossForWave(50);

        Assert.NotNull(warden);
        Assert.Equal(20000f, warden!.Hp);
        Assert.Equal(2, warden.Phases.Count);
        Assert.Equal(0.66f, warden.Phases[0].HpFraction, 3);
        Assert.Equal(0.33f, warden.Phases[1].HpFraction, 3);
        Assert.Contains(warden.Patterns, p => p.Type == BossPatternType.LaserSweep);
        Assert.All(warden.Phases, phase => Assert.Contains(phase.Patterns, p => p.Type == BossPatternType.LaserSweep));

        var all = warden.AllPatterns;
        foreach (var borrowed in new[]
        {
            BossPatternType.LineCharge, BossPatternType.GroundSlamRing, BossPatternType.FireballRing,
            BossPatternType.FireballSpiral, BossPatternType.Teleport, BossPatternType.FireStream, BossPatternType.Summon,
            BossPatternType.LeapSlam, BossPatternType.MissileRain, BossPatternType.FireballStream,
        })
        {
            Assert.Contains(all, p => p.Type == borrowed);
        }

        Assert.Contains(all, p => p.FollowUp is not null);
    }

    [Fact]
    public void Phases_OutOfOrder_AreRejected()
    {
        const string json = """
            { "bosses": [ { "id": "b", "displayName": "B", "wave": 10, "hp": 100, "speed": 1, "radius": 40,
              "contactDamage": 0, "contactCooldown": 1, "xp": 1, "scoreValue": 1, "addsBudgetFraction": 0.3,
              "idleSeconds": 1, "patterns": [ { "type": "teleport", "telegraphSeconds": 0.8 } ],
              "phases": [
                { "hpFraction": 0.33, "transitionSeconds": 1, "patterns": [ { "type": "teleport", "telegraphSeconds": 0.8 } ] },
                { "hpFraction": 0.66, "transitionSeconds": 1, "patterns": [ { "type": "teleport", "telegraphSeconds": 0.8 } ] }
              ] } ] }
            """;

        var error = Assert.Throws<DataLoadException>(() => DataLoader.ParseBosses(json, "bosses.json"));

        Assert.Equal("bosses[0].phases[1].hpFraction", error.Field);
    }

    [Fact]
    public void Boss_ChangesPhase_AtItsHpThreshold_UnhurtableWhileItDoes()
    {
        var phases = new List<BossPhaseConfig>
        {
            new() { HpFraction = 0.5f, TransitionSeconds = 1f, Patterns = new List<BossPatternConfig> { Pattern(BossPatternType.Summon) } },
        };
        var world = new World(Pattern(BossPatternType.LineCharge), phases: phases);
        world.AddPlayer(new Vector2(1800, 1000));
        var boss = world.SpawnBoss(new Vector2(1000, 1000));
        world.Run(Step);
        Assert.Equal(0, world.Bosses.Phase);

        boss.Hit(60f, 0);
        world.Run(Step);
        Assert.Equal(1, world.Bosses.Phase);
        Assert.True(world.Bosses.IsChangingPhase);

        boss.Hit(30f, 0);
        Assert.Equal(40f, boss.Hp, 3);

        world.Run(1.1f);
        Assert.False(world.Bosses.IsChangingPhase);
        boss.Hit(10f, 0);
        Assert.Equal(30f, boss.Hp, 3);

        // The new phase's patterns: it summons now instead of charging.
        world.Run(2f);
        Assert.True(world.Enemies.Count > 1);
    }

    [Fact]
    public void LaserSweep_CutsAcrossThePlayer_FromOneSide()
    {
        var laser = new BossPatternConfig
        {
            Type = BossPatternType.LaserSweep, TelegraphSeconds = 1f, Damage = 70, Distance = 1400, Width = 16,
            Seconds = 1.6f, SpinDegreesPerSecond = 70,
        };
        var world = new World(laser);
        var player = world.AddPlayer(new Vector2(1600, 1000));
        world.SpawnBoss(new Vector2(1000, 1000));

        // The warning starts after 1 s; the beam does not touch the player yet.
        world.Run(1.5f);
        Assert.Equal(BossPatternType.LaserSweep, world.Bosses.CurrentPattern);
        Assert.Equal(100f, player.Hp);

        // It cuts once, for its full damage, however long it takes to cross the player.
        world.Run(2.5f);
        Assert.Equal(30f, player.Hp);
    }

    [Fact]
    public void LeapSlam_JumpsOntoWhereThePlayerStood_AndHurtsOnlyThoseNearTheLanding()
    {
        var leap = new BossPatternConfig
        {
            Type = BossPatternType.LeapSlam, TelegraphSeconds = 1f, Damage = 35, Radius = 170, Distance = 650, Seconds = 0.7f,
        };
        var world = new World(leap);
        var near = world.AddPlayer(new Vector2(1400, 1000));
        var far = world.AddPlayer(new Vector2(1000, 1500));
        var boss = world.SpawnBoss(new Vector2(1000, 1000));

        // Warning from 1 s to 2 s, then 0.7 s in the air.
        world.Run(1.9f);
        Assert.Equal(BossPatternType.LeapSlam, world.Bosses.CurrentPattern);
        Assert.Equal(0f, boss.Elevation);
        world.Run(0.4f);
        Assert.True(boss.Elevation > 0f);
        Assert.Equal(100f, near.Hp);

        world.Run(0.6f);
        Assert.Equal(0f, boss.Elevation);
        Assert.Equal(1400f - 50f - near.Radius, boss.Position.X, 1);
        Assert.Equal(65f, near.Hp);
        Assert.Equal(100f, far.Hp);
    }

    [Fact]
    public void MissileRain_HitsTheMarkedSpots_OnlyWhenTheMissilesLand()
    {
        var missiles = new BossPatternConfig
        {
            Type = BossPatternType.MissileRain, TelegraphSeconds = 1f, Damage = 25, Radius = 110, Count = 1,
            SpawnRadius = 0, Seconds = 0.8f,
        };
        var world = new World(missiles);
        var player = world.AddPlayer(new Vector2(1600, 1000));
        world.SpawnBoss(new Vector2(1000, 1000));
        var impacts = 0;
        world.Bosses.Impact = (_, _) => impacts++;

        // Warning from 1 s to 2 s, then 0.8 s of falling.
        world.Run(2.6f);
        Assert.Equal(100f, player.Hp);
        Assert.Equal(0, impacts);

        world.Run(0.4f);
        Assert.Equal(75f, player.Hp);
        Assert.Equal(1, impacts);
    }

    [Fact]
    public void FireballStream_ThrowsALongLine_ThatTurnsSlowlyAfterThePlayer()
    {
        var stream = new BossPatternConfig
        {
            Type = BossPatternType.FireballStream, TelegraphSeconds = 0.7f, Damage = 10, Seconds = 1f, Interval = 0.1f,
            SpinDegreesPerSecond = 60, ProjectileSpeed = 420, ProjectileRadius = 12, ProjectileRange = 1000,
        };
        var world = new World(stream);
        var first = world.AddPlayer(new Vector2(1600, 1000));
        world.AddPlayer(new Vector2(1000, 1700));
        var boss = world.SpawnBoss(new Vector2(1000, 1000));

        world.Run(1.7f);
        Assert.Equal(BossPatternType.FireballStream, world.Bosses.CurrentPattern);

        // The nearer player falls, so it turns a quarter round to the other: only 60 degrees a second.
        first.TakeDamage(1000f);
        world.Run(0.5f);
        Assert.InRange(MathHelper.ToDegrees(boss.Facing), 20f, 40f);

        world.Run(0.6f);
        Assert.InRange(world.Fireballs.FireballCount, 9, 11);
    }

    [Fact]
    public void Warning_TurnsLethal_ForAHitThatWouldKill()
    {
        var charge = Charge(120);
        var world = new World(charge);
        var player = world.AddPlayer(new Vector2(1500, 1000));
        world.SpawnBoss(new Vector2(1000, 1000));

        world.Run(1.2f);
        Assert.Equal(BossPatternType.LineCharge, world.Bosses.CurrentPattern);
        Assert.True(world.Bosses.IsLethalWarning);
        Assert.True(Vector2.Distance(world.Bosses.LethalPoint, new Vector2(1000, 1000)) > 100f);
    }

    [Fact]
    public void Warning_IsLethal_ForAnOrdinaryHit_OnlyWhenThePlayerIsLow()
    {
        var full = new World(Charge(30));
        full.AddPlayer(new Vector2(1500, 1000));
        full.SpawnBoss(new Vector2(1000, 1000));
        full.Run(1.2f);
        Assert.False(full.Bosses.IsLethalWarning);

        var low = new World(Charge(30));
        var hurt = low.AddPlayer(new Vector2(1500, 1000));
        hurt.TakeDamage(80f);
        low.SpawnBoss(new Vector2(1000, 1000));
        low.Run(1.2f);
        Assert.True(low.Bosses.IsLethalWarning);
    }

    [Fact]
    public void Aggro_ThePlayerDealingDamage_TakesTheBossOver_FromAPassiveOneStandingCloser()
    {
        var world = new World(Haste(), aggro: Aggro());
        world.HpMultiplier = 1000f;
        world.AddPlayer(new Vector2(1150, 1000));
        world.AddPlayer(new Vector2(1900, 1000));
        var boss = world.SpawnBoss(new Vector2(1000, 1000));

        world.Run(0.1f);
        Assert.Equal(0, world.Bosses.TargetIndex);

        // The far player keeps shooting (about 200 damage a second); the near one does nothing.
        for (var i = 0; i < 60 * 12; i++)
        {
            boss.Hit(200f / 60f, 1);
            world.Run(1f / 60f);
        }

        Assert.Equal(1, world.Bosses.TargetIndex);
        Assert.True(world.Bosses.GetThreat(1) > world.Bosses.GetThreat(0));
    }

    [Fact]
    public void Aggro_TheBossStaysCommitted_ForItsMinimumTime_EvenWhenOutDamaged()
    {
        var world = new World(Haste(), aggro: Aggro());
        world.HpMultiplier = 1000f;
        world.AddPlayer(new Vector2(1150, 1000));
        world.AddPlayer(new Vector2(1900, 1000));
        var boss = world.SpawnBoss(new Vector2(1000, 1000));
        world.Run(0.1f);

        boss.Hit(5000f, 1);
        world.Run(3f);

        Assert.Equal(0, world.Bosses.TargetIndex);
    }

    [Fact]
    public void FarthestTarget_Attack_GoesForThePlayerFarthestAway()
    {
        var missiles = new BossPatternConfig
        {
            Type = BossPatternType.MissileRain, Target = BossTargetChoice.Farthest, TelegraphSeconds = 1f, Damage = 25,
            Radius = 110, Count = 1, SpawnRadius = 0, Seconds = 0.8f,
        };
        var world = new World(missiles);
        var near = world.AddPlayer(new Vector2(1200, 1000));
        var far = world.AddPlayer(new Vector2(1000, 1800));
        world.SpawnBoss(new Vector2(1000, 1000));

        world.Run(3f);

        Assert.Equal(100f, near.Hp);
        Assert.Equal(75f, far.Hp);
    }

    private static BossAggroConfig Aggro()
    {
        return new BossAggroConfig
        {
            MinTargetSeconds = 5, MaxTargetSeconds = 9, ThreatPerDamage = 1, ProximityThreatPerSecond = 120,
            ProximityRange = 500, ThreatDecayPerSecond = 0.15f, SwitchMargin = 1.3f,
        };
    }

    // An attack that does nothing to the players, so only targeting is tested.
    private static BossPatternConfig Haste()
    {
        return new BossPatternConfig
        {
            Type = BossPatternType.Haste, TelegraphSeconds = 0.6f, SpeedMultiplier = 1f, Seconds = 1f,
        };
    }

    [Fact]
    public void BossBody_DrawsBackDuringTheWarning_AndCommitsWhileItActs()
    {
        var world = new World(Charge(40));
        world.AddPlayer(new Vector2(1500, 1000));
        var boss = world.SpawnBoss(new Vector2(1000, 1000));

        world.Run(0.5f);
        Assert.True(boss.BossWindup < 0.05f);

        // Most of the way through the 0.9 s warning.
        world.Run(1.25f);
        Assert.True(boss.BossWindup > 0.4f);
        Assert.True(boss.BossAction < 0.1f);

        // Charging.
        world.Run(0.35f);
        Assert.True(boss.BossAction > 0.5f);
    }

    [Fact]
    public void BossArriving_IsAnnouncedByName_ForAMoment()
    {
        var shipped = DataLoader.LoadAll(DataLoader.DefaultDirectory);
        var data = new GameData(
            TestData.Game(startWave: 10), shipped.Weapons, shipped.Enemies, shipped.Waves, shipped.Upgrades, shipped.Bosses);
        var input = new FakeInput();
        var scene = new GameplayScene(data, new IPlayerInput[] { input }, new NullAudioManager());

        var frames = 0;
        while (!scene.IsShowingBossIntro && frames < 60 * 60)
        {
            scene.Update(Step);
            scene.Players[0].HealToFull();
            frames++;
        }

        Assert.True(scene.IsShowingBossIntro);
        for (var i = 0; i < 60 * 4; i++)
        {
            scene.Update(Step);
            scene.Players[0].HealToFull();
        }

        Assert.False(scene.IsShowingBossIntro);
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

    private static BossPatternConfig Charge(float damage)
    {
        return new BossPatternConfig
        {
            Type = BossPatternType.LineCharge, TelegraphSeconds = 0.9f, Damage = damage, Distance = 800, Speed = 1100,
        };
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

        public World(
            BossPatternConfig pattern, float turnDegreesPerSecond = 0f, float armorGap = 0f, float armorThickness = 0f,
            float speed = 0f, List<BossPatternConfig>? patterns = null, BossPatternOrder order = BossPatternOrder.Random,
            List<BossPhaseConfig>? phases = null, BossAggroConfig? aggro = null)
        {
            _boss = new BossConfig
            {
                Id = "boss", DisplayName = "Boss", Wave = 10, Hp = 100, Speed = speed, Radius = 50, ContactDamage = 0,
                ContactCooldown = 1, Xp = 1, ScoreValue = 1, AddsBudgetFraction = 0.3f, IdleSeconds = 1,
                TurnDegreesPerSecond = turnDegreesPerSecond, FrontArmorArcDegrees = 90, FrontArmorDamageMult = 0.2f,
                FrontArmorGap = armorGap, FrontArmorThickness = armorThickness,
                Patterns = patterns ?? new List<BossPatternConfig> { pattern },
                PatternOrder = order,
                Phases = phases ?? new List<BossPhaseConfig>(),
            };
            var data = new GameData(
                TestData.Game(bossAggro: aggro), new[] { TestData.Pistol() }, new[] { TestData.Zombie() }, TestData.Waves(), TestData.Upgrades(),
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

        public Enemy SpawnBossInto(EnemySystem enemies, Vector2 position)
        {
            return enemies.Spawn(_boss.AsEnemy, position);
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
