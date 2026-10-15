namespace Fourgate.Core.Data;

/// <summary>Base player stats.</summary>
public sealed class PlayerConfig
{
    public required float MaxHp { get; init; }

    /// <summary>World units per second.</summary>
    public required float MoveSpeed { get; init; }

    public required float Radius { get; init; }

    /// <summary>Id of the weapon every player starts with.</summary>
    public required string StartingWeapon { get; init; }

    public required DashConfig Dash { get; init; }

    /// <summary>The player's 3D model; null draws the plain shape.</summary>
    public string? Model { get; init; }

    /// <summary>The gun model held when the weapon in hand has none of its own.</summary>
    public string? DefaultGunModel { get; init; }
}
