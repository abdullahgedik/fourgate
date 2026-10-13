namespace Fourgate.Core.Data;

/// <summary>Base player stats.</summary>
public sealed class PlayerConfig
{
    /// <summary>World units per second.</summary>
    public required float MoveSpeed { get; init; }

    public required float Radius { get; init; }

    /// <summary>Id of the weapon every player starts with.</summary>
    public required string StartingWeapon { get; init; }

    public required DashConfig Dash { get; init; }
}
