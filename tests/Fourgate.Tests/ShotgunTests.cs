using Fourgate.Core.Audio;
using Fourgate.Core.Data;
using Fourgate.Core.Rng;
using Fourgate.Weapons;
using Microsoft.Xna.Framework;

namespace Fourgate.Tests;

public class ShotgunTests
{
    private const float Step = 1f / 60f;

    private readonly FakeInput _input = new();
    private readonly FakeShotHandler _shots = new();

    [Fact]
    public void Shot_FiresAllPellets_ForOneRound()
    {
        var weapon = Create(new GameRandom(3));

        Press(weapon);

        Assert.Equal(7, _shots.Count);
        Assert.Equal(5, weapon.Magazine);
        Assert.Equal(8f, _shots.LastDamage);
    }

    [Fact]
    public void Pellets_FanAcrossTheWholeCone_OnePerSlice()
    {
        var weapon = Create(new GameRandom(3));

        Press(weapon);

        // 18 degrees over 7 pellets: each pellet stays inside its own slice of about 2.57 degrees.
        var angles = _shots.Directions.Select(AngleOf).ToList();
        for (var i = 0; i < angles.Count; i++)
        {
            var sliceStart = -9f + i * (18f / 7f);
            Assert.InRange(angles[i], sliceStart - 0.01f, sliceStart + 18f / 7f + 0.01f);
        }

        Assert.All(_shots.Directions, direction => Assert.Equal(1f, direction.Length(), 3));
    }

    [Fact]
    public void Pellets_WithoutARandomSource_AreEvenlySpaced()
    {
        var weapon = Create(null);

        Press(weapon);

        var angles = _shots.Directions.Select(AngleOf).ToList();
        Assert.Equal(-9f + 9f / 7f, angles[0], 2);
        Assert.Equal(0f, angles[3], 2);
        Assert.Equal(9f - 9f / 7f, angles[6], 2);
    }

    [Fact]
    public void TwoVolleys_DoNotLandInExactlyTheSamePlaces()
    {
        var weapon = Create(new GameRandom(3));

        Press(weapon);
        var first = _shots.Directions.ToList();
        _shots.Directions.Clear();
        Run(weapon, 60);
        Press(weapon);

        Assert.NotEqual(first, _shots.Directions);
    }

    [Fact]
    public void BelowLevelTen_HoldingFire_ShootsOnce()
    {
        var weapon = Create(new GameRandom(3));
        weapon.SetLevel(9);

        _input.FirePressed = true;
        _input.FireHeld = true;
        Run(weapon, 1);
        _input.FirePressed = false;
        Run(weapon, 120);

        Assert.Equal(7, _shots.Count);
    }

    [Fact]
    public void LevelTen_FullAuto_KeepsShootingWhileFireIsHeld()
    {
        var weapon = Create(new GameRandom(3));
        weapon.SetLevel(10);

        _input.FireHeld = true;
        Run(weapon, 120);

        // 1.2 shots per second for 2 seconds: volleys at 0, 0.83 and 1.67 s.
        Assert.Equal(3 * 7, _shots.Count);
    }

    [Fact]
    public void ShippedShotgun_FiresWhileHeld_AndLevelTenDoublesItsRate_AndHitsHardUpClose()
    {
        var shotgun = DataLoader.LoadAll(DataLoader.DefaultDirectory).GetWeapon("shotgun");

        var one = WeaponStats.Compute(shotgun, 1);
        var nine = WeaponStats.Compute(shotgun, 9);
        var ten = WeaponStats.Compute(shotgun, 10);

        Assert.Equal(3, shotgun.Slot);
        Assert.Equal(3, shotgun.UnlockWave);
        Assert.True(one.Pellets > 1);
        Assert.True(one.Automatic);
        Assert.Null(nine.PointBlank);

        // Level 10: a real step up in fire rate and magazine, and close hits land harder.
        Assert.True(ten.FireRate >= nine.FireRate * 1.5f);
        Assert.True(ten.MagSize > nine.MagSize);
        Assert.NotNull(ten.PointBlank);
    }

    [Fact]
    public void LevelTen_CanRaiseFireRateAndMagazine()
    {
        var config = TestData.Pistol(
            levelTen: new WeaponLevelTenConfig
            {
                Description = new List<string> { "x" },
                Abilities = new List<WeaponAbilityConfig>(),
                FireRateMult = 2f,
                MagSizeBonus = 4,
            });

        var ten = WeaponStats.Compute(config, 10);

        Assert.Equal(6f, ten.FireRate);
        Assert.Equal(16, ten.MagSize);
        Assert.Equal(3f, WeaponStats.Compute(config, 9).FireRate);
    }

    private static float AngleOf(Vector2 direction)
    {
        return MathHelper.ToDegrees(MathF.Atan2(direction.Y, direction.X));
    }

    private static Weapon Create(GameRandom? random)
    {
        var config = TestData.Pistol(
            magSize: 6,
            reserveMax: 48,
            infiniteReserve: false,
            fireRate: 1.2f,
            id: "shotgun",
            slot: 3,
            spreadDegrees: 18,
            pellets: 7,
            levelTen: new WeaponLevelTenConfig
            {
                Description = new List<string> { "Fully automatic fire" },
                Abilities = new List<WeaponAbilityConfig> { new() { Id = WeaponAbility.FullAuto } },
            });

        // TestData.Pistol deals 15 damage; the shotgun's pellets deal 8.
        return new Weapon(WithDamage(config, 8), TestData.Game(), new NullAudioManager(), random);
    }

    private static WeaponConfig WithDamage(WeaponConfig config, float damage)
    {
        return new WeaponConfig
        {
            Id = config.Id,
            DisplayName = config.DisplayName,
            Slot = config.Slot,
            UnlockWave = config.UnlockWave,
            Type = config.Type,
            Damage = damage,
            Pellets = config.Pellets,
            FireRate = config.FireRate,
            Automatic = config.Automatic,
            SpreadDegrees = config.SpreadDegrees,
            Ammo = config.Ammo,
            Projectile = config.Projectile,
            Levels = config.Levels,
            LevelTen = config.LevelTen,
        };
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
            weapon.Update(Step, _input, Vector2.Zero, Vector2.UnitX, 0, _shots);
        }
    }
}
