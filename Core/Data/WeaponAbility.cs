namespace Fourgate.Core.Data;

/// <summary>Named level-10 weapon behaviors that can be referenced from <c>weapons.json</c>.</summary>
public enum WeaponAbility
{
    /// <summary>Bullets pass through this many enemies before stopping.</summary>
    Pierce,

    /// <summary>
    /// Bullets bounce this many times: off a wall, or from an enemy they hit to the nearest
    /// other enemy within the ability's radius.
    /// </summary>
    Ricochet,

    /// <summary>Every shot fires two bullets side by side.</summary>
    DualWield,
}
