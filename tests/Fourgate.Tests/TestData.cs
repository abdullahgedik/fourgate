using Fourgate.Core.Data;

namespace Fourgate.Tests;

/// <summary>Config objects shared by the tests.</summary>
public static class TestData
{
    public static ArenaConfig Arena()
    {
        return new ArenaConfig { X = 0, Y = 0, Size = 3000, DoorWidth = 420, WallThickness = 24 };
    }

    public static GameConfig Game(
        bool reloadsEnabled = true,
        float reloadMultiplier = 1f,
        bool dashIFrames = true,
        int maxAlive = 60,
        int startWave = 1,
        int campaignWaves = 50,
        BossAggroConfig? bossAggro = null)
    {
        return new GameConfig
        {
            BossAggro = bossAggro,
            VirtualWidth = 1920,
            VirtualHeight = 1080,
            Arena = Arena(),
            Score = new ScoreConfig { WaveClearBonus = 250, PointsPerSecond = 1f },
            Coop = new CoopConfig { ReviveHpFraction = 0.5f, EnemyBudgetMultiplier = 1.6f, BossHpMultiplier = 1.5f },
            Camera = new CameraConfig
            {
                Zoom = 1f, LeadFraction = 0f, LeadMaxDistance = 0f, Smoothing = 0f, EdgeMargin = 0f,
            },
            Player = new PlayerConfig
            {
                MaxHp = 100,
                MoveSpeed = 300,
                Radius = 20,
                StartingWeapon = "pistol",
                Dash = new DashConfig { Cooldown = 2f, Duration = 0.18f, Distance = 200, IFrames = dashIFrames },
            },
            MaxAliveEnemies = maxAlive,
            CampaignWaves = campaignWaves,
            RestCountdownSeconds = 5,
            Debug = new DebugConfig { StartWave = startWave },
            Xp = Xp(),
            Crates = Crates(),
            FriendlyFire = new FriendlyFireConfig { Enabled = true, DamageToOther = 0.25f, SelfExplosiveDamage = 0.5f },
            ReloadsEnabled = reloadsEnabled,
            GlobalReloadTimeMultiplier = reloadMultiplier,
        };
    }

    public static XpConfig Xp(bool shared = true)
    {
        return new XpConfig { Base = 60, Exponent = 1.35f, SharedFullToAllPlayers = shared };
    }

    public static UpgradeConfig Upgrade(string id, UpgradeStat stat, float amount, int maxLevel)
    {
        return new UpgradeConfig
        {
            Id = id,
            DisplayName = id,
            Description = id,
            Stat = stat,
            AmountPerLevel = amount,
            MaxLevel = maxLevel,
        };
    }

    public static UpgradesConfig Upgrades()
    {
        return new UpgradesConfig
        {
            CardsPerPick = 3,
            Fallback = new UpgradeFallbackConfig { DisplayName = "Second Wind", Description = "Restore all HP" },
            Upgrades = new List<UpgradeConfig>
            {
                Upgrade("maxHp", UpgradeStat.MaxHp, 20, 5),
                Upgrade("moveSpeed", UpgradeStat.MoveSpeed, 0.06f, 5),
                Upgrade("dashCooldown", UpgradeStat.DashCooldown, 0.10f, 5),
                Upgrade("ammoCapacity", UpgradeStat.AmmoCapacity, 0.10f, 5),
                Upgrade("pickupRange", UpgradeStat.PickupRange, 0.20f, 3),
            },
        };
    }

    public static Systems.WeaponUnlocks Unlocks()
    {
        return new Systems.WeaponUnlocks(Game(), new Core.Audio.NullAudioManager(), null);
    }

    public static CratesConfig Crates()
    {
        return new CratesConfig
        {
            DespawnSeconds = 20,
            BlinkSeconds = 5,
            PickupRadius = 40,
            HealAmount = 25,
            LowHpThreshold = 0.4f,
            HealthChanceNormal = 0.15f,
            HealthChanceLowHp = 0.6f,
            UpgradeCrateChance = 0.015f,
            AmmoPrimaryFraction = 0.35f,
            AmmoOthersFraction = 0.08f,
        };
    }

    public static WavesConfig Waves()
    {
        return new WavesConfig
        {
            Budget = new WaveBudgetConfig { Base = 6, PerWave = 2.2f, Quadratic = 0.04f },
            NewerTypeBias = 0.5f,
            SpawnIntervalStart = 1.2f,
            SpawnIntervalStepPerWave = 0.03f,
            SpawnIntervalMin = 0.35f,
            GroupSizeBase = 2,
            GroupSizePerWave = 0.15f,
            GroupSizeMax = 6,
            DoorWarningSeconds = 0.8f,
        };
    }

    public static EnemyConfig Zombie(
        string id = "normal", float cost = 1, int unlockWave = 1, float dropChance = 0.12f)
    {
        return new EnemyConfig
        {
            Id = id,
            DisplayName = "Zombie",
            Hp = 60,
            Speed = 110,
            Radius = 20,
            ContactDamage = 10,
            ContactCooldown = 0.8f,
            Xp = 10,
            ScoreValue = 10,
            Cost = cost,
            UnlockWave = unlockWave,
            DropChance = dropChance,
        };
    }

    public static WeaponConfig Pistol(
        bool automatic = false,
        int magSize = 12,
        int reserveMax = 0,
        bool infiniteReserve = true,
        float fireRate = 3,
        string id = "pistol",
        int slot = 1,
        float spreadDegrees = 0,
        int pellets = 1,
        WeaponCategory category = WeaponCategory.Gun,
        List<WeaponLevelConfig>? levels = null,
        WeaponLevelTenConfig? levelTen = null)
    {
        return new WeaponConfig
        {
            Id = id,
            DisplayName = "Pistol",
            Slot = slot,
            SpreadDegrees = spreadDegrees,
            Pellets = pellets,
            Category = category,
            Levels = levels ?? new List<WeaponLevelConfig>(),
            LevelTen = levelTen,
            UnlockWave = 1,
            Type = WeaponType.Projectile,
            Damage = 15,
            FireRate = fireRate,
            Automatic = automatic,
            Ammo = new WeaponAmmoConfig
            {
                MagSize = magSize, ReserveMax = reserveMax, ReloadTime = 0.8f, InfiniteReserve = infiniteReserve,
            },
            Projectile = new ProjectileConfig { Speed = 6000, Radius = 4, Range = 1100 },
        };
    }
}
