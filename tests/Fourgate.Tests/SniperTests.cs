using Fourgate.Core.Audio;
using Fourgate.Core.Data;
using Fourgate.Entities;
using Fourgate.Entities.Enemies;
using Fourgate.Systems;
using Fourgate.Weapons;
using Microsoft.Xna.Framework;

namespace Fourgate.Tests;

public class SniperTests
{
    private const float Step = 1f / 60f;

    private static readonly Arena TestArena = new(TestData.Arena());
    private static readonly ProjectileConfig Projectile = new() { Speed = 9000, Radius = 4, Range = 2400 };

    [Fact]
    public void Overkill_CarriesTheLeftoverDamageToTheNextEnemyInLine()
    {
        var (shots, enemies) = Line(80, 80, 80, 80);

        // 300 damage: kills three 80-HP zombies and leaves the fourth with 20 HP.
        Fire(shots, 300, overkill: true);

        Assert.False(enemies[0].IsAlive);
        Assert.False(enemies[1].IsAlive);
        Assert.False(enemies[2].IsAlive);
        Assert.True(enemies[3].IsAlive);
        Assert.Equal(20f, enemies[3].Hp, 3);
    }

    [Fact]
    public void Overkill_StopsInsideAnEnemyItDoesNotKill()
    {
        var (shots, enemies) = Line(80, 400, 80);

        Fire(shots, 300, overkill: true);

        Assert.False(enemies[0].IsAlive);
        Assert.Equal(180f, enemies[1].Hp, 3);
        Assert.Equal(80f, enemies[2].Hp);
    }

    [Fact]
    public void WithoutOverkill_TheBulletStopsAtTheFirstKill()
    {
        var (shots, enemies) = Line(80, 80);

        Fire(shots, 300, overkill: false);

        Assert.False(enemies[0].IsAlive);
        Assert.Equal(80f, enemies[1].Hp);
    }

    [Fact]
    public void Overkill_DoesNothingAgainstADummyThatCannotDie()
    {
        var shots = new ShotSystem(TestArena);
        var dummy = new PracticeTarget(new Vector2(1800, 1500), 20);
        var behind = new PracticeTarget(new Vector2(2000, 1500), 20);
        shots.AddTarget(dummy);
        shots.AddTarget(behind);

        shots.FireProjectile(new Vector2(1500, 1500), Vector2.UnitX, Projectile, 300, 0, overkill: true);
        Run(shots, 20);

        Assert.Equal(1, dummy.HitCount);
        Assert.Equal(0, behind.HitCount);
    }

    [Fact]
    public void LevelTen_TurnsOnOverkill()
    {
        var config = TestData.Pistol(
            levelTen: new WeaponLevelTenConfig
            {
                Description = new List<string> { "Overkill" },
                Abilities = new List<WeaponAbilityConfig> { new() { Id = WeaponAbility.Overkill } },
            });
        var sniper = new Weapon(config, TestData.Game(), new NullAudioManager());
        var input = new FakeInput { FirePressed = true, FireHeld = true };
        var handler = new FakeShotHandler();

        sniper.SetLevel(9);
        sniper.Update(Step, input, Vector2.Zero, Vector2.UnitX, 0, handler);
        Assert.False(handler.LastOverkill);

        sniper.SetLevel(10);
        for (var i = 0; i < 60; i++)
        {
            sniper.Update(Step, input, Vector2.Zero, Vector2.UnitX, 0, handler);
        }

        Assert.True(handler.LastOverkill);
    }

    [Fact]
    public void ShippedSniper_IsASlowHeavySingleTargetGunOnKeySix()
    {
        var data = DataLoader.LoadAll(DataLoader.DefaultDirectory);
        var sniper = data.GetWeapon("sniper");
        var marksman = data.GetWeapon("marksman");

        var stats = WeaponStats.Compute(sniper, 1);
        Assert.Equal(6, sniper.Slot);
        Assert.Equal(11, sniper.UnlockWave);
        Assert.True(stats.Automatic);
        Assert.Equal(0, stats.Pierce);
        Assert.True(stats.Damage > WeaponStats.Compute(marksman, 1).Damage);
        Assert.True(stats.FireRate < WeaponStats.Compute(marksman, 1).FireRate);
        Assert.False(WeaponStats.Compute(sniper, 9).Overkill);
        Assert.True(WeaponStats.Compute(sniper, 10).Overkill);
    }

    // Zombies in a row along the x axis, 120 units apart, in front of the muzzle.
    private static (ShotSystem Shots, List<Enemy> Enemies) Line(params float[] hp)
    {
        var enemySystem = new EnemySystem(new NullAudioManager());
        var shots = new ShotSystem(TestArena);
        shots.AddSource(enemySystem);

        var enemies = new List<Enemy>();
        for (var i = 0; i < hp.Length; i++)
        {
            var config = TestData.Zombie();
            var withHp = new EnemyConfig
            {
                Id = config.Id,
                DisplayName = config.DisplayName,
                Hp = hp[i],
                Speed = config.Speed,
                Radius = config.Radius,
                ContactDamage = config.ContactDamage,
                ContactCooldown = config.ContactCooldown,
                Xp = config.Xp,
                ScoreValue = config.ScoreValue,
                Cost = config.Cost,
                UnlockWave = config.UnlockWave,
                DropChance = config.DropChance,
            };
            enemies.Add(enemySystem.Spawn(withHp, new Vector2(1800 + i * 120, 1500)));
        }

        // One update so the enemies are in the spatial hash; nobody to chase, so they stay put.
        enemySystem.Update(Step, new List<Player>(), TestArena);
        return (shots, enemies);
    }

    private static void Fire(ShotSystem shots, float damage, bool overkill)
    {
        shots.FireProjectile(new Vector2(1500, 1500), Vector2.UnitX, Projectile, damage, 0, overkill: overkill);
        Run(shots, 20);
    }

    private static void Run(ShotSystem shots, int steps)
    {
        for (var i = 0; i < steps; i++)
        {
            shots.Update(Step);
        }
    }
}
