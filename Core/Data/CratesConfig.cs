namespace Fourgate.Core.Data;

/// <summary>Crate drop and pickup rules.</summary>
public sealed class CratesConfig
{
    /// <summary>Seconds a crate stays on the floor.</summary>
    public required float DespawnSeconds { get; init; }

    /// <summary>Seconds before despawning during which the crate blinks.</summary>
    public required float BlinkSeconds { get; init; }

    /// <summary>Distance between a player's center and a crate's center at which it is picked up.</summary>
    public required float PickupRadius { get; init; }

    /// <summary>HP restored by a health crate.</summary>
    public required float HealAmount { get; init; }

    /// <summary>A living player below this HP fraction switches drops to the low-HP health chance.</summary>
    public required float LowHpThreshold { get; init; }

    /// <summary>Chance (0–1) that a non-upgrade drop is a health crate.</summary>
    public required float HealthChanceNormal { get; init; }

    /// <summary>The same chance while any living player is low on HP.</summary>
    public required float HealthChanceLowHp { get; init; }

    /// <summary>Chance (0–1) that a drop is an upgrade crate.</summary>
    public required float UpgradeCrateChance { get; init; }

    /// <summary>Share of its maximum reserve that the lowest-ammo weapon gets from an ammo crate.</summary>
    public required float AmmoPrimaryFraction { get; init; }

    /// <summary>Share of its maximum reserve that every other weapon gets from an ammo crate.</summary>
    public required float AmmoOthersFraction { get; init; }

    /// <summary>Checks value ranges; throws <see cref="DataLoadException"/> naming the bad field.</summary>
    public void Validate(string file)
    {
        DataLoadException.Require(DespawnSeconds > 0, file, "crates.despawnSeconds", "must be greater than 0");
        DataLoadException.Require(BlinkSeconds >= 0, file, "crates.blinkSeconds", "must not be negative");
        DataLoadException.Require(PickupRadius > 0, file, "crates.pickupRadius", "must be greater than 0");
        DataLoadException.Require(HealAmount > 0, file, "crates.healAmount", "must be greater than 0");
        RequireFraction(LowHpThreshold, file, "crates.lowHpThreshold");
        RequireFraction(HealthChanceNormal, file, "crates.healthChanceNormal");
        RequireFraction(HealthChanceLowHp, file, "crates.healthChanceLowHp");
        RequireFraction(UpgradeCrateChance, file, "crates.upgradeCrateChance");
        RequireFraction(AmmoPrimaryFraction, file, "crates.ammoPrimaryFraction");
        RequireFraction(AmmoOthersFraction, file, "crates.ammoOthersFraction");
    }

    private static void RequireFraction(float value, string file, string field)
    {
        DataLoadException.Require(value >= 0 && value <= 1, file, field, "must be between 0 and 1");
    }
}
