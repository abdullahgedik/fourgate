using Fourgate.Core.Audio;
using Fourgate.Core.Data;
using Fourgate.Weapons;
using Microsoft.Xna.Framework;

namespace Fourgate.Tests;

public class RifleTests
{
    private const float Step = 1f / 60f;

    private readonly FakeInput _input = new();
    private readonly FakeShotHandler _shots = new();
    private readonly List<float> _damages = new();

    [Fact]
    public void LevelTen_EveryFourthShot_DealsDoubleDamage()
    {
        var rifle = CreateRifle();
        rifle.SetLevel(10);

        for (var i = 0; i < 8; i++)
        {
            FireOnce(rifle);
        }

        var normal = WeaponStats.Compute(rifle.Config, 10).Damage;
        Assert.Equal(
            new[] { normal, normal, normal, normal * 2, normal, normal, normal, normal * 2 },
            _damages);
    }

    [Fact]
    public void LevelTen_BulletsPierceThreeEnemies()
    {
        var rifle = CreateRifle();
        rifle.SetLevel(10);

        FireOnce(rifle);

        Assert.Equal(3, _shots.LastPierce);
    }

    [Fact]
    public void BelowLevelTen_EveryShotDealsTheSameDamage_AndNothingPierces()
    {
        var rifle = CreateRifle();
        rifle.SetLevel(9);

        for (var i = 0; i < 8; i++)
        {
            FireOnce(rifle);
        }

        Assert.All(_damages, damage => Assert.Equal(_damages[0], damage));
        Assert.Equal(0, _shots.LastPierce);
    }

    [Fact]
    public void EmpoweredRhythm_CarriesOverAReload()
    {
        var rifle = CreateRifle();
        rifle.SetLevel(10);

        // Three shots, a manual reload, then the fourth shot is still the empowered one.
        for (var i = 0; i < 3; i++)
        {
            FireOnce(rifle);
        }

        _input.ReloadPressed = true;
        Run(rifle, 1);
        _input.ReloadPressed = false;
        Run(rifle, 120);

        FireOnce(rifle);

        Assert.Equal(_damages[0] * 2, _damages[3], 3);
    }

    [Fact]
    public void ShippedMarksmanRifle_PiercesAndEmpowersAtLevelTen()
    {
        var rifle = DataLoader.LoadAll(DataLoader.DefaultDirectory).GetWeapon("marksman");

        var nine = WeaponStats.Compute(rifle, 9);
        var ten = WeaponStats.Compute(rifle, 10);

        Assert.Equal(5, rifle.Slot);
        Assert.Equal(6, rifle.UnlockWave);
        Assert.False(ten.Automatic);
        Assert.Equal(0, nine.Pierce);
        Assert.Equal(0, nine.EmpoweredEvery);
        Assert.Equal(3, ten.Pierce);
        Assert.Equal(4, ten.EmpoweredEvery);
        Assert.Equal(2f, ten.EmpoweredDamageMult);
    }

    private static Weapon CreateRifle()
    {
        var config = new WeaponConfig
        {
            Id = "rifle",
            DisplayName = "Rifle",
            Slot = 5,
            UnlockWave = 6,
            Type = WeaponType.Projectile,
            Damage = 40,
            FireRate = 2.5f,
            Automatic = false,
            Ammo = new WeaponAmmoConfig { MagSize = 10, ReserveMax = 80, ReloadTime = 1.3f, InfiniteReserve = false },
            Projectile = new ProjectileConfig { Speed = 7500, Radius = 4, Range = 1700 },
            LevelTen = new WeaponLevelTenConfig
            {
                Description = new List<string> { "Pierce 3", "Every 4th shot double" },
                Abilities = new List<WeaponAbilityConfig>
                {
                    new() { Id = WeaponAbility.Pierce, Count = 3 },
                    new() { Id = WeaponAbility.EmpoweredShot, Count = 4, DamageMult = 2f },
                },
            },
        };

        return new Weapon(config, TestData.Game(), new NullAudioManager());
    }

    /// <summary>Clicks once, records the damage of the shot, then waits out the fire interval.</summary>
    private void FireOnce(Weapon rifle)
    {
        var before = _shots.Count;
        _input.FirePressed = true;
        _input.FireHeld = true;
        Run(rifle, 1);
        _input.FirePressed = false;
        _input.FireHeld = false;
        Run(rifle, 30);

        Assert.Equal(before + 1, _shots.Count);
        _damages.Add(_shots.LastDamage);
    }

    private void Run(Weapon rifle, int steps)
    {
        for (var i = 0; i < steps; i++)
        {
            rifle.Update(Step, _input, Vector2.Zero, Vector2.UnitX, 0, _shots);
        }
    }
}
