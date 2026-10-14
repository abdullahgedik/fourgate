using Microsoft.Xna.Framework;

namespace Fourgate.Weapons;

/// <summary>A thrown explosive on its way to where it will land.</summary>
/// <param name="Origin">World position it leaves from.</param>
/// <param name="Target">World position it lands and explodes at.</param>
/// <param name="Speed">World units per second along the ground.</param>
/// <param name="VisualRadius">Size of the thrown object.</param>
/// <param name="Damage">Damage dealt to everything in the blast.</param>
/// <param name="BlastRadius">Reach of the blast.</param>
/// <param name="OwnerIndex">Index of the player who threw it.</param>
/// <param name="ClusterCount">Mini-bombs scattered when it explodes; 0 for none.</param>
/// <param name="ClusterDistance">How far from the blast the mini-bombs land.</param>
/// <param name="ClusterDamageMult">Mini-bomb damage as a share of this explosive's damage.</param>
/// <param name="ClusterRadiusMult">Mini-bomb blast radius as a share of this explosive's blast radius.</param>
public readonly record struct ExplosiveShot(
    Vector2 Origin,
    Vector2 Target,
    float Speed,
    float VisualRadius,
    float Damage,
    float BlastRadius,
    int OwnerIndex,
    int ClusterCount = 0,
    float ClusterDistance = 0f,
    float ClusterDamageMult = 1f,
    float ClusterRadiusMult = 1f);
