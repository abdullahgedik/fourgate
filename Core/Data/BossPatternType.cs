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
}
