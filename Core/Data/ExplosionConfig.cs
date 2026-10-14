namespace Fourgate.Core.Data;

/// <summary>Blast of an explosive weapon.</summary>
public sealed class ExplosionConfig
{
    /// <summary>Everything within this distance of the center takes the weapon's damage.</summary>
    public required float Radius { get; init; }
}
