using System;
using System.Collections.Generic;
using Fourgate.Weapons;

namespace Fourgate.Systems;

/// <summary>How an ammo crate's contents are split between a player's weapons.</summary>
public static class AmmoDistribution
{
    /// <summary>True when at least one of the weapons has room for more reserve ammo.</summary>
    public static bool CanUse(IReadOnlyList<Weapon> weapons)
    {
        for (var i = 0; i < weapons.Count; i++)
        {
            if (weapons[i].NeedsAmmo)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Gives the weapon with the lowest reserve percentage a large share of its maximum
    /// reserve and every other weapon a small share. Returns false when nothing needed ammo.
    /// </summary>
    public static bool Distribute(IReadOnlyList<Weapon> weapons, float primaryFraction, float othersFraction)
    {
        Weapon? lowest = null;
        for (var i = 0; i < weapons.Count; i++)
        {
            var weapon = weapons[i];
            if (weapon.NeedsAmmo && (lowest is null || weapon.ReserveFraction < lowest.ReserveFraction))
            {
                lowest = weapon;
            }
        }

        if (lowest is null)
        {
            return false;
        }

        for (var i = 0; i < weapons.Count; i++)
        {
            var weapon = weapons[i];
            if (!weapon.NeedsAmmo)
            {
                continue;
            }

            var fraction = ReferenceEquals(weapon, lowest) ? primaryFraction : othersFraction;
            weapon.AddReserve((int)MathF.Ceiling(weapon.Config.Ammo.ReserveMax * fraction));
        }

        return true;
    }
}
