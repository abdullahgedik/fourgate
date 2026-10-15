namespace Fourgate.Core.Data;

/// <summary>Flight values of a weapon's bullets.</summary>
public sealed class ProjectileConfig
{
    /// <summary>World units per second.</summary>
    public required float Speed { get; init; }

    /// <summary>Collision radius; widens the line a bullet sweeps.</summary>
    public required float Radius { get; init; }

    /// <summary>
    /// World units a bullet travels before it disappears; null for no limit (it flies on until
    /// it hits something or the arena's wall).
    /// </summary>
    public float? Range { get; init; }

    /// <summary>World units a hit pushes an enemy along the bullet's path; 0 for none.</summary>
    public float Knockback { get; init; }

    /// <summary>What a hit throws off (purely visual).</summary>
    public HitEffect HitEffect { get; init; } = HitEffect.Light;
}
