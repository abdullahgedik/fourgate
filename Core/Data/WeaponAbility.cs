namespace Fourgate.Core.Data;

/// <summary>Named level-10 weapon behaviors that can be referenced from <c>weapons.json</c>.</summary>
public enum WeaponAbility
{
    /// <summary>Bullets pass through this many enemies before stopping.</summary>
    Pierce,

    /// <summary>
    /// Bullets bounce this many times: off a wall, or from an enemy they hit to the nearest
    /// other enemy within the ability's radius.
    /// </summary>
    Ricochet,

    /// <summary>Every shot fires two bullets side by side.</summary>
    DualWield,

    /// <summary>Holding fire keeps shooting.</summary>
    FullAuto,

    /// <summary>
    /// An explosive scatters this many mini-bombs around its blast; they land at the ability's
    /// radius and hit with its damage and radius multipliers.
    /// </summary>
    Cluster,

    /// <summary>Every <c>count</c>-th shot deals the ability's <c>damageMult</c> times the damage.</summary>
    EmpoweredShot,

    /// <summary>
    /// While fire is held the fire rate climbs to the ability's <c>fireRateMult</c> over
    /// <c>seconds</c>, and settles back when fire is released.
    /// </summary>
    Overdrive,

    /// <summary>After a kill, the damage left over keeps flying and hits the next enemy in line.</summary>
    Overkill,

    /// <summary>
    /// Enemies caught in the weapon's fire keep burning for the ability's <c>seconds</c> after
    /// leaving it, and set the enemies touching them alight once.
    /// </summary>
    SpreadFire,

    /// <summary>A placed weapon can be set off the moment it is down.</summary>
    InstantArm,

    /// <summary>A placed weapon that explodes sets off the others in its blast.</summary>
    ChainDetonation,

    /// <summary>
    /// A triggered placed weapon explodes <c>count</c> times, <c>seconds</c> apart; every blast
    /// after the first is shifted by up to <c>radius</c> in a random direction.
    /// </summary>
    MultiBlast,

    /// <summary>Flames keep going after touching an enemy instead of stopping at it.</summary>
    PassThrough,

    /// <summary>
    /// While firing, every <c>interval</c> seconds a flame leaves fire pools of <c>radius</c> along
    /// its path, <c>spacing</c> apart, each burning for <c>seconds</c> at <c>damageMult</c> of the
    /// weapon's damage per second.
    /// </summary>
    FireTrail,

    /// <summary>
    /// <c>seconds</c> after an explosion, <c>count</c> smaller blasts evenly spaced around a ring
    /// <c>radius</c> away from it go off one by one in a random order, <c>interval</c> apart, with
    /// <c>damageMult</c> and <c>radiusMult</c> of the main blast.
    /// </summary>
    RingBlast,

    /// <summary>
    /// A placed explosive leaves a fire pool of <c>radius</c> where it went off, burning for
    /// <c>seconds</c> at <c>damageMult</c> of its damage per second; with <c>hurtsPlayers</c>
    /// false the fire spares players.
    /// </summary>
    LeaveFire,
}
