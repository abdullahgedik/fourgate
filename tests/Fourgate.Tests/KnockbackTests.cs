using Fourgate.Core.Audio;
using Fourgate.Core.Data;
using Fourgate.Core.Rng;
using Fourgate.Entities;
using Fourgate.Systems;
using Fourgate.Weapons;
using Microsoft.Xna.Framework;

namespace Fourgate.Tests;

public class KnockbackTests
{
    private const float Step = 1f / 60f;

    private static readonly Arena TestArena = new(TestData.Arena());
    private static readonly List<Player> Nobody = new();

    [Fact]
    public void Bullet_PushesAnEnemy_AlongItsPath_ByItsKnockback()
    {
        var enemies = new EnemySystem(new NullAudioManager()) { KnockbackDamping = 14 };
        var shots = new ShotSystem(TestArena);
        shots.AddSource(enemies);
        var enemy = enemies.Spawn(Tough(), new Vector2(1500, 1500));
        enemies.Update(Step, Nobody, TestArena);

        shots.FireProjectile(new Vector2(1200, 1500), Vector2.UnitX, Projectile(30), 1, 0);
        Run(enemies, shots, 120);

        Assert.Equal(1530f, enemy.Position.X, 0);
        Assert.Equal(1500f, enemy.Position.Y, 1);
    }

    [Fact]
    public void Knockback_PlaysOutOverTime_NotAtOnce()
    {
        var enemies = new EnemySystem(new NullAudioManager()) { KnockbackDamping = 14 };
        var enemy = enemies.Spawn(Tough(), new Vector2(1500, 1500));

        enemy.Knock(new Vector2(30, 0));
        enemies.Update(Step, Nobody, TestArena);

        Assert.InRange(enemy.Position.X, 1500.1f, 1515f);
    }

    [Fact]
    public void KnockbackResist_TakesOffItsShareOfThePush()
    {
        var enemies = new EnemySystem(new NullAudioManager());
        var enemy = enemies.Spawn(Tough(knockbackResist: 0.75f), new Vector2(1500, 1500));

        enemy.Knock(new Vector2(40, 0));
        enemies.Update(Step, Nobody, TestArena);

        Assert.Equal(1510f, enemy.Position.X, 1);
    }

    [Fact]
    public void Explosion_PushesEnemiesAwayFromItsCenter()
    {
        var enemies = new EnemySystem(new NullAudioManager());
        var explosives = new ExplosiveSystem(
            TestArena, TestData.Game().FriendlyFire, new GameRandom(1), new NullAudioManager(),
            new HazardSystem(TestData.Game().FriendlyFire))
        {
            KnockbackDistance = 50,
        };
        explosives.AddSource(enemies);
        var left = enemies.Spawn(Tough(), new Vector2(1400, 1500));
        var below = enemies.Spawn(Tough(), new Vector2(1500, 1600));
        enemies.Update(Step, Nobody, TestArena);

        explosives.Throw(new ExplosiveShot(
            new Vector2(1500, 1500), new Vector2(1500, 1500), 400, 10, 1, 200, 0));
        for (var i = 0; i < 60; i++)
        {
            explosives.Update(Step, Nobody);
            enemies.Update(Step, Nobody, TestArena);
        }

        Assert.True(left.Position.X < 1400f - 40f);
        Assert.True(below.Position.Y > 1600f + 40f);
    }

    [Fact]
    public void ShippedWeapons_PushHarder_TheHeavierTheyHit()
    {
        var data = DataLoader.LoadAll(DataLoader.DefaultDirectory);

        Assert.True(data.GetWeapon("smg").Projectile!.Knockback < data.GetWeapon("pistol").Projectile!.Knockback);
        Assert.True(data.GetWeapon("pistol").Projectile!.Knockback < data.GetWeapon("sniper").Projectile!.Knockback);
        Assert.True(data.Game.Knockback.Explosion > 0f);
        Assert.True(data.Game.Knockback.Damping > 0f);
    }

    private static ProjectileConfig Projectile(float knockback)
    {
        return new ProjectileConfig { Speed = 6000, Radius = 4, Range = 1100, Knockback = knockback };
    }

    private static EnemyConfig Tough(float knockbackResist = 0f)
    {
        return new EnemyConfig
        {
            Id = "tough", DisplayName = "Tough", Hp = 100000, Speed = 110, Radius = 20, ContactDamage = 10,
            ContactCooldown = 0.8f, Xp = 10, ScoreValue = 10, Cost = 1, UnlockWave = 1, DropChance = 0f,
            KnockbackResist = knockbackResist,
        };
    }

    private static void Run(EnemySystem enemies, ShotSystem shots, int steps)
    {
        for (var i = 0; i < steps; i++)
        {
            shots.Update(Step);
            enemies.Update(Step, Nobody, TestArena);
        }
    }
}
