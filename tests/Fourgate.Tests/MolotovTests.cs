using Fourgate.Core.Audio;
using Fourgate.Core.Data;
using Fourgate.Core.Rng;
using Fourgate.Entities;
using Fourgate.Entities.Enemies;
using Fourgate.Systems;
using Fourgate.Weapons;
using Microsoft.Xna.Framework;

namespace Fourgate.Tests;

public class MolotovTests
{
    private const float Step = 1f / 60f;

    private static readonly Arena TestArena = new(TestData.Arena());
    private static readonly Vector2 Spot = new(2000, 2000);
    private static readonly List<Player> Nobody = new();

    [Fact]
    public void FirePool_BurnsEnemiesInside_ForItsWholeDuration()
    {
        var (enemies, hazards) = CreateWorld();
        var inside = Spawn(enemies, Spot + new Vector2(50, 0), hp: 500);
        var outside = Spawn(enemies, Spot + new Vector2(130, 0), hp: 500);

        hazards.SpawnFire(Spot, 90, 4, 12, 0, 0);
        Run(enemies, hazards, seconds: 5);

        // 12 per second for 4 seconds.
        Assert.Equal(452f, inside.Hp, 0);
        Assert.Equal(500f, outside.Hp);
        Assert.Equal(0, hazards.FirePoolCount);
    }

    [Fact]
    public void Splash_SpreadsOutFromWhereItLands_AndOnlyBurnsAsFarAsItHasSpread()
    {
        var (enemies, hazards) = CreateWorld();
        var nearMiddle = Spawn(enemies, Spot + new Vector2(10, 0), hp: 500);
        var atEdge = Spawn(enemies, Spot + new Vector2(75, 0), hp: 500);

        hazards.SpawnFire(Spot, 90, 4, 12, 0, 0, spreadSeconds: 0.4f);
        Run(enemies, hazards, seconds: 0.1f);
        Assert.True(nearMiddle.Hp < 500f);
        Assert.Equal(500f, atEdge.Hp);

        Run(enemies, hazards, seconds: 0.5f);
        Assert.True(atEdge.Hp < 500f);
    }

    [Fact]
    public void FirePool_DoesNotFlashEnemiesLikeAHit()
    {
        var (enemies, hazards) = CreateWorld();
        var enemy = Spawn(enemies, Spot, hp: 500);

        hazards.SpawnFire(Spot, 90, 4, 12, 0, 0);
        Run(enemies, hazards, seconds: 1);

        Assert.False(enemy.ConsumeHit());
    }

    [Fact]
    public void Thrower_BurnsAtTheSelfShare()
    {
        var hazards = new HazardSystem(TestData.Game().FriendlyFire);
        var thrower = new Player(0, new FakeInput(), TestData.Game().Player, new NullAudioManager(), Spot);
        var players = new List<Player> { thrower };

        hazards.SpawnFire(Spot, 90, 4, 12, 0, 0);
        for (var i = 0; i < 60; i++)
        {
            hazards.Update(Step, players);
        }

        // Half of 12 per second, for one second.
        Assert.Equal(94f, thrower.Hp, 0);
    }

    [Fact]
    public void WithoutIgnite_EnemiesStopBurningOnceTheyLeave()
    {
        var (enemies, hazards) = CreateWorld();
        var enemy = Spawn(enemies, Spot, hp: 500);
        hazards.SpawnFire(Spot, 90, 0.5f, 12, 0, 0);

        Run(enemies, hazards, seconds: 3);

        Assert.False(enemy.IsBurning);
        Assert.Equal(494f, enemy.Hp, 0);
    }

    [Fact]
    public void Ignite_KeepsEnemiesBurningAfterThePoolIsGone()
    {
        var (enemies, hazards) = CreateWorld();
        var enemy = Spawn(enemies, Spot, hp: 500);
        hazards.SpawnFire(Spot, 90, 0.5f, 12, 0, igniteSeconds: 3);

        Run(enemies, hazards, seconds: 1);
        Assert.Equal(0, hazards.FirePoolCount);
        Assert.True(enemy.IsBurning);

        Run(enemies, hazards, seconds: 4);
        Assert.False(enemy.IsBurning);

        // Burned inside the pool for 0.5 s (doubled by the ignite running alongside),
        // then by the ignite alone until 3 s after it last touched the fire.
        Assert.InRange(enemy.Hp, 500f - 12f * 4f, 500f - 12f * 3f);
    }

    [Fact]
    public void BurningEnemy_SetsTheEnemiesTouchingItAlight_ButTheyDoNotPassItOn()
    {
        var (enemies, hazards) = CreateWorld();
        var first = Spawn(enemies, Spot, hp: 500);
        var touching = Spawn(enemies, Spot + new Vector2(800, 0), hp: 500);
        var touchingThat = Spawn(enemies, Spot + new Vector2(840, 0), hp: 500);

        first.Ignite(3, 12, 0);
        first.Position = touching.Position - new Vector2(40, 0);
        Run(enemies, hazards, seconds: 0.1f);

        Assert.True(first.CanSpreadFire);
        Assert.True(touching.IsBurning);
        Assert.False(touching.CanSpreadFire);
        Assert.False(touchingThat.IsBurning);
    }

    [Fact]
    public void Thrown_LandsAsAFirePool_WithoutAnExplosion()
    {
        var hazards = new HazardSystem(TestData.Game().FriendlyFire);
        var explosives = new ExplosiveSystem(
            TestArena, TestData.Game().FriendlyFire, new GameRandom(1), new NullAudioManager(), hazards);
        var dummy = new PracticeTarget(Spot, 20);
        explosives.AddTarget(dummy);

        explosives.Throw(new ExplosiveShot(
            Spot, Spot, 850, 9, 12, 0, 0, FireRadius: 90, FireSeconds: 4, FireDamagePerSecond: 12));
        explosives.Update(Step, Nobody);

        Assert.Equal(1, hazards.FirePoolCount);
        Assert.Equal(0, dummy.HitCount);
    }

    [Fact]
    public void LevelTen_DoublesTheBurnTime_AndAddsIgnite()
    {
        var molotov = DataLoader.LoadAll(DataLoader.DefaultDirectory).GetWeapon("molotov");

        var nine = WeaponStats.Compute(molotov, 9);
        var ten = WeaponStats.Compute(molotov, 10);

        Assert.Equal(WeaponCategory.Equipment, molotov.Category);
        Assert.Equal(8, molotov.UnlockWave);
        Assert.Equal(0f, nine.IgniteSeconds);
        Assert.True(ten.IgniteSeconds > 0f);
        Assert.Equal(nine.FireSeconds * 2f, ten.FireSeconds, 3);
        Assert.Equal(0f, ten.BlastRadius);
    }

    [Fact]
    public void Molotov_ThrowsAFirePoolShot()
    {
        var molotov = DataLoader.LoadAll(DataLoader.DefaultDirectory).GetWeapon("molotov");
        var weapon = new Weapon(molotov, TestData.Game(), new NullAudioManager());
        var shots = new FakeShotHandler();
        var trigger = new WeaponTrigger(true, true, false, 300);

        weapon.Update(Step, trigger, Spot, Vector2.UnitX, 0, shots);

        var shot = Assert.Single(shots.Throws);
        Assert.Equal(0f, shot.BlastRadius);
        Assert.True(shot.FireRadius > 0f);
        Assert.True(shot.FireSeconds > 0f);
        Assert.Equal(molotov.Damage, shot.FireDamagePerSecond);
    }

    [Fact]
    public void ThrownWeapon_WithFireButNoExplosion_IsValid()
    {
        const string json = """
            {
              "weapons": [
                {
                  "id": "molotov", "displayName": "Molotov", "category": "equipment", "slot": 2, "unlockWave": 8,
                  "type": "thrown", "damage": 12, "fireRate": 1, "automatic": false,
                  "ammo": { "usesMagazine": false, "reserveMax": 8, "infiniteReserve": false },
                  "thrown": { "speed": 850, "maxRange": 600, "radius": 9 },
                  "fire": { "radius": 90, "seconds": 4 }
                }
              ]
            }
            """;

        Assert.Single(DataLoader.ParseWeapons(json, "weapons.json"));
    }

    private static (EnemySystem Enemies, HazardSystem Hazards) CreateWorld()
    {
        var enemies = new EnemySystem(new NullAudioManager());
        var hazards = new HazardSystem(TestData.Game().FriendlyFire);
        hazards.AddSource(enemies);
        return (enemies, hazards);
    }

    private static Enemy Spawn(EnemySystem enemies, Vector2 position, float hp)
    {
        var zombie = TestData.Zombie();
        var config = new EnemyConfig
        {
            Id = zombie.Id,
            DisplayName = zombie.DisplayName,
            Hp = hp,
            Speed = zombie.Speed,
            Radius = zombie.Radius,
            ContactDamage = zombie.ContactDamage,
            ContactCooldown = zombie.ContactCooldown,
            Xp = zombie.Xp,
            ScoreValue = zombie.ScoreValue,
            Cost = zombie.Cost,
            UnlockWave = zombie.UnlockWave,
            DropChance = zombie.DropChance,
        };
        var enemy = enemies.Spawn(config, position);
        enemies.Update(Step, Nobody, TestArena);
        return enemy;
    }

    private static void Run(EnemySystem enemies, HazardSystem hazards, float seconds)
    {
        var steps = (int)MathF.Round(seconds / Step);
        for (var i = 0; i < steps; i++)
        {
            enemies.Update(Step, Nobody, TestArena);
            hazards.Update(Step, Nobody);
        }
    }
}
