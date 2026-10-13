using Fourgate.Core.Audio;
using Fourgate.Core.Data;
using Fourgate.Core.Rng;
using Fourgate.Entities;
using Fourgate.Entities.Enemies;
using Fourgate.Entities.Pickups;
using Fourgate.Systems;
using Fourgate.Weapons;
using Microsoft.Xna.Framework;

namespace Fourgate.Tests;

public class DropTests
{
    private const float Step = 1f / 60f;

    private static readonly CratesConfig Config = TestData.Crates();
    private static readonly Vector2 Center = new(1500, 1500);

    [Theory]
    [InlineData(0.01f, 0.99f, false, CrateType.Upgrade)]
    [InlineData(0.50f, 0.10f, false, CrateType.Health)]
    [InlineData(0.50f, 0.20f, false, CrateType.Ammo)]
    [InlineData(0.50f, 0.20f, true, CrateType.Health)]
    [InlineData(0.50f, 0.59f, true, CrateType.Health)]
    [InlineData(0.50f, 0.61f, true, CrateType.Ammo)]
    public void ChooseType_FollowsTheChances(float upgradeRoll, float healthRoll, bool lowHp, CrateType expected)
    {
        Assert.Equal(expected, DropSystem.ChooseType(Config, lowHp, upgradeRoll, healthRoll));
    }

    [Fact]
    public void Ammo_GoesMostlyToTheLowestWeapon_WithASmallShareForTheOthers()
    {
        var smg = LimitedWeapon("smg", reserveMax: 240);
        var shotgun = LimitedWeapon("shotgun", reserveMax: 48);
        Drain(smg, 120);     // 50% left
        Drain(shotgun, 36);  // 25% left: the lowest
        var weapons = new List<Weapon> { Pistol(), smg, shotgun };

        Assert.True(AmmoDistribution.Distribute(weapons, 0.35f, 0.08f));

        // Shotgun: 12 + ceil(48 x 0.35) = 29. SMG: 120 + ceil(240 x 0.08) = 140.
        Assert.Equal(29, shotgun.Reserve);
        Assert.Equal(140, smg.Reserve);
    }

    [Fact]
    public void Ammo_NeverExceedsTheMaximumReserve()
    {
        var smg = LimitedWeapon("smg", reserveMax: 240);
        Drain(smg, 10);

        AmmoDistribution.Distribute(new List<Weapon> { smg }, 0.35f, 0.08f);

        Assert.Equal(240, smg.Reserve);
    }

    [Fact]
    public void Ammo_IsOfNoUse_WithOnlyThePistol()
    {
        var weapons = new List<Weapon> { Pistol() };

        Assert.False(AmmoDistribution.CanUse(weapons));
        Assert.False(AmmoDistribution.Distribute(weapons, 0.35f, 0.08f));
    }

    [Fact]
    public void Ammo_SkipsWeaponsThatAreAlreadyFull()
    {
        var full = LimitedWeapon("smg", reserveMax: 240);
        var low = LimitedWeapon("shotgun", reserveMax: 48);
        Drain(low, 40);

        AmmoDistribution.Distribute(new List<Weapon> { full, low }, 0.35f, 0.08f);

        Assert.Equal(240, full.Reserve);
        Assert.Equal(8 + 17, low.Reserve);
    }

    [Fact]
    public void HealthCrate_HealsAndDisappears_WhenAHurtPlayerTouchesIt()
    {
        var drops = CreateDrops();
        var player = CreatePlayer();
        player.TakeDamage(50);
        drops.Spawn(CrateType.Health, Center + new Vector2(30, 0));

        drops.Update(Step, new List<Player> { player });

        Assert.Equal(75f, player.Hp);
        Assert.Equal(0, drops.Count);
    }

    [Fact]
    public void HealthCrate_DoesNotOverheal()
    {
        var drops = CreateDrops();
        var player = CreatePlayer();
        player.TakeDamage(10);
        drops.Spawn(CrateType.Health, Center);

        drops.Update(Step, new List<Player> { player });

        Assert.Equal(100f, player.Hp);
    }

    [Fact]
    public void HealthCrate_StaysOnTheFloor_ForAPlayerAtFullHp()
    {
        var drops = CreateDrops();
        var player = CreatePlayer();
        drops.Spawn(CrateType.Health, Center);

        drops.Update(Step, new List<Player> { player });

        Assert.Equal(1, drops.Count);
    }

    [Fact]
    public void Crate_OutOfReach_IsNotPickedUp()
    {
        var drops = CreateDrops();
        var player = CreatePlayer();
        player.TakeDamage(50);
        drops.Spawn(CrateType.Health, Center + new Vector2(41, 0));

        drops.Update(Step, new List<Player> { player });

        Assert.Equal(50f, player.Hp);
        Assert.Equal(1, drops.Count);
    }

    [Fact]
    public void UpgradeCrate_BanksAPick()
    {
        var drops = CreateDrops();
        var player = CreatePlayer();
        drops.Spawn(CrateType.Upgrade, Center);

        drops.Update(Step, new List<Player> { player });

        Assert.Equal(1, player.Progression.BankedPicks);
        Assert.Equal(0, drops.Count);
    }

    [Fact]
    public void AmmoCrate_RefillsAWeaponThatNeedsIt()
    {
        var drops = CreateDrops();
        var player = CreatePlayer();
        var smg = LimitedWeapon("smg", reserveMax: 240);
        Drain(smg, 200);
        player.GiveWeapon(smg);
        drops.Spawn(CrateType.Ammo, Center);

        drops.Update(Step, new List<Player> { player });

        Assert.Equal(40 + 84, smg.Reserve);
        Assert.Equal(0, drops.Count);
    }

    [Fact]
    public void DeadPlayer_CannotPickUpCrates()
    {
        var drops = CreateDrops();
        var player = CreatePlayer();
        player.TakeDamage(1000);
        drops.Spawn(CrateType.Upgrade, Center);

        drops.Update(Step, new List<Player> { player });

        Assert.Equal(1, drops.Count);
    }

    [Fact]
    public void Crate_DisappearsAfterItsLifetime_AndBlinksNearTheEnd()
    {
        var drops = CreateDrops();
        var crate = drops.Spawn(CrateType.Upgrade, new Vector2(100, 100));
        var nobody = new List<Player>();

        Run(drops, nobody, seconds: 14f);
        Assert.True(crate.IsVisible(Config.BlinkSeconds));

        // Inside the last five seconds it must be hidden on some frames.
        Run(drops, nobody, seconds: 1.5f);
        var hiddenFrames = 0;
        for (var i = 0; i < 60; i++)
        {
            drops.Update(Step, nobody);
            if (!crate.IsVisible(Config.BlinkSeconds))
            {
                hiddenFrames++;
            }
        }

        Assert.InRange(hiddenFrames, 20, 40);
        Assert.Equal(1, drops.Count);

        Run(drops, nobody, seconds: 5.1f);
        Assert.Equal(0, drops.Count);
    }

    [Fact]
    public void Kill_WithCertainDropChance_LeavesACrateWhereTheEnemyDied()
    {
        var drops = CreateDrops();
        var player = CreatePlayer();
        player.TakeDamage(80);
        var death = new EnemyDeath(TestData.Zombie(dropChance: 1f), new Vector2(700, 900), 0);

        // Low HP makes health likely; over many kills there must be at least one crate.
        for (var i = 0; i < 20; i++)
        {
            drops.OnEnemyKilled(death, new List<Player> { player });
        }

        Assert.True(drops.Count > 0);
        Assert.Equal(new Vector2(700, 900), drops[0].Position);
    }

    [Fact]
    public void Kill_WithZeroDropChance_LeavesNothing()
    {
        var drops = CreateDrops();
        var death = new EnemyDeath(TestData.Zombie(dropChance: 0f), new Vector2(700, 900), 0);

        for (var i = 0; i < 100; i++)
        {
            drops.OnEnemyKilled(death, new List<Player> { CreatePlayer() });
        }

        Assert.Equal(0, drops.Count);
    }

    [Fact]
    public void AmmoCrates_AreNotDropped_WhenNobodyCanUseAmmo()
    {
        var drops = CreateDrops();
        var player = CreatePlayer();
        player.GiveWeapon(Pistol());
        var death = new EnemyDeath(TestData.Zombie(dropChance: 1f), new Vector2(700, 900), 0);

        for (var i = 0; i < 300; i++)
        {
            drops.OnEnemyKilled(death, new List<Player> { player });
        }

        for (var i = 0; i < drops.Count; i++)
        {
            Assert.NotEqual(CrateType.Ammo, drops[i].Type);
        }
    }

    [Fact]
    public void LowHp_MakesHealthCratesFarMoreCommon()
    {
        var healthy = CountHealthCrates(playerHp: 100);
        var hurt = CountHealthCrates(playerHp: 20);

        // 15% against 60% of 1000 drops.
        Assert.InRange(healthy, 100, 200);
        Assert.InRange(hurt, 540, 660);
    }

    private static int CountHealthCrates(float playerHp)
    {
        var drops = CreateDrops();
        var player = CreatePlayer();
        player.TakeDamage(100 - playerHp);
        var death = new EnemyDeath(TestData.Zombie(dropChance: 1f), new Vector2(700, 900), 0);

        for (var i = 0; i < 1000; i++)
        {
            drops.OnEnemyKilled(death, new List<Player> { player });
        }

        var health = 0;
        for (var i = 0; i < drops.Count; i++)
        {
            if (drops[i].Type == CrateType.Health)
            {
                health++;
            }
        }

        return health;
    }

    private static DropSystem CreateDrops()
    {
        return new DropSystem(Config, new GameRandom(11), new NullAudioManager());
    }

    private static Player CreatePlayer()
    {
        return new Player(0, new FakeInput(), TestData.Game().Player, new NullAudioManager(), Center);
    }

    private static Weapon Pistol()
    {
        return new Weapon(TestData.Pistol(), TestData.Game(), new NullAudioManager());
    }

    private static Weapon LimitedWeapon(string id, int reserveMax)
    {
        // Reloads off and one shot per frame, so the reserve can be drained quickly in tests.
        var config = TestData.Pistol(
            automatic: true, reserveMax: reserveMax, infiniteReserve: false, fireRate: 60, id: id);
        return new Weapon(config, TestData.Game(reloadsEnabled: false), new NullAudioManager());
    }

    private static void Drain(Weapon weapon, int rounds)
    {
        var input = new FakeInput { FireHeld = true };
        var shots = new FakeShotHandler();
        while (shots.Count < rounds)
        {
            weapon.Update(Step, input, Vector2.Zero, Vector2.UnitX, 0, shots);
        }
    }

    private static void Run(DropSystem drops, List<Player> players, float seconds)
    {
        var steps = (int)MathF.Round(seconds / Step);
        for (var i = 0; i < steps; i++)
        {
            drops.Update(Step, players);
        }
    }
}
