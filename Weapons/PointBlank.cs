namespace Fourgate.Weapons;

/// <summary>Bullets that hit close to where they were fired hit harder and push further.</summary>
/// <param name="Range">How far from the muzzle a hit still counts as point blank.</param>
/// <param name="DamageMult">Damage multiplier for a point-blank hit.</param>
/// <param name="KnockbackMult">Knockback multiplier for a point-blank hit.</param>
public readonly record struct PointBlank(float Range, float DamageMult, float KnockbackMult);
