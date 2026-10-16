namespace Fourgate.Weapons;

/// <summary>Where a gun's shots leave from, measured from the body of the player holding it.</summary>
/// <param name="Forward">How far ahead of the body's centre, along the aim.</param>
/// <param name="Right">How far to the right of the aim line (negative: left).</param>
/// <param name="DualHalfSpread">With a gun in each hand, half the distance between the two muzzles.</param>
public readonly record struct MuzzleSpot(float Forward, float Right, float DualHalfSpread);
