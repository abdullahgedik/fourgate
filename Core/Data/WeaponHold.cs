namespace Fourgate.Core.Data;

/// <summary>How a gun is held by the player's model.</summary>
public enum WeaponHold
{
    /// <summary>Shotguns, rifles, launchers, the flamethrower: at the chest, one hand under the barrel.</summary>
    TwoHands,

    /// <summary>Pistols and SMGs: aimed with one arm, the other one free.</summary>
    OneHand,

    /// <summary>Launchers: on the right shoulder, steadied with both hands.</summary>
    Shoulder,
}
