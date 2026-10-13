namespace Fourgate.Core.Data;

/// <summary>Player stat a non-weapon upgrade changes.</summary>
public enum UpgradeStat
{
    /// <summary>Adds the amount to max HP per level, and heals that much when taken.</summary>
    MaxHp,

    /// <summary>Raises move speed by the amount (a fraction) per level.</summary>
    MoveSpeed,

    /// <summary>Lowers the dash cooldown by the amount (a fraction) per level.</summary>
    DashCooldown,

    /// <summary>Raises every weapon's maximum reserve by the amount (a fraction) per level.</summary>
    AmmoCapacity,

    /// <summary>Raises the crate pickup range by the amount (a fraction) per level.</summary>
    PickupRange,
}
