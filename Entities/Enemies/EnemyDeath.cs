using Fourgate.Core.Data;
using Microsoft.Xna.Framework;

namespace Fourgate.Entities.Enemies;

/// <summary>Record of an enemy that died, for rewards and drops.</summary>
/// <param name="Config">The enemy's type.</param>
/// <param name="Position">Where it died.</param>
/// <param name="KilledBy">Index of the player whose shot landed last.</param>
/// <param name="Gibbed">True when one big hit killed it: it bursts apart instead of falling over.</param>
public readonly record struct EnemyDeath(
    EnemyConfig Config, Vector2 Position, int KilledBy, float Facing = 0f, bool Gibbed = false);
