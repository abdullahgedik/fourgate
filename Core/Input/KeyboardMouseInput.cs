using Fourgate.Core.Rendering;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace Fourgate.Core.Input;

/// <summary>Player 1 controls: WASD to move, mouse to aim and fire.</summary>
public sealed class KeyboardMouseInput : IPlayerInput
{
    private const Keys EquipmentKey = Keys.G;
    private const Keys EquipmentCycleKey = Keys.T;

    // Index + 1 is the gun slot.
    private static readonly Keys[] SlotKeys =
    {
        Keys.D1, Keys.D2, Keys.D3, Keys.D4, Keys.D5, Keys.D6, Keys.D7, Keys.D8, Keys.D9,
    };

    private readonly VirtualScreen _screen;
    private KeyboardState _previousKeyboard;
    private MouseState _previousMouse;

    public KeyboardMouseInput(VirtualScreen screen)
    {
        _screen = screen;
    }

    public Vector2 Move { get; private set; }

    public Vector2 AimDirection { get; private set; } = Vector2.UnitX;

    public float AimDistance { get; private set; } = float.PositiveInfinity;

    public Vector2 AimScreen { get; private set; }

    public bool HasPointer => true;

    public bool FireHeld { get; private set; }

    public bool FirePressed { get; private set; }

    public bool DashPressed { get; private set; }

    public bool ReloadPressed { get; private set; }

    public int WeaponCycle { get; private set; }

    public int WeaponSlotPressed { get; private set; }

    public bool EquipmentPressed { get; private set; }

    public bool EquipmentHeld { get; private set; }

    public int EquipmentCycle { get; private set; }

    public bool ConfirmPressed { get; private set; }

    public bool MenuLeftPressed { get; private set; }

    public bool MenuRightPressed { get; private set; }

    public bool MenuUpPressed { get; private set; }

    public bool MenuDownPressed { get; private set; }

    public bool HasActivity { get; private set; }

    public bool IsConnected => true;

    public void Update(Vector2 playerPosition, Camera2D camera)
    {
        var keyboard = Keyboard.GetState();
        var mouse = Mouse.GetState();

        var move = Vector2.Zero;
        if (keyboard.IsKeyDown(Keys.W))
        {
            move.Y -= 1f;
        }

        if (keyboard.IsKeyDown(Keys.S))
        {
            move.Y += 1f;
        }

        if (keyboard.IsKeyDown(Keys.A))
        {
            move.X -= 1f;
        }

        if (keyboard.IsKeyDown(Keys.D))
        {
            move.X += 1f;
        }

        if (move != Vector2.Zero)
        {
            move.Normalize();
        }

        Move = move;

        AimScreen = _screen.Viewport.WindowToVirtual(new Vector2(mouse.X, mouse.Y));
        var toAim = camera.ScreenToWorld(AimScreen) - playerPosition;
        AimDistance = toAim.Length();
        if (toAim.LengthSquared() > 0.0001f)
        {
            AimDirection = Vector2.Normalize(toAim);
        }

        FireHeld = mouse.LeftButton == ButtonState.Pressed;
        FirePressed = FireHeld && _previousMouse.LeftButton == ButtonState.Released;
        DashPressed = keyboard.IsKeyDown(Keys.Space) && _previousKeyboard.IsKeyUp(Keys.Space);
        ReloadPressed = keyboard.IsKeyDown(Keys.R) && _previousKeyboard.IsKeyUp(Keys.R);
        ConfirmPressed = keyboard.IsKeyDown(Keys.Enter) && _previousKeyboard.IsKeyUp(Keys.Enter);

        var wheel = mouse.ScrollWheelValue - _previousMouse.ScrollWheelValue;
        WeaponCycle = 0;
        if (wheel < 0 || WasPressed(keyboard, Keys.E))
        {
            WeaponCycle = 1;
        }
        else if (wheel > 0 || WasPressed(keyboard, Keys.Q))
        {
            WeaponCycle = -1;
        }

        EquipmentHeld = keyboard.IsKeyDown(EquipmentKey);
        EquipmentPressed = WasPressed(keyboard, EquipmentKey);
        EquipmentCycle = WasPressed(keyboard, EquipmentCycleKey) ? 1 : 0;

        WeaponSlotPressed = 0;
        for (var i = 0; i < SlotKeys.Length; i++)
        {
            if (WasPressed(keyboard, SlotKeys[i]))
            {
                WeaponSlotPressed = i + 1;
                break;
            }
        }
        MenuLeftPressed = WasPressed(keyboard, Keys.A) || WasPressed(keyboard, Keys.Left);
        MenuRightPressed = WasPressed(keyboard, Keys.D) || WasPressed(keyboard, Keys.Right);
        MenuUpPressed = WasPressed(keyboard, Keys.W) || WasPressed(keyboard, Keys.Up);
        MenuDownPressed = WasPressed(keyboard, Keys.S) || WasPressed(keyboard, Keys.Down);

        HasActivity = move != Vector2.Zero
            || FireHeld
            || DashPressed
            || ReloadPressed
            || ConfirmPressed
            || WeaponCycle != 0
            || WeaponSlotPressed != 0
            || EquipmentHeld
            || EquipmentCycle != 0
            || MenuLeftPressed
            || MenuRightPressed
            || MenuUpPressed
            || MenuDownPressed
            || mouse.X != _previousMouse.X
            || mouse.Y != _previousMouse.Y;

        _previousKeyboard = keyboard;
        _previousMouse = mouse;
    }

    private bool WasPressed(KeyboardState keyboard, Keys key)
    {
        return keyboard.IsKeyDown(key) && _previousKeyboard.IsKeyUp(key);
    }
}
