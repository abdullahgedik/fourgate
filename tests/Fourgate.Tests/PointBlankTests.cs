using Fourgate.Core.Audio;
using Fourgate.Core.Data;
using Fourgate.Entities;
using Fourgate.Systems;
using Fourgate.Weapons;
using Microsoft.Xna.Framework;

namespace Fourgate.Tests;

public class PointBlankTests
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
