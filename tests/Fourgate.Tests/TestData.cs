using Fourgate.Core.Data;

namespace Fourgate.Tests;

/// <summary>Config objects shared by the tests.</summary>
public static class TestData
{
    public static ArenaConfig Arena()
    {
        return new ArenaConfig { X = 0, Y = 0, Size = 3000, DoorWidth = 420, WallThickness = 24 };
    }

    public static GameConfig Game(bool reloadsEnabled = true, float reloadMultiplier = 1f, bool dashIFrames = true)
    {
        return new GameConfig
        {
            VirtualWidth = 1920,
            VirtualHeight = 1080,
            Arena = Arena(),
            Camera = new CameraConfig
            {
                Zoom = 1f, LeadFraction = 0f, LeadMaxDistance = 0f, Smoothing = 0f, EdgeMargin = 0f,
            },
            Player = new PlayerConfig
            {
                MoveSpeed = 300,
                Radius = 20,
                StartingWeapon = "pistol",
                Dash = new DashConfig { Cooldown = 2f, Duration = 0.18f, Distance = 200, IFrames = dashIFrames },
            },
            ReloadsEnabled = reloadsEnabled,
            GlobalReloadTimeMultiplier = reloadMultiplier,
        };
    }

    public static EnemyConfig Zombie()
    {
        return new EnemyConfig
        {
            Id = "normal",
            DisplayName = "Zombie",
            Hp = 60,
            Speed = 110,
            Radius = 20,
            ContactDamage = 10,
            ContactCooldown = 0.8f,
            Xp = 10,
            ScoreValue = 10,
            Cost = 1,
            UnlockWave = 1,
            DropChance = 0.12f,
        };
    }

    public static WeaponConfig Pistol(
        bool automatic = false, int magSize = 12, int reserveMax = 0, bool infiniteReserve = true)
    {
        return new WeaponConfig
        {
            Id = "pistol",
            DisplayName = "Pistol",
            Slot = 1,
            UnlockWave = 1,
            Type = WeaponType.Projectile,
            Damage = 15,
            FireRate = 3,
            Automatic = automatic,
            Ammo = new WeaponAmmoConfig
            {
                MagSize = magSize, ReserveMax = reserveMax, ReloadTime = 0.8f, InfiniteReserve = infiniteReserve,
            },
            Projectile = new ProjectileConfig { Speed = 6000, Radius = 4, Range = 1100 },
        };
    }
}
