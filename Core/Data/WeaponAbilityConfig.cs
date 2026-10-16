namespace Fourgate.Core.Data;

/// <summary>One ability granted at weapon level 10.</summary>
public sealed class WeaponAbilityConfig
{
    public required WeaponAbility Id { get; init; }

    /// <summary>How many: enemies pierced, bounces to another enemy. Not used by abilities without a count.</summary>
    public int Count { get; init; } = 1;

    /// <summary>Ricochet: bounces off walls and pillars, apart from the bounces to enemies.</summary>
    public int WallBounces { get; init; }

    /// <summary>Reach of the ability in world units (ricochet: how far a bullet looks for its next enemy).</summary>
    public float Radius { get; init; }

    /// <summary>Damage multiplier applied by the ability (ricochet: to the bullet after each enemy bounce).</summary>
    public float DamageMult { get; init; } = 1f;

    /// <summary>Size multiplier applied by the ability (cluster: mini-bomb blast radius).</summary>
    public float RadiusMult { get; init; } = 1f;

    /// <summary>Fire rate multiplier reached by the ability (overdrive: at full ramp).</summary>
    public float FireRateMult { get; init; } = 1f;

    /// <summary>Knockback multiplier applied by the ability (point blank: to close hits).</summary>
    public float KnockbackMult { get; init; } = 1f;

    /// <summary>Time the ability takes, in seconds (overdrive: to ramp up fully).</summary>
    public float Seconds { get; init; }

    /// <summary>Seconds between repeats of the ability (fire trail: between the flames that leave pools).</summary>
    public float Interval { get; init; }

    /// <summary>Distance between things the ability leaves along a path (fire trail: between its pools).</summary>
    public float Spacing { get; init; }

    /// <summary>False when what the ability leaves behind (fire) only hurts enemies.</summary>
    public bool HurtsPlayers { get; init; } = true;
}
