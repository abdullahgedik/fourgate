namespace Fourgate.Core.Data;

/// <summary>Flight values of a thrown weapon (grenade, molotov).</summary>
public sealed class ThrownConfig
{
    /// <summary>World units per second along the ground.</summary>
    public required float Speed { get; init; }

    /// <summary>Farthest a throw can land from the player.</summary>
    public required float MaxRange { get; init; }

    /// <summary>Visual size of the thrown object.</summary>
    public required float Radius { get; init; }
}
