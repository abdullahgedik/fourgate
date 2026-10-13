namespace Fourgate.Core.Data;

/// <summary>Magazine, reserve and reload values of a weapon.</summary>
public sealed class WeaponAmmoConfig
{
    public required int MagSize { get; init; }

    /// <summary>Largest reserve the player can carry; ignored when <see cref="InfiniteReserve"/> is set.</summary>
    public required int ReserveMax { get; init; }

    /// <summary>Seconds a reload takes, before the global multiplier.</summary>
    public required float ReloadTime { get; init; }

    public required bool InfiniteReserve { get; init; }
}
