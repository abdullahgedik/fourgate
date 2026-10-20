using Fourgate.Core.Audio;
using Fourgate.Core.Data;
using Fourgate.Core.Rng;
using Fourgate.Entities;
using Fourgate.Systems;
using Fourgate.Weapons;
using Microsoft.Xna.Framework;

namespace Fourgate.Tests;

public class GrenadeTests
{
    private const float Step = 1f / 60f;

    private static readonly Arena TestArena = new(TestData.Arena());
    private static readonly Vector2 Center = new(1500, 1500);

    private readonly FakeInput _input = new();
    private readonly FakeShotHandler _shots = new();

    [Fact]
    public void Throw_LandsOnTheAimPoint_AndUsesOneGrenade()
    {
        var grenade = CreateGrenade();
        _input.AimDistance = 300;

        Press(grenade);

        var thrown = Assert.Single(_shots.Throws);
        Assert.Equal(Center + new Vector2(300, 0), thrown.Target);
        Assert.Equal(80f, thrown.Damage);
        Assert.Equal(110f, thrown.BlastRadius);
        Assert.Equal(9, grenade.Reserve);
    }

    [Fact]
    public void Throw_AimedBeyondReach_LandsAtMaxRange()
    {
        var grenade = CreateGrenade();

        Press(grenade);

        Assert.Equal(Center + new Vector2(650, 0), _shots.Throws[0].Target);
    }

    [Fact]
    public void Grenade_HasNoMagazine_AndNeverReloads()
    {
        var grenade = CreateGrenade();

        Assert.False(grenade.UsesMagazine);
        Assert.Equal(0, grenade.Magazine);

        _input.ReloadPressed = true;
        Run(grenade, 1);
        Assert.False(grenade.IsReloading);
    }

    [Fact]
    public void Grenade_RunsOut_AfterItsCarryCap()
    {
        var grenade = CreateGrenade();

        for (var i = 0; i < 15; i++)
        {
            Press(grenade);
            Run(grenade, 60);
        }

        Assert.Equal(10, _shots.Throws.Count);
        Assert.Equal(0, grenade.Reserve);
        Assert.False(grenade.IsReloading);
    }

    [Fact]
    public void Throw_WaitsForTheCooldown()
    {
        var grenade = CreateGrenade();

        // A click long before the 0.8 s cooldown is over is not kept.
        Press(grenade);
        Press(grenade);
        Run(grenade, 60);
        Assert.Single(_shots.Throws);

        // A quick click just before it is over goes off as soon as the grenade is ready.
        Press(grenade);
        Run(grenade, 38);
        Press(grenade);
        Assert.Equal(2, _shots.Throws.Count);
        Run(grenade, 20);
        Assert.Equal(3, _shots.Throws.Count);
        Assert.Equal(7, grenade.Reserve);
    }

    [Fact]
    public void Explosive_ExplodesWhenItLands_NotBefore()
    {
        var explosives = CreateExplosives();
        var target = new PracticeTarget(new Vector2(1800, 1500), 20);
        explosives.AddTarget(target);

        // 300 units at 900 units/s is 20 frames.
        explosives.Throw(Shot(new Vector2(1800, 1500)));
        Run(explosives, 19);
        Assert.Equal(0, target.HitCount);
        Assert.Equal(1, explosives.InFlightCount);

        Run(explosives, 2);
        Assert.Equal(1, target.HitCount);
        Assert.Equal(0, explosives.InFlightCount);
    }

    [Fact]
    public void Blast_HitsEveryEnemyInRange_AndNoneOutside()
    {
        var enemies = new EnemySystem(new NullAudioManager());
        var explosives = CreateExplosives();
        explosives.AddSource(enemies);
        var nobody = new List<Player>();
        var landing = new Vector2(2000, 2000);
        var near = enemies.Spawn(TestData.Zombie(), landing + new Vector2(60, 0));
        var edge = enemies.Spawn(TestData.Zombie(), landing + new Vector2(0, 125));
        var outside = enemies.Spawn(TestData.Zombie(), landing + new Vector2(-140, 0));
        enemies.Update(Step, nobody, TestArena);

        explosives.Throw(Shot(landing, origin: landing));
        Run(explosives, 2, nobody);

        Assert.False(near.IsAlive);
        Assert.False(edge.IsAlive);
        Assert.True(outside.IsAlive);
    }

    [Fact]
    public void Thrower_TakesHalfDamage_FromTheirOwnBlast()
    {
        var explosives = CreateExplosives();
        var thrower = CreatePlayer(0);

        explosives.Throw(Shot(Center + new Vector2(50, 0)));
        Run(explosives, 30, new List<Player> { thrower });

        Assert.Equal(60f, thrower.Hp);
    }

    [Fact]
    public void OtherPlayer_TakesTheFriendlyFireShare()
    {
        var explosives = CreateExplosives();
        var thrower = CreatePlayer(0, new Vector2(500, 500));
        var partner = CreatePlayer(1);

        explosives.Throw(Shot(Center + new Vector2(50, 0)));
        Run(explosives, 30, new List<Player> { thrower, partner });

        Assert.Equal(100f, thrower.Hp);
        Assert.Equal(80f, partner.Hp);
    }

    [Fact]
    public void FriendlyFireOff_SparesTheOtherPlayer_ButNotTheThrower()
    {
        var friendlyFire = new FriendlyFireConfig { Enabled = false, DamageToOther = 0.25f, SelfExplosiveDamage = 0.5f };
        var explosives = new ExplosiveSystem(TestArena, friendlyFire, new GameRandom(1), new NullAudioManager());
        var thrower = CreatePlayer(0);
        var partner = CreatePlayer(1);

        explosives.Throw(Shot(Center + new Vector2(50, 0)));
        Run(explosives, 30, new List<Player> { thrower, partner });

        Assert.Equal(60f, thrower.Hp);
        Assert.Equal(100f, partner.Hp);
    }

    [Fact]
    public void DashingPlayer_IsNotHurtByTheBlast()
    {
        var explosives = CreateExplosives();
        var input = new FakeInput { DashPressed = true, Move = Vector2.UnitY };
        var thrower = new Player(0, input, TestData.Game().Player, new NullAudioManager(), Center);

        // Land the grenade right as the dash starts.
        explosives.Throw(Shot(Center, origin: Center));
        thrower.Update(Step, TestArena, _shots);
        Run(explosives, 1, new List<Player> { thrower });

        Assert.Equal(100f, thrower.Hp);
    }

    [Fact]
    public void Throw_PastAWall_LandsInsideTheArena()
    {
        var explosives = CreateExplosives();
        var target = new PracticeTarget(new Vector2(2990, 1500), 10);
        explosives.AddTarget(target);

        explosives.Throw(Shot(new Vector2(3400, 1500), origin: new Vector2(2700, 1500)));
        Run(explosives, 60);

        Assert.Equal(1, target.HitCount);
    }

    [Fact]
    public void Cluster_ScattersMiniBombs_ThatExplodeAroundTheBlast()
    {
        var explosives = CreateExplosives();
        var landing = new Vector2(2000, 2000);
        var center = new PracticeTarget(landing, 10);
        var ring = new List<PracticeTarget>();
        for (var i = 0; i < 8; i++)
        {
            var angle = MathF.Tau * i / 8f;
            var target = new PracticeTarget(landing + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * 150f, 10);
            ring.Add(target);
            explosives.AddTarget(target);
        }

        explosives.AddTarget(center);

        explosives.Throw(Shot(landing, origin: landing) with
        {
            ClusterCount = 4, ClusterDistance = 150, ClusterDamageMult = 0.4f, ClusterRadiusMult = 0.55f,
        });
        Run(explosives, 1);
        Assert.Equal(4, explosives.InFlightCount);

        Run(explosives, 60);
        Assert.Equal(0, explosives.InFlightCount);

        // The main blast reaches only the center; the four mini-bombs land on the ring.
        Assert.Equal(1, center.HitCount);
        Assert.InRange(ring.Sum(t => t.HitCount), 4, 8);
    }

    [Fact]
    public void Levels_GrowTheBlastAndTheCarryCap_AndLevelTenAddsTheCluster()
    {
        var config = GrenadeConfig();

        var one = WeaponStats.Compute(config, 1);
        var three = WeaponStats.Compute(config, 3);
        var ten = WeaponStats.Compute(config, 10);

        Assert.Equal(110f, one.BlastRadius);
        Assert.Equal(650f, one.Range);
        Assert.Equal(0, one.ReserveMaxBonus);
        Assert.Equal(1, three.ReserveMaxBonus);
        Assert.Equal(110f * 1.08f, three.BlastRadius, 2);
        Assert.Equal(0, WeaponStats.Compute(config, 9).ClusterCount);
        Assert.Equal(4, ten.ClusterCount);
        Assert.Equal(0.4f, ten.ClusterDamageMult);
    }

    [Fact]
    public void CarryCapBonus_RaisesTheReserveMax()
    {
        var grenade = new Weapon(GrenadeConfig(), TestData.Game(), new NullAudioManager());

        grenade.SetLevel(3);

        Assert.Equal(11, grenade.ReserveMax);
        Assert.True(grenade.NeedsAmmo);
    }

    [Fact]
    public void ShippedGrenade_IsAThrownCountBasedWeapon()
    {
        var grenade = DataLoader.LoadAll(DataLoader.DefaultDirectory).GetWeapon("grenade");

        Assert.Equal(WeaponType.Thrown, grenade.Type);
        Assert.False(grenade.Ammo.UsesMagazine);
        Assert.NotNull(grenade.Thrown);
        Assert.NotNull(grenade.Explosion);
        Assert.True(WeaponStats.Compute(grenade, 10).ClusterCount > 0);
    }

    [Fact]
    public void ThrownWeapon_WithoutAnExplosionBlock_IsRejected()
    {
        const string json = """
            {
              "weapons": [
                {
                  "id": "grenade", "displayName": "Grenade", "slot": 4, "unlockWave": 5, "type": "thrown",
                  "damage": 80, "fireRate": 1.25, "automatic": false,
                  "ammo": { "usesMagazine": false, "reserveMax": 10, "infiniteReserve": false },
                  "thrown": { "speed": 900, "maxRange": 650, "radius": 10 }
                }
              ]
            }
            """;

        var error = Assert.Throws<DataLoadException>(() => DataLoader.ParseWeapons(json, "weapons.json"));

        Assert.Equal("weapons[0].explosion", error.Field);
    }

    [Fact]
    public void LandingMarker_SitsAtTheAimPoint_NoFurtherThanTheReach_AndOnlyForThrownEquipment()
    {
        var data = DataLoader.LoadAll(DataLoader.DefaultDirectory);
        var input = new FakeInput { AimDistance = 200f, EquipmentHeld = true, HasPointer = true };
        var player = new Player(0, input, TestData.Game().Player, new NullAudioManager(), new Vector2(1000, 1000));
        var grenade = data.GetWeapon("grenade");
        player.GiveWeapon(new Weapon(grenade, TestData.Game(), new NullAudioManager()));

        Assert.True(player.TryGetThrowLanding(out var landing, out var reach));
        Assert.Equal(1000f + player.Radius + 200f, landing.X, 1);
        Assert.Equal(grenade.Explosion!.Radius, reach, 1);

        input.AimDistance = 5000f;
        player.TryGetThrowLanding(out landing, out _);
        Assert.Equal(1000f + player.Radius + grenade.Thrown!.MaxRange, landing.X, 1);

        var barrels = new Player(1, new FakeInput { EquipmentHeld = true }, TestData.Game().Player, new NullAudioManager(), new Vector2(1000, 1000));
        barrels.GiveWeapon(new Weapon(data.GetWeapon("barrel"), TestData.Game(), new NullAudioManager()));
        Assert.False(barrels.TryGetThrowLanding(out _, out _));

        // Not aiming: no marker.
        input.EquipmentHeld = false;
        Assert.False(player.TryGetThrowLanding(out _, out _));
    }

    [Fact]
    public void WithAStick_TheThrowGoesAsFarAsTheTiltAsks()
    {
        var data = DataLoader.LoadAll(DataLoader.DefaultDirectory);
        var input = new FakeInput { HasPointer = false, AimReach = 0.5f, EquipmentHeld = true };
        var player = new Player(0, input, TestData.Game().Player, new NullAudioManager(), new Vector2(1000, 1000));
        var grenade = data.GetWeapon("grenade");
        player.GiveWeapon(new Weapon(grenade, TestData.Game(), new NullAudioManager()));

        Assert.True(player.TryGetThrowLanding(out var landing, out _));
        Assert.Equal(1000f + player.Radius + grenade.Thrown!.MaxRange * 0.5f, landing.X, 1);

        input.AimReach = 1f;
        player.TryGetThrowLanding(out landing, out _);
        Assert.Equal(1000f + player.Radius + grenade.Thrown.MaxRange, landing.X, 1);
    }

    private static WeaponConfig GrenadeConfig()
    {
        return new WeaponConfig
        {
            Id = "grenade",
            DisplayName = "Grenade",
            Slot = 4,
            UnlockWave = 5,
            Type = WeaponType.Thrown,
            Damage = 80,
            FireRate = 1.25f,
            Automatic = false,
            Ammo = new WeaponAmmoConfig { UsesMagazine = false, ReserveMax = 10, InfiniteReserve = false },
            Thrown = new ThrownConfig { Speed = 900, MaxRange = 650, Radius = 10 },
            Explosion = new ExplosionConfig { Radius = 110 },
            Levels = new List<WeaponLevelConfig>
            {
                new() { Level = 3, ReserveMaxBonus = 1, BlastRadiusMult = 1.08f },
            },
            LevelTen = new WeaponLevelTenConfig
            {
                Description = new List<string> { "Cluster grenade" },
                Abilities = new List<WeaponAbilityConfig>
                {
                    new() { Id = WeaponAbility.Cluster, Count = 4, Radius = 150, DamageMult = 0.4f, RadiusMult = 0.55f },
                },
            },
        };
    }

    private static ExplosiveSystem CreateExplosives()
    {
        return new ExplosiveSystem(
            TestArena, TestData.Game().FriendlyFire, new GameRandom(1), new NullAudioManager());
    }

    private static ExplosiveShot Shot(Vector2 target, Vector2? origin = null)
    {
        return new ExplosiveShot(origin ?? Center, target, 900, 10, 80, 110, 0);
    }

    private static Player CreatePlayer(int index, Vector2? position = null)
    {
        return new Player(index, new FakeInput(), TestData.Game().Player, new NullAudioManager(), position ?? Center);
    }

    private static void Run(ExplosiveSystem explosives, int steps, List<Player>? players = null)
    {
        var everyone = players ?? new List<Player>();
        for (var i = 0; i < steps; i++)
        {
            explosives.Update(Step, everyone);
        }
    }

    private Weapon CreateGrenade()
    {
        return new Weapon(GrenadeConfig(), TestData.Game(), new NullAudioManager());
    }

    /// <summary>Clicks fire for one frame, then releases.</summary>
    private void Press(Weapon weapon)
    {
        _input.FirePressed = true;
        _input.FireHeld = true;
        Run(weapon, 1);
        _input.FirePressed = false;
        _input.FireHeld = false;
        Run(weapon, 1);
    }

    private void Run(Weapon weapon, int steps)
    {
        for (var i = 0; i < steps; i++)
        {
            weapon.Update(Step, _input, Center, Vector2.UnitX, 0, _shots);
        }
    }
}
