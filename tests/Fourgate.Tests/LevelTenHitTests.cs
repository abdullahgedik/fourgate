using Fourgate.Core.Audio;
using Fourgate.Core.Data;
using Fourgate.Entities;
using Fourgate.Entities.Enemies;
using Fourgate.Systems;
using Fourgate.Weapons;
using Microsoft.Xna.Framework;

namespace Fourgate.Tests;

public class LevelTenHitTests
{
    private const float Step = 1f / 60f;
    private const float Hp = 100000;

    private static readonly Arena TestArena = new(TestData.Arena());
    private static readonly List<Player> Nobody = new();
    private static readonly PointBlank Close = new(150, 1.6f, 2f);

    [Fact]
    public void CloseHit_DealsMoreDamage_AndPushesFurther()
    {
        var (damage, push) = ShootAt(100, Close);

        Assert.Equal(16f, damage, 2);
        Assert.Equal(60f, push, 0);
    }

    [Fact]
    public void FarHit_IsAnOrdinaryHit()
    {
        var (damage, push) = ShootAt(500, Close);

        Assert.Equal(10f, damage, 2);
        Assert.Equal(30f, push, 0);
    }

    [Fact]
    public void WithoutPointBlank_ACloseHitIsOrdinary()
    {
        var (damage, push) = ShootAt(100, null);

        Assert.Equal(10f, damage, 2);
        Assert.Equal(30f, push, 0);
    }

    [Fact]
    public void ShippedShotgun_GetsPointBlank_OnlyAtLevelTen()
    {
        var shotgun = DataLoader.LoadAll(DataLoader.DefaultDirectory).GetWeapon("shotgun");

        Assert.Null(WeaponStats.Compute(shotgun, 9).PointBlank);
        var close = WeaponStats.Compute(shotgun, 10).PointBlank;
        Assert.NotNull(close);
        Assert.True(close.Value.Range > 0f);
        Assert.True(close.Value.DamageMult > 1f);
        Assert.True(close.Value.KnockbackMult > 1f);
    }

    [Fact]
    public void Weapon_PassesItsPointBlank_ToEveryPellet()
    {
        var shotgun = DataLoader.LoadAll(DataLoader.DefaultDirectory).GetWeapon("shotgun");
        var weapon = new Weapon(shotgun, TestData.Game(), new NullAudioManager());
        weapon.SetLevel(10);
        var shots = new FakeShotHandler();
        var input = new FakeInput { FirePressed = true, FireHeld = true };

        weapon.Update(Step, input, Vector2.Zero, Vector2.UnitX, 0, shots);

        Assert.Equal(shotgun.Pellets, shots.Count);
        Assert.Equal(weapon.Stats.PointBlank, shots.LastPointBlank);
    }

    [Fact]
    public void Execute_KillsAWoundedEnemy_AsAFullHpHit()
    {
        var enemy = ShootWounded(hpShare: 0.25f, executeBelow: 0.3f, isBoss: false);

        Assert.False(enemy.IsAlive);
        Assert.Equal(enemy.MaxHp, enemy.LastHitDamage, 1);
    }

    [Fact]
    public void Execute_LeavesAHealthierEnemy_ToAnOrdinaryHit()
    {
        var enemy = ShootWounded(hpShare: 0.5f, executeBelow: 0.3f, isBoss: false);

        Assert.True(enemy.IsAlive);
        Assert.Equal(500f - 10f, enemy.HitPointsLeft, 1);
    }

    [Fact]
    public void Execute_NeverKillsABoss()
    {
        var enemy = ShootWounded(hpShare: 0.1f, executeBelow: 0.3f, isBoss: true);

        Assert.True(enemy.IsAlive);
    }

    [Fact]
    public void Weapon_PassesItsExecute_ToItsBullets()
    {
        var marksman = DataLoader.LoadAll(DataLoader.DefaultDirectory).GetWeapon("marksman");
        var weapon = new Weapon(marksman, TestData.Game(), new NullAudioManager());
        weapon.SetLevel(10);
        var shots = new FakeShotHandler();
        var input = new FakeInput { FirePressed = true, FireHeld = true };

        weapon.Update(Step, input, Vector2.Zero, Vector2.UnitX, 0, shots);

        Assert.Equal(1, shots.Count);
        Assert.Equal(weapon.Stats.ExecuteBelow, shots.LastExecuteBelow);
    }

    // An enemy of 1000 HP brought down to this share of it, then shot once for 10 damage.
    private static Enemy ShootWounded(float hpShare, float executeBelow, bool isBoss)
    {
        var enemies = new EnemySystem(new NullAudioManager());
        var shots = new ShotSystem(TestArena);
        shots.AddSource(enemies);
        var enemy = enemies.Spawn(Wounded(isBoss), new Vector2(1500, 1500));
        enemies.Update(Step, Nobody, TestArena);
        enemy.Hit(1000f * (1f - hpShare), 0);

        var projectile = new ProjectileConfig { Speed = 6000, Radius = 4, Range = 1100 };
        shots.FireProjectile(new Vector2(1200, 1500), Vector2.UnitX, projectile, 10, 0, executeBelow: executeBelow);
        for (var i = 0; i < 10; i++)
        {
            shots.Update(Step);
        }

        return enemy;
    }

    private static EnemyConfig Wounded(bool isBoss)
    {
        return new EnemyConfig
        {
            Id = "wounded", DisplayName = "Wounded", Hp = 1000, Speed = 0, Radius = 20, ContactDamage = 10,
            ContactCooldown = 0.8f, Xp = 10, ScoreValue = 10, Cost = 1, UnlockWave = 1, DropChance = 0f,
            IsBoss = isBoss,
        };
    }

    // Fires one bullet (10 damage, 30 knockback) from 1200 at an enemy this far ahead;
    // returns the damage it took and how far it was pushed.
    private static (float Damage, float Push) ShootAt(float distance, PointBlank? pointBlank)
    {
        var enemies = new EnemySystem(new NullAudioManager()) { KnockbackDamping = 14 };
        var shots = new ShotSystem(TestArena);
        shots.AddSource(enemies);
        var start = new Vector2(1200 + distance, 1500);
        var enemy = enemies.Spawn(Tough(), start);
        enemies.Update(Step, Nobody, TestArena);

        var projectile = new ProjectileConfig { Speed = 6000, Radius = 4, Range = 1100, Knockback = 30 };
        shots.FireProjectile(new Vector2(1200, 1500), Vector2.UnitX, projectile, 10, 0, pointBlank: pointBlank);
        for (var i = 0; i < 120; i++)
        {
            shots.Update(Step);
            enemies.Update(Step, Nobody, TestArena);
        }

        return (Hp - enemy.HitPointsLeft, enemy.Position.X - start.X);
    }

    private static EnemyConfig Tough()
    {
        return new EnemyConfig
        {
            Id = "tough", DisplayName = "Tough", Hp = Hp, Speed = 110, Radius = 20, ContactDamage = 10,
            ContactCooldown = 0.8f, Xp = 10, ScoreValue = 10, Cost = 1, UnlockWave = 1, DropChance = 0f,
        };
    }
}
