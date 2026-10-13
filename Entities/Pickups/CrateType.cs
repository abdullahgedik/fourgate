namespace Fourgate.Entities.Pickups;

public enum CrateType
{
    /// <summary>Refills weapon reserves.</summary>
    Ammo,

    /// <summary>Restores HP.</summary>
    Health,

    /// <summary>Banks one extra upgrade pick.</summary>
    Upgrade,

    /// <summary>Holds one specific weapon.</summary>
    Weapon,
}
