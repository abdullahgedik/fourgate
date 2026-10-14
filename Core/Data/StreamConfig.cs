namespace Fourgate.Core.Data;

/// <summary>
/// Values of a stream weapon (flamethrower): it sprays short-lived flames, one per shot.
/// The weapon's damage is its damage per second, spread over the flames it fires each second.
/// </summary>
public sealed class StreamConfig
{
    /// <summary>World units per second a flame travels.</summary>
    public required float Speed { get; init; }

    /// <summary>How far a flame reaches before it dies out.</summary>
    public required float Range { get; init; }

    /// <summary>Size of a flame as it leaves the nozzle.</summary>
    public required float RadiusStart { get; init; }

    /// <summary>Size of a flame at the end of its reach.</summary>
    public required float RadiusEnd { get; init; }

    /// <summary>True when flames keep going after touching an enemy, from level 1.</summary>
    public bool PassThrough { get; init; }
}
