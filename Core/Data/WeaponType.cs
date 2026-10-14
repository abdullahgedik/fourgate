namespace Fourgate.Core.Data;

/// <summary>How a weapon delivers its damage.</summary>
public enum WeaponType
{
    /// <summary>Fires fast straight-flying bullets.</summary>
    Projectile,

    /// <summary>Throws an object that flies to the aim point and explodes where it lands.</summary>
    Thrown,

    /// <summary>Put down at the player's feet; explodes when an enemy comes close.</summary>
    Placed,
}
