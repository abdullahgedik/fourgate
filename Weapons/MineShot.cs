using Microsoft.Xna.Framework;

namespace Fourgate.Weapons;

/// <summary>A mine being put down.</summary>
/// <param name="Position">Where it lies.</param>
/// <param name="Damage">Damage of its blast.</param>
/// <param name="BlastRadius">Reach of its blast.</param>
/// <param name="TriggerRadius">An enemy this close sets it off.</param>
/// <param name="ArmSeconds">Delay before it can be set off.</param>
/// <param name="MaxPlaced">Most mines of this kind the owner can have down at once; 0 for no limit.</param>
/// <param name="ChainDetonation">When it explodes, it sets off other mines in its blast.</param>
/// <param name="OwnerIndex">Index of the player who placed it.</param>
/// <param name="WeaponId">The kind of mine, so the cap counts mines of the same kind.</param>
/// <param name="BlastCount">Explosions once triggered.</param>
/// <param name="BlastInterval">Seconds between those explosions.</param>
/// <param name="BlastSpread">How far each explosion after the first may be shifted from the mine.</param>
public readonly record struct MineShot(
    Vector2 Position,
    float Damage,
    float BlastRadius,
    float TriggerRadius,
    float ArmSeconds,
    int MaxPlaced,
    bool ChainDetonation,
    int OwnerIndex,
    string WeaponId,
    int BlastCount = 1,
    float BlastInterval = 0f,
    float BlastSpread = 0f);
