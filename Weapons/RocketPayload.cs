namespace Fourgate.Weapons;

/// <summary>What a rocket does when it stops: a blast, and optionally a ring of smaller blasts around it.</summary>
/// <param name="BlastRadius">Reach of the main blast; the rocket's damage is the blast's damage.</param>
/// <param name="RingCount">Smaller blasts in a ring around the main one; 0 for none.</param>
/// <param name="RingDistance">How far from the main blast the ring lies.</param>
/// <param name="RingDamageMult">Ring blast damage as a share of the main blast's.</param>
/// <param name="RingRadiusMult">Ring blast radius as a share of the main blast's.</param>
/// <param name="RingDelay">Seconds after the main blast that the first ring blast goes off.</param>
/// <param name="RingInterval">Seconds between one ring blast and the next; the order around the ring is random.</param>
public readonly record struct RocketPayload(
    float BlastRadius,
    int RingCount = 0,
    float RingDistance = 0f,
    float RingDamageMult = 1f,
    float RingRadiusMult = 1f,
    float RingDelay = 0f,
    float RingInterval = 0f);
