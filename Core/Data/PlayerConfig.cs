namespace Fourgate.Core.Data;

/// <summary>Base player stats.</summary>
public sealed class PlayerConfig
{
    /// <summary>World units per second.</summary>
    public required float MoveSpeed { get; init; }

    public required float Radius { get; init; }

    public required DashConfig Dash { get; init; }
}
