namespace Fourgate.Core.Data;

/// <summary>Reusable boss attack patterns; any boss can list any of them in <c>bosses.json</c>.</summary>
public enum BossPatternType
{
    /// <summary>Rushes in a straight line at the player, hurting anyone in the way.</summary>
    LineCharge,

    /// <summary>Slams the ground; a shockwave ring spreads out from the boss.</summary>
    GroundSlamRing,

    /// <summary>Calls in a group of enemies around itself.</summary>
    Summon,

    /// <summary>Throws fireballs in every direction at once, in one or more bursts.</summary>
    FireballRing,

    /// <summary>Spins on the spot throwing fireballs from rotating arms for a while.</summary>
    FireballSpiral,

    /// <summary>Vanishes and reappears just inside another door.</summary>
    Teleport,

    /// <summary>Breathes a long stream of fire that sweeps round while it lasts.</summary>
    FireStream,

    /// <summary>Speeds up for a while.</summary>
    Haste,

    /// <summary>Throws bursts of fireballs fanned out at the player, aimed when the warning starts.</summary>
    FireballVolley,

    /// <summary>Fires a long thin beam that sweeps across where the player stood.</summary>
    LaserSweep,

    /// <summary>Leaps into the air and slams down where the player stood, hurting everyone near.</summary>
    LeapSlam,

    /// <summary>Calls missiles down from the sky onto marked spots round the player.</summary>
    MissileRain,

    /// <summary>Throws a long line of fireballs one after another, slowly following the player.</summary>
    FireballStream,
}
