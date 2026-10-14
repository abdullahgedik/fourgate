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
/// <param name="Shootable">True for a barrel: any hit by a player sets it off.</param>
/// <param name="FireRadius">Radius of the fire pool it leaves where it went off; 0 for none.</param>
/// <param name="FireSeconds">How long that fire pool burns.</param>
/// <param name="FireDamagePerSecond">Damage per second of that fire pool.</param>
/// <param name="FireHurtsPlayers">False when that fire pool spares players.</param>
/// <param name="Solid">True when nothing can walk through it; it then snaps to a grid cell.</param>
/// <param name="GridSize">Size of a grid cell for a solid one.</param>
/// <param name="EnemyHitsToDetonate">Zombie hits a solid one takes before going off; 0 when zombies leave it alone.</param>
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
    float BlastSpread = 0f,
    bool Shootable = false,
    float FireRadius = 0f,
    float FireSeconds = 0f,
    float FireDamagePerSecond = 0f,
    bool FireHurtsPlayers = true,
    bool Solid = false,
    float GridSize = 0f,
    int EnemyHitsToDetonate = 0);
