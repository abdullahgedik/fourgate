using Fourgate.Core.Rendering;
using Microsoft.Xna.Framework;

namespace Fourgate.Core.Input;

/// <summary>One player's controls for the current frame. Gameplay reads only this, never a device.</summary>
public interface IPlayerInput
{
    /// <summary>Movement direction; length is between 0 and 1.</summary>
    Vector2 Move { get; }

    /// <summary>Unit-length aim direction in world space. Keeps its last value when there is no aim input.</summary>
    Vector2 AimDirection { get; }

    /// <summary>World distance from the player to the aim point; infinite when the device has no pointer.</summary>
    float AimDistance { get; }

    /// <summary>Point on the virtual screen the player is aiming toward; drives the camera lead.</summary>
    Vector2 AimScreen { get; }

    /// <summary>True when <see cref="AimScreen"/> is an on-screen pointer that should be shown as a crosshair.</summary>
    bool HasPointer { get; }

    bool FireHeld { get; }

    bool FirePressed { get; }

    bool DashPressed { get; }

    bool ReloadPressed { get; }

    /// <summary>-1 to switch to the previous gun, +1 to the next, 0 for no change.</summary>
    int WeaponCycle { get; }

    /// <summary>Gun slot number chosen directly this frame, or 0 for none.</summary>
    int WeaponSlotPressed { get; }

    /// <summary>The equipment button went down this frame.</summary>
    bool EquipmentPressed { get; }

    /// <summary>The equipment button is down.</summary>
    bool EquipmentHeld { get; }

    /// <summary>-1 or +1 to change the active equipment, 0 for no change.</summary>
    int EquipmentCycle { get; }

    /// <summary>Accept/continue in menus and on the game over screen.</summary>
    bool ConfirmPressed { get; }

    /// <summary>Move the menu selection one step left.</summary>
    bool MenuLeftPressed { get; }

    /// <summary>Move the menu selection one step right.</summary>
    bool MenuRightPressed { get; }

    bool MenuUpPressed { get; }

    bool MenuDownPressed { get; }

    /// <summary>True when any control of this device was used this frame.</summary>
    bool HasActivity { get; }

    /// <summary>False while the device is unplugged (a gamepad); keyboards are always connected.</summary>
    bool IsConnected { get; }

    /// <summary>Polls the device. Call once per simulation step, before reading the properties.</summary>
    /// <param name="playerPosition">The player's world position, used to turn a pointer into an aim direction.</param>
    /// <param name="camera">The player's camera.</param>
    void Update(Vector2 playerPosition, Camera2D camera);
}
