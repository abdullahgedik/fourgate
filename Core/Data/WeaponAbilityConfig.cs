namespace Fourgate.Core.Data;

/// <summary>One ability granted at weapon level 10.</summary>
public sealed class WeaponAbilityConfig
{
    public required WeaponAbility Id { get; init; }

    /// <summary>How many: enemies pierced, wall bounces. Not used by abilities without a count.</summary>
    public int Count { get; init; } = 1;

    /// <summary>Reach of the ability in world units (ricochet: how far a bullet looks for its next enemy).</summary>
    public float Radius { get; init; }

    /// <summary>Damage multiplier applied by the ability (ricochet: to the bullet after each enemy bounce).</summary>
    public float DamageMult { get; init; } = 1f;
}
