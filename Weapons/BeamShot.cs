using Microsoft.Xna.Framework;

namespace Fourgate.Weapons;

/// <summary>A beam being fired.</summary>
/// <param name="Origin">Where it starts.</param>
/// <param name="Direction">Unit direction it points.</param>
/// <param name="Damage">Damage to everything along it.</param>
/// <param name="Range">Longest reach when nothing solid is in the way.</param>
/// <param name="Width">Half its thickness for hitting.</param>
/// <param name="Knockback">World units a hit pushes an enemy along it.</param>
/// <param name="OwnerIndex">Index of the player who fired.</param>
/// <param name="EndBlastRadius">Radius of a blast where it ends; 0 for none.</param>
/// <param name="EndBlastDamage">Damage of that blast.</param>
public readonly record struct BeamShot(
    Vector2 Origin,
    Vector2 Direction,
    float Damage,
    float Range,
    float Width,
    float Knockback,
    int OwnerIndex,
    float EndBlastRadius = 0f,
    float EndBlastDamage = 0f);
