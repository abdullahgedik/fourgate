using Fourgate.Core.Input;

namespace Fourgate.Weapons;

/// <summary>The buttons that drive one weapon this frame: the fire button for guns, the equipment button for equipment.</summary>
/// <param name="Pressed">The trigger went down this frame.</param>
/// <param name="Held">The trigger is down.</param>
/// <param name="ReloadPressed">Reload was asked for this frame.</param>
/// <param name="AimDistance">World distance to the aim point; infinite without a pointer.</param>
public readonly record struct WeaponTrigger(bool Pressed, bool Held, bool ReloadPressed, float AimDistance)
{
    /// <summary>The fire button, as used by the gun in hand.</summary>
    public static WeaponTrigger ForGun(IPlayerInput input)
    {
        return new WeaponTrigger(input.FirePressed, input.FireHeld, input.ReloadPressed, input.AimDistance);
    }

    /// <summary>The equipment button. Equipment never reloads.</summary>
    public static WeaponTrigger ForEquipment(IPlayerInput input)
    {
        return new WeaponTrigger(input.EquipmentPressed, input.EquipmentHeld, false, input.AimDistance);
    }
}
