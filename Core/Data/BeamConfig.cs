namespace Fourgate.Core.Data;

/// <summary>
/// Values of a beam weapon (railgun): an instant line that hits every enemy along it and stops
/// at walls and pillars.
/// </summary>
public sealed class BeamConfig
{
    /// <summary>How far the beam reaches when nothing solid is in the way.</summary>
    public required float Range { get; init; }

    /// <summary>Half the beam's thickness; an enemy this close to the line is hit.</summary>
    public required float Width { get; init; }

    /// <summary>World units a hit pushes an enemy along the beam; 0 for none.</summary>
    public float Knockback { get; init; }
}
