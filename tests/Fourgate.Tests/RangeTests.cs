using Fourgate.Core.Data;
using Fourgate.Entities;
using Fourgate.Systems;
using Fourgate.Weapons;
using Microsoft.Xna.Framework;

namespace Fourgate.Tests;

public class RangeTests
{
    private const float Step = 1f / 60f;

    [Fact]
    public void ShippedGuns_HaveNoRangeLimit_ButThrowsDo()
    {
        var data = DataLoader.LoadAll(DataLoader.DefaultDirectory);

        foreach (var id in new[] { "pistol", "smg", "shotgun", "marksman", "assault", "sniper", "rocket", "railgun" })
        {
            Assert.Equal(float.PositiveInfinity, WeaponStats.Compute(data.GetWeapon(id), 10).Range);
        }

        var grenade = WeaponStats.Compute(data.GetWeapon("grenade"), 1).Range;
        var molotov = WeaponStats.Compute(data.GetWeapon("molotov"), 1).Range;
        Assert.InRange(grenade, 400f, 600f);
        Assert.InRange(molotov, 400f, 600f);
    }

    [Fact]
    public void BulletWithoutARange_CrossesTheWholeArena_AndStopsAtTheFarWall()
    {
        var particles = new ParticleSystem(new Fourgate.Core.Rng.GameRandom(1));
        var shots = new ShotSystem(new Arena(TestData.Arena())) { Particles = particles };
        var slow = new ProjectileConfig { Speed = 1500, Radius = 4 };

        // Along a row clear of the pillars, from near one wall toward the other.
        shots.FireProjectile(new Vector2(100, 300), Vector2.UnitX, slow, 10, 0);
        for (var i = 0; i < 60; i++)
        {
            shots.Update(Step);
        }

        Assert.Equal(1, shots.BulletCount);

        for (var i = 0; i < 60 * 2; i++)
        {
            shots.Update(Step);
        }

        Assert.Equal(0, shots.BulletCount);
        Assert.True(particles.Count > 0);
    }

    [Fact]
    public void ShippedSmg_LevelTen_BouncesOffWallsFourTimes_ThenStopsAtTheNextWall()
    {
        var data = DataLoader.LoadAll(DataLoader.DefaultDirectory);
        var stats = WeaponStats.Compute(data.GetWeapon("smg"), 10);
        Assert.Equal(4, stats.WallBounces);
        Assert.Equal(1, stats.Ricochets);

        var shots = new ShotSystem(new Arena(TestData.Arena()));
        var bullet = new ProjectileConfig { Speed = 6000, Radius = 4 };

        // Back and forth along a row clear of the pillars: one wall, then four more crossings.
        shots.FireProjectile(new Vector2(1500, 300), Vector2.UnitX, bullet, 10, 0, wallBounces: 4);
        Run(shots, 2.0f);
        Assert.Equal(1, shots.BulletCount);
        Run(shots, 0.6f);
        Assert.Equal(0, shots.BulletCount);
    }

    [Fact]
    public void WallBounces_DoNotAddEnemyBounces_TheBulletStillJumpsToOnlyOneMoreEnemy()
    {
        var shots = new ShotSystem(new Arena(TestData.Arena()));
        var first = new PracticeTarget(new Vector2(1000, 300), 20);
        var second = new PracticeTarget(new Vector2(1000, 550), 20);
        var third = new PracticeTarget(new Vector2(1000, 800), 20);
        shots.AddTarget(first);
        shots.AddTarget(second);
        shots.AddTarget(third);

        shots.FireProjectile(
            new Vector2(600, 300), Vector2.UnitX, new ProjectileConfig { Speed = 6000, Radius = 4 }, 10, 0,
            ricochets: 1, ricochetRadius: 350, wallBounces: 4);
        Run(shots, 1f);

        Assert.Equal(1, first.HitCount);
        Assert.Equal(1, second.HitCount);
        Assert.Equal(0, third.HitCount);
        Assert.Equal(0, shots.BulletCount);
    }

    private static void Run(ShotSystem shots, float seconds)
    {
        for (var i = 0; i < (int)MathF.Round(seconds / Step); i++)
        {
            shots.Update(Step);
        }
    }

    [Fact]
    public void RangeBonus_OnAGunWithoutARange_IsRejected()
    {
        const string json = """
            {
              "weapons": [
                {
                  "id": "pistol", "displayName": "Pistol", "slot": 1, "unlockWave": 1, "type": "projectile",
                  "damage": 10, "fireRate": 3, "automatic": false,
                  "ammo": { "magSize": 12, "reserveMax": 0, "reloadTime": 0.8, "infiniteReserve": true },
                  "projectile": { "speed": 6000, "radius": 4 },
                  "levels": [ { "level": 5, "rangeMult": 1.1 } ]
                }
              ]
            }
            """;

        var error = Assert.Throws<DataLoadException>(() => DataLoader.ParseWeapons(json, "weapons.json"));

        Assert.Equal("weapons[0].levels[0].rangeMult", error.Field);
    }
}
