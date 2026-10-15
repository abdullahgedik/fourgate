using Fourgate.Core.Input;
using Fourgate.Core.Rendering;
using Microsoft.Xna.Framework;

namespace Fourgate.Tests;

/// <summary>Scripted input for driving a player in tests.</summary>
public sealed class FakeInput : IPlayerInput
{
    public Vector2 Move { get; set; }

    public Vector2 AimDirection { get; set; } = Vector2.UnitX;

    public float AimDistance { get; set; } = float.PositiveInfinity;

    public Vector2 AimScreen { get; set; }

    public bool HasPointer { get; set; }

    public bool FireHeld { get; set; }

    public bool FirePressed { get; set; }

    public bool DashPressed { get; set; }

    public bool ReloadPressed { get; set; }

    public int WeaponCycle { get; set; }

    public int WeaponSlotPressed { get; set; }

    public bool EquipmentPressed { get; set; }

    public bool EquipmentHeld { get; set; }

    public int EquipmentCycle { get; set; }

    public bool ConfirmPressed { get; set; }

    public bool MenuLeftPressed { get; set; }

    public bool MenuRightPressed { get; set; }

    public bool BackPressed { get; set; }

    public bool MenuUpPressed { get; set; }

    public bool MenuDownPressed { get; set; }

    public bool HasActivity { get; set; }

    public bool IsConnected { get; set; } = true;

    public void Update(Vector2 playerPosition, Camera2D camera)
    {
    }
}
