using Fourgate.Core.Audio;
using Fourgate.Core.Data;
using Fourgate.Weapons;
using Microsoft.Xna.Framework;

namespace Fourgate.Tests;

public class AssaultRifleTests
{
    private const float Step = 1f / 60f;

    private readonly FakeInput _input = new();
    private readonly FakeShotHandler _shots = new();

    [Fact]
    public void BelowLevelTen_FireRateStaysConstant()
    {
        var rifle = CreateRifle(level: 9);

        _input.FireHeld = true;
        Run(rifle, 180);

        // 8 shots per second for 3 seconds.
        Assert.Equal(24, _shots.Count);
        Assert.Equal(0f, rifle.Overdrive);
    }

    [Fact]
    public void LevelTen_HoldingFire_BuildsUpToTheFullOverdrive()
    {
        var rifle = CreateRifle(level: 10);

        _input.FireHeld = true;
        Run(rifle, 45);
        Assert.InRange(rifle.Overdrive, 0.45f, 0.55f);

        Run(rifle, 60);
        Assert.Equal(1f, rifle.Overdrive);
        Assert.Equal(8f * 1.75f, rifle.CurrentFireRate, 3);
    }

    [Fact]
    public void LevelTen_FiresMoreShots_OverASustainedBurst()
    {
        var rifle = CreateRifle(level: 10);

        _input.FireHeld = true;
        Run(rifle, 180);

        // Well above the 24 shots of a constant 8 per second, below a flat 14 per second.
        Assert.InRange(_shots.Count, 32, 41);
    }

    [Fact]
    public void ReleasingFire_LetsTheOverdriveSettleBack()
    {
        var rifle = CreateRifle(level: 10);
        _input.FireHeld = true;
        Run(rifle, 120);

        _input.FireHeld = false;
        Run(rifle, 15);
        Assert.InRange(rifle.Overdrive, 0.45f, 0.55f);

        Run(rifle, 30);
        Assert.Equal(0f, rifle.Overdrive);
    }

    [Fact]
    public void Overdrive_DoesNotBuildUpWhileReloading()
    {
        var rifle = CreateRifle(level: 10, magSize: 3);

        _input.FireHeld = true;
        Run(rifle, 40);

        // Three rounds go in well under a second, then the reload holds the overdrive back.
        Assert.True(rifle.IsReloading);
        Assert.True(rifle.Overdrive < 0.5f);
    }

    [Fact]
    public void SwitchingAway_ResetsTheOverdrive()
    {
        var rifle = CreateRifle(level: 10);
        _input.FireHeld = true;
        Run(rifle, 120);

        rifle.CancelReload();

        Assert.Equal(0f, rifle.Overdrive);
    }

    [Fact]
    public void ShippedAssaultRifle_IsAnAutomaticOnKeyFive_WithOverdriveAtLevelTen()
    {
        var rifle = DataLoader.LoadAll(DataLoader.DefaultDirectory).GetWeapon("assault");

        Assert.Equal(WeaponCategory.Gun, rifle.Category);
        Assert.Equal(5, rifle.Slot);
        Assert.Equal(7, rifle.UnlockWave);
        Assert.True(rifle.Automatic);
        var nine = WeaponStats.Compute(rifle, 9);
        var ten = WeaponStats.Compute(rifle, 10);
        Assert.Equal(1f, nine.OverdriveFireRateMult);
        Assert.True(ten.OverdriveFireRateMult > 1f);

        // Level 10 carries more, so overdrive has rounds to burn.
        Assert.True(ten.MagSize >= nine.MagSize * 1.4f);
        Assert.True(ten.ReserveMaxBonus > nine.ReserveMaxBonus);
    }

    private static Weapon CreateRifle(int level, int magSize = 500)
    {
        var config = new WeaponConfig
        {
            Id = "assault",
            DisplayName = "Assault Rifle",
            Slot = 5,
            UnlockWave = 7,
            Type = WeaponType.Projectile,
            Damage = 18,
            FireRate = 8,
            Automatic = true,
            Ammo = new WeaponAmmoConfig { MagSize = magSize, ReserveMax = 1000, ReloadTime = 1.4f, InfiniteReserve = false },
            Projectile = new ProjectileConfig { Speed = 7000, Radius = 4, Range = 1300 },
            LevelTen = new WeaponLevelTenConfig
            {
                Description = new List<string> { "Overdrive" },
                Abilities = new List<WeaponAbilityConfig>
                {
                    new() { Id = WeaponAbility.Overdrive, FireRateMult = 1.75f, Seconds = 1.5f },
                },
            },
        };

        var weapon = new Weapon(config, TestData.Game(), new NullAudioManager());
        weapon.SetLevel(level);
        return weapon;
    }

    private void Run(Weapon rifle, int steps)
    {
        for (var i = 0; i < steps; i++)
        {
            rifle.Update(Step, _input, Vector2.Zero, Vector2.UnitX, 0, _shots);
        }
    }
}
