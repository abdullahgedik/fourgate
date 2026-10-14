namespace Fourgate.Core.Data;

/// <summary>Magazine, reserve and reload values of a weapon.</summary>
public sealed class WeaponAmmoConfig
{
    /// <summary>
    /// False for count-based weapons (grenades, mines): they have no magazine and never
    /// reload, and every use takes one from the reserve.
    /// </summary>
    public bool UsesMagazine { get; init; } = true;

    /// <summary>Rounds per magazine; ignored when <see cref="UsesMagazine"/> is false.</summary>
    public int MagSize { get; init; } = 1;

    /// <summary>Largest reserve the player can carry; ignored when <see cref="InfiniteReserve"/> is set.</summary>
    public required int ReserveMax { get; init; }

    /// <summary>Seconds a reload takes, before the global multiplier.</summary>
    public float ReloadTime { get; init; }

    public required bool InfiniteReserve { get; init; }
}
