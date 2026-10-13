namespace Fourgate.Core.Data;

/// <summary>One ability granted at weapon level 10.</summary>
public sealed class WeaponAbilityConfig
{
    public required WeaponAbility Id { get; init; }

    /// <summary>How many: enemies pierced, wall bounces. Not used by abilities without a count.</summary>
    public int Count { get; init; } = 1;
}
