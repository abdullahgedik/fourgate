namespace Fourgate.Core.Data;

/// <summary>Fire pool left by a fire weapon (molotov). The weapon's damage is its damage per second.</summary>
public sealed class FireConfig
{
    /// <summary>Everything within this distance of the center burns.</summary>
    public required float Radius { get; init; }

    /// <summary>How long the pool burns.</summary>
    public required float Seconds { get; init; }

    /// <summary>
    /// Seconds the splash takes to spread from where it lands to the full radius; it only burns
    /// as far as it has spread. 0 for all at once.
    /// </summary>
    public float SpreadSeconds { get; init; }
}
