using Fourgate.Core.Rendering;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace Fourgate.Core.Input;

/// <summary>
/// Debug stand-in for player 2's gamepad, on keys player 1 does not use: IJKL to move (aim
/// follows the last direction moved), U to fire, O to dash, P to reload, Y for equipment,
/// H to cycle equipment, N / M to cycle guns. U also confirms in menus. Only used when
/// <c>debug.keyboardPlayerTwo</c> is on in game.json.
/// </summary>
public sealed class DebugKeyboardInput : IPlayerInput
{
    private KeyboardState _previous;

    public Vector2 Move { get; private set; }

    public Vector2 AimDirection { get; private set; } = Vector2.UnitX;

    public float AimDistance => float.PositiveInfinity;

    public Vector2 AimScreen { get; private set; }

    public bool HasPointer => false;

    public bool FireHeld { get; private set; }

    public bool FirePressed { get; private set; }

    public bool DashPressed { get; private set; }

    public bool ReloadPressed { get; private set; }

    public int WeaponCycle { get; private set; }

    public int WeaponSlotPressed => 0;

    public bool EquipmentPressed { get; private set; }

    public bool EquipmentHeld { get; private set; }

    public int EquipmentCycle { get; private set; }

    public bool ConfirmPressed { get; private set; }

    public bool MenuLeftPressed { get; private set; }

    public bool MenuRightPressed { get; private set; }

    public bool HasActivity { get; private set; }

    public void Update(Vector2 playerPosition, Camera2D camera)
    {
        var keyboard = Keyboard.GetState();

        var move = Vector2.Zero;
        if (keyboard.IsKeyDown(Keys.I))
        {
            move.Y -= 1f;
        }

        if (keyboard.IsKeyDown(Keys.K))
        {
            move.Y += 1f;
        }

        if (keyboard.IsKeyDown(Keys.J))
        {
            move.X -= 1f;
        }

        if (keyboard.IsKeyDown(Keys.L))
        {
            move.X += 1f;
        }

        if (move != Vector2.Zero)
        {
            move.Normalize();
            AimDirection = move;
        }

        Move = move;
        AimScreen = camera.ViewCenter;

        FireHeld = keyboard.IsKeyDown(Keys.U);
        FirePressed = WasPressed(keyboard, Keys.U);
        DashPressed = WasPressed(keyboard, Keys.O);
        ReloadPressed = WasPressed(keyboard, Keys.P);
        EquipmentHeld = keyboard.IsKeyDown(Keys.Y);
        EquipmentPressed = WasPressed(keyboard, Keys.Y);
        EquipmentCycle = WasPressed(keyboard, Keys.H) ? 1 : 0;
        WeaponCycle = WasPressed(keyboard, Keys.M) ? 1 : WasPressed(keyboard, Keys.N) ? -1 : 0;
        ConfirmPressed = FirePressed;
        MenuLeftPressed = WasPressed(keyboard, Keys.J);
        MenuRightPressed = WasPressed(keyboard, Keys.L);

        HasActivity = move != Vector2.Zero || FireHeld || DashPressed || ReloadPressed || EquipmentHeld
            || EquipmentCycle != 0 || WeaponCycle != 0;

        _previous = keyboard;
    }

    private bool WasPressed(KeyboardState keyboard, Keys key)
    {
        return keyboard.IsKeyDown(key) && _previous.IsKeyUp(key);
    }
}
