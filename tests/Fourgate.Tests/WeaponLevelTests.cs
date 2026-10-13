using Fourgate.Core.Audio;
using Fourgate.Core.Data;
using Fourgate.Core.Rng;
using Fourgate.Entities;
using Fourgate.Systems;
using Fourgate.Weapons;
using Microsoft.Xna.Framework;

namespace Fourgate.Tests;

public class WeaponLevelTests
{
    private const float Step = 1f / 60f;

    private static readonly Arena TestArena = new(TestData.Arena());
    private static readonly Vector2 Center = new(1500, 1500);
    private static readonly ProjectileConfig Projectile = new() { Speed = 6000, Radius = 4, Range = 1100 };

    [Fact]
    public void LevelOne_HasTheBaseStats()
    {
        var stats = WeaponStats.Compute(LeveledPistol(), 1);

        Assert.Equal(15f, stats.Damage);
        Assert.Equal(3f, stats.FireRate);
        Assert.Equal(12, stats.MagSize);
        Assert.Equal(0.8f, stats.ReloadTime);
        Assert.Equal(1100f, stats.Range);
        Assert.Equal(0, stats.Pierce);
        Assert.False(stats.DualWield);
    }

    [Fact]
    public void Levels_CompoundDamage_AndAddTheirOwnBonus()
    {
        var config = LeveledPistol();

        var levelThree = WeaponStats.Compute(config, 3);
        Assert.Equal(15f * 1.08f * 1.08f, levelThree.Damage, 3);
        Assert.Equal(3.3f, levelThree.FireRate, 3);
        Assert.Equal(14, levelThree.MagSize);

        var levelFive = WeaponStats.Compute(config, 5);
        Assert.Equal(0.72f, levelFive.ReloadTime, 3);
        Assert.Equal(1210f, levelFive.Range, 1);
    }

    [Fact]
    public void LevelTen_GrantsTheAbilities_AndTheFinalBump()
    {
        var config = LeveledPistol();

        var nine = WeaponStats.Compute(config, 9);
        var ten = WeaponStats.Compute(config, 10);

        Assert.Equal(0, nine.Pierce);
        Assert.False(nine.DualWield);
        Assert.Equal(1, ten.Pierce);
        Assert.True(ten.DualWield);
        Assert.Equal(nine.Damage * 1.08f, ten.Damage, 3);
    }

    [Fact]
    public void Level_IsClampedToTheValidRange()
    {
        var config = LeveledPistol();

        Assert.Equal(WeaponStats.Compute(config, 1), WeaponStats.Compute(config, -3));
        Assert.Equal(WeaponStats.Compute(config, 10), WeaponStats.Compute(config, 40));
    }

    [Fact]
    public void ShippedWeapons_GetStrongerEveryLevel_AndHaveALevelTenAbility()
    {
        var data = DataLoader.LoadAll(DataLoader.DefaultDirectory);

        foreach (var weapon in data.Weapons)
        {
            for (var level = 2; level <= WeaponConfig.MaxLevel; level++)
            {
                Assert.True(
                    WeaponStats.Compute(weapon, level).Damage > WeaponStats.Compute(weapon, level - 1).Damage,
                    $"{weapon.Id} level {level} does not raise damage.");
            }

            Assert.NotNull(weapon.LevelTen);
            Assert.NotEmpty(weapon.LevelTen!.Abilities);
        }
    }

    [Fact]
    public void DescribeLevel_ListsWhatTheLevelAdds()
    {
        var config = LeveledPistol();
        var lines = new List<string>();

        WeaponStats.DescribeLevel(config, 2, lines);
        Assert.Equal(new[] { "+8% damage", "+10% fire rate" }, lines);

        lines.Clear();
        WeaponStats.DescribeLevel(config, 4, lines);
        Assert.Equal(new[] { "+8% damage", "-10% reload time" }, lines);

        lines.Clear();
        WeaponStats.DescribeLevel(config, 10, lines);
        Assert.Equal(new[] { "Dual pistols", "Bullets pierce 1 enemy" }, lines);
    }

    [Fact]
    public void Weapon_UsesItsLeveledStats()
    {
        var weapon = new Weapon(LeveledPistol(), TestData.Game(), new NullAudioManager());
        var shots = new FakeShotHandler();
        var input = new FakeInput { FirePressed = true, FireHeld = true };
        weapon.SetLevel(3);

        weapon.Update(Step, input, Vector2.Zero, Vector2.UnitX, 0, shots);

        Assert.Equal(15f * 1.08f * 1.08f, shots.LastDamage, 3);
        Assert.Equal(11, weapon.Magazine);

        // The bigger magazine is filled by the next reload, not handed out for free.
        input.FirePressed = false;
        input.FireHeld = false;
        input.ReloadPressed = true;
        weapon.Update(Step, input, Vector2.Zero, Vector2.UnitX, 0, shots);
        input.ReloadPressed = false;
        for (var i = 0; i < 60; i++)
        {
            weapon.Update(Step, input, Vector2.Zero, Vector2.UnitX, 0, shots);
        }

        Assert.Equal(14, weapon.Magazine);
    }

    [Fact]
    public void DualWield_FiresTwoBulletsSideBySide_ForOneRound()
    {
        var weapon = new Weapon(LeveledPistol(), TestData.Game(), new NullAudioManager());
        var shots = new FakeShotHandler();
        var input = new FakeInput { FirePressed = true, FireHeld = true };
        weapon.SetLevel(10);
        var magazineBefore = weapon.Magazine;

        weapon.Update(Step, input, new Vector2(100, 100), Vector2.UnitX, 0, shots);

        Assert.Equal(2, shots.Count);
        Assert.Equal(magazineBefore - 1, weapon.Magazine);
        Assert.Equal(100f, shots.LastOrigin.X);
        Assert.NotEqual(100f, shots.LastOrigin.Y);
        Assert.Equal(1, shots.LastPierce);
    }

    [Fact]
    public void PlayerWeaponLevel_IsKeptPerWeapon_AndAppliedWhenTheWeaponArrives()
    {
        var player = CreatePlayer();
        var pistol = new Weapon(LeveledPistol(), TestData.Game(), new NullAudioManager());
        player.GiveWeapon(pistol);

        player.LevelUpWeapon("pistol");
        player.LevelUpWeapon("smg");
        player.LevelUpWeapon("smg");

        Assert.Equal(2, pistol.Level);
        Assert.Equal(3, player.GetWeaponLevel("smg"));

        // The SMG is not carried yet; it arrives at the level already earned.
        var smg = new Weapon(TestData.Pistol(id: "smg", slot: 2), TestData.Game(), new NullAudioManager());
        player.GiveWeapon(smg);
        Assert.Equal(3, smg.Level);
    }

    [Fact]
    public void PlayerWeaponLevel_StopsAtTheMaximum()
    {
        var player = CreatePlayer();

        for (var i = 0; i < 20; i++)
        {
            player.LevelUpWeapon("pistol");
        }

        Assert.Equal(10, player.GetWeaponLevel("pistol"));
    }

    [Fact]
    public void UpgradeOffer_IncludesUnlockedWeapons_ButNotLockedOrMaxedOnes()
    {
        var pistol = LeveledPistol();
        var smg = TestData.Pistol(id: "smg", slot: 2);
        var unlocks = TestData.Unlocks();
        unlocks.Unlock(pistol);
        var config = new UpgradesConfig
        {
            CardsPerPick = 3,
            Fallback = new UpgradeFallbackConfig { DisplayName = "Second Wind", Description = "Restore all HP" },
            Upgrades = new List<UpgradeConfig>(),
        };
        var upgrades = new UpgradeSystem(config, unlocks, new GameRandom(2), new NullAudioManager());
        var player = CreatePlayer();
        player.Progression.AddPick();
        var players = new List<Player> { player };

        // Only the pistol is unlocked: the SMG must not be offered.
        upgrades.OpenOffers(players);
        var card = Assert.Single(upgrades.GetOffer(0));
        Assert.Equal("pistol", card.Weapon!.Id);
        Assert.Equal(new[] { "+8% damage", "+10% fire rate" }, card.Lines);

        upgrades.Choose(player, 0, 0);
        Assert.Equal(2, player.GetWeaponLevel("pistol"));
        Assert.Equal(0, player.Progression.BankedPicks);

        // Once maxed, the pistol leaves the pool and the newly unlocked SMG takes its place.
        for (var i = 0; i < 10; i++)
        {
            player.LevelUpWeapon("pistol");
        }

        unlocks.Unlock(smg);
        player.Progression.AddPick();
        upgrades.OpenOffers(players);
        Assert.Equal("smg", Assert.Single(upgrades.GetOffer(0)).Weapon!.Id);
    }

    [Fact]
    public void PiercingBullet_HitsTheNextEnemyToo_ThenStops()
    {
        var shots = new ShotSystem(TestArena);
        var first = AddTarget(shots, 1800);
        var second = AddTarget(shots, 2000);
        var third = AddTarget(shots, 2200);

        shots.FireProjectile(Center, Vector2.UnitX, Projectile, 15, 0, pierce: 1);
        Run(shots, 20);

        Assert.Equal(1, first.HitCount);
        Assert.Equal(1, second.HitCount);
        Assert.Equal(0, third.HitCount);
        Assert.Equal(0, shots.BulletCount);
    }

    [Fact]
    public void PiercingBullet_HitsEachEnemyOnlyOnce()
    {
        var shots = new ShotSystem(TestArena);

        // A slow bullet spends several frames inside the target.
        var target = AddTarget(shots, 1600);
        var slow = new ProjectileConfig { Speed = 300, Radius = 4, Range = 400 };

        shots.FireProjectile(Center, Vector2.UnitX, slow, 15, 0, pierce: 3);
        Run(shots, 120);

        Assert.Equal(1, target.HitCount);
    }

    [Fact]
    public void RicochetBullet_BouncesFromAnEnemy_ToTheNearestOtherEnemy()
    {
        var shots = new ShotSystem(TestArena);
        var first = AddTarget(shots, 1800);
        var near = new PracticeTarget(new Vector2(1800, 1700), 20);
        var farther = new PracticeTarget(new Vector2(1800, 1200), 20);
        shots.AddTarget(near);
        shots.AddTarget(farther);

        shots.FireProjectile(Center, Vector2.UnitX, Projectile, 15, 0, ricochets: 1, ricochetRadius: 350);
        Run(shots, 30);

        Assert.Equal(1, first.HitCount);
        Assert.Equal(1, near.HitCount);
        Assert.Equal(0, farther.HitCount);
        Assert.Equal(0, shots.BulletCount);
    }

    [Fact]
    public void RicochetBullet_WithNoEnemyInRadius_StopsAtTheFirstOne()
    {
        var shots = new ShotSystem(TestArena);
        var first = AddTarget(shots, 1800);
        var tooFar = new PracticeTarget(new Vector2(1800, 1900), 20);
        shots.AddTarget(tooFar);

        shots.FireProjectile(Center, Vector2.UnitX, Projectile, 15, 0, ricochets: 1, ricochetRadius: 350);
        Run(shots, 30);

        Assert.Equal(1, first.HitCount);
        Assert.Equal(0, tooFar.HitCount);
    }

    [Fact]
    public void RicochetBullet_BouncesOnlyOnce_AndNeverBackToTheSameEnemy()
    {
        var shots = new ShotSystem(TestArena);
        var first = AddTarget(shots, 1800);
        var second = new PracticeTarget(new Vector2(1800, 1650), 20);
        var third = new PracticeTarget(new Vector2(1800, 1800), 20);
        shots.AddTarget(second);
        shots.AddTarget(third);

        shots.FireProjectile(Center, Vector2.UnitX, Projectile, 15, 0, ricochets: 1, ricochetRadius: 350);
        Run(shots, 30);

        Assert.Equal(1, first.HitCount);
        Assert.Equal(1, second.HitCount);
        Assert.Equal(0, third.HitCount);
    }

    [Fact]
    public void RicochetBullet_ReachesItsBounceTarget_EvenAtTheEndOfItsRange()
    {
        var shots = new ShotSystem(TestArena);

        // The first target sits at the very end of the 1100 range.
        var first = AddTarget(shots, 2610);
        var second = new PracticeTarget(new Vector2(2610, 1800), 20);
        shots.AddTarget(second);

        shots.FireProjectile(Center, Vector2.UnitX, Projectile, 15, 0, ricochets: 1, ricochetRadius: 350);
        Run(shots, 30);

        Assert.Equal(1, first.HitCount);
        Assert.Equal(1, second.HitCount);
    }

    [Fact]
    public void RicochetDamageMultiplier_AppliesToTheBouncedHit()
    {
        var enemies = new EnemySystem(new NullAudioManager());
        var shots = new ShotSystem(TestArena);
        shots.AddSource(enemies);
        var players = new List<Player> { CreatePlayer() };
        var first = enemies.Spawn(TestData.Zombie(), new Vector2(2000, 1500));
        var second = enemies.Spawn(TestData.Zombie(), new Vector2(2000, 1700));
        enemies.Update(Step, players, TestArena);

        shots.FireProjectile(
            new Vector2(1540, 1500), Vector2.UnitX, Projectile, 20, 0,
            ricochets: 1, ricochetRadius: 350, ricochetDamageMult: 0.5f);
        Run(shots, 30);

        Assert.Equal(40f, first.Hp);
        Assert.Equal(50f, second.Hp);
    }

    [Fact]
    public void ShippedSmg_AtLevelTen_RicochetsToEnemies()
    {
        var smg = DataLoader.LoadAll(DataLoader.DefaultDirectory).GetWeapon("smg");

        var stats = WeaponStats.Compute(smg, 10);

        Assert.Equal(1, stats.Ricochets);
        Assert.True(stats.RicochetRadius > 0f);
        Assert.Equal(0, WeaponStats.Compute(smg, 9).Ricochets);
    }

    [Fact]
    public void RicochetBullet_BouncesOffTheWall_AndCanHitOnTheWayBack()
    {
        var shots = new ShotSystem(TestArena);

        // Fired at the right wall from 200 units away; the target sits behind the muzzle.
        var behind = AddTarget(shots, 2600);
        shots.FireProjectile(new Vector2(2800, 1500), Vector2.UnitX, Projectile, 15, 0, ricochets: 1);
        Run(shots, 30);

        Assert.Equal(1, behind.HitCount);
    }

    [Fact]
    public void RicochetBullet_BouncesOnlyAsOftenAsAllowed_AndRangeStillLimitsIt()
    {
        var shots = new ShotSystem(TestArena);
        var far = AddTarget(shots, 1500);

        // 200 to the wall and back leaves 700 of range: not enough to reach x = 1500.
        shots.FireProjectile(new Vector2(2800, 1500), Vector2.UnitX, Projectile, 15, 0, ricochets: 1);
        Run(shots, 60);

        Assert.Equal(0, far.HitCount);
        Assert.Equal(0, shots.BulletCount);
    }

    [Fact]
    public void BulletWithoutRicochet_StopsAtTheWall()
    {
        var shots = new ShotSystem(TestArena);
        var behind = AddTarget(shots, 2600);

        shots.FireProjectile(new Vector2(2800, 1500), Vector2.UnitX, Projectile, 15, 0);
        Run(shots, 30);

        Assert.Equal(0, behind.HitCount);
    }

    [Fact]
    public void RicochetBullet_InACorner_ComesStraightBack()
    {
        var shots = new ShotSystem(TestArena);
        var diagonal = Vector2.Normalize(new Vector2(1, 1));
        var onThePath = new PracticeTarget(new Vector2(2700, 2700), 20);
        shots.AddTarget(onThePath);

        shots.FireProjectile(new Vector2(2900, 2900), diagonal, Projectile, 15, 0, ricochets: 1);
        Run(shots, 30);

        Assert.Equal(1, onThePath.HitCount);
    }

    private static WeaponConfig LeveledPistol()
    {
        return TestData.Pistol(
            levels: new List<WeaponLevelConfig>
            {
                new() { Level = 2, DamageMult = 1.08f, FireRateMult = 1.10f },
                new() { Level = 3, DamageMult = 1.08f, MagSizeBonus = 2 },
                new() { Level = 4, DamageMult = 1.08f, ReloadTimeMult = 0.90f },
                new() { Level = 5, DamageMult = 1.08f, RangeMult = 1.10f },
            },
            levelTen: new WeaponLevelTenConfig
            {
                Description = new List<string> { "Dual pistols", "Bullets pierce 1 enemy" },
                DamageMult = 1.08f,
                Abilities = new List<WeaponAbilityConfig>
                {
                    new() { Id = WeaponAbility.DualWield },
                    new() { Id = WeaponAbility.Pierce, Count = 1 },
                },
            });
    }

    private static Player CreatePlayer()
    {
        return new Player(0, new FakeInput(), TestData.Game().Player, new NullAudioManager(), Center);
    }

    private static PracticeTarget AddTarget(ShotSystem shots, float x)
    {
        var target = new PracticeTarget(new Vector2(x, 1500), 20);
        shots.AddTarget(target);
        return target;
    }

    private static void Run(ShotSystem shots, int steps)
    {
        for (var i = 0; i < steps; i++)
        {
            shots.Update(Step);
        }
    }
}
