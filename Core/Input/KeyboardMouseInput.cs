using Fourgate.Core.Rendering;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace Fourgate.Core.Input;

/// <summary>
/// Player 1 controls: the mouse aims and fires, the keys come from <see cref="Bindings"/>
/// (WASD, Left Shift, R, G, T, Q / E by default; rebindable in Settings).
/// </summary>
public sealed class KeyboardMouseInput : IPlayerInput, IKeyCapture
{
    // Room for every key held at once that matters; more are ignored.
    private const int MaxKeysHeld = 16;

    // Index + 1 is the gun slot.
    private static readonly Keys[] SlotKeys =
    {
        Keys.D1, Keys.D2, Keys.D3, Keys.D4, Keys.D5, Keys.D6, Keys.D7, Keys.D8, Keys.D9,
    };

    private readonly VirtualScreen _screen;
    private readonly Keys[] _held = new Keys[MaxKeysHeld];
    private KeyboardState _previousKeyboard;
    private MouseState _previousMouse;

    public KeyboardMouseInput(VirtualScreen screen)
    {
        _screen = screen;
    }

    /// <summary>The keys in use; replace or change them to rebind.</summary>
    public KeyBindings Bindings { get; set; } = new();

    public Keys KeyPressed { get; private set; }

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

    public bool BackPressed { get; private set; }

    public bool PausePressed => BackPressed;

    public bool MenuUpPressed { get; private set; }

    public bool MenuDownPressed { get; private set; }

    public bool HasActivity { get; private set; }

    public bool IsConnected => true;

    public void Update(Vector2 playerPosition, Camera2D camera)
    {
        var keyboard = Keyboard.GetState();
        var mouse = Mouse.GetState();

        var keys = Bindings;
        var move = Vector2.Zero;
        if (keyboard.IsKeyDown(keys.MoveUp))
        {
            move.Y -= 1f;
        }

        if (keyboard.IsKeyDown(keys.MoveDown))
        {
            move.Y += 1f;
        }

        if (keyboard.IsKeyDown(keys.MoveLeft))
        {
            move.X -= 1f;
        }

        if (keyboard.IsKeyDown(keys.MoveRight))
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
        DashPressed = WasPressed(keyboard, keys.Dash);
        ReloadPressed = WasPressed(keyboard, keys.Reload);
        ConfirmPressed = keyboard.IsKeyDown(Keys.Enter) && _previousKeyboard.IsKeyUp(Keys.Enter);

        var wheel = mouse.ScrollWheelValue - _previousMouse.ScrollWheelValue;
        WeaponCycle = 0;
        if (wheel < 0 || WasPressed(keyboard, keys.NextGun))
        {
            WeaponCycle = 1;
        }
        else if (wheel > 0 || WasPressed(keyboard, keys.PreviousGun))
        {
            WeaponCycle = -1;
        }

        EquipmentHeld = keyboard.IsKeyDown(keys.Equipment);
        EquipmentPressed = WasPressed(keyboard, keys.Equipment);
        EquipmentCycle = WasPressed(keyboard, keys.EquipmentCycle) ? 1 : 0;

        WeaponSlotPressed = 0;
        for (var i = 0; i < SlotKeys.Length; i++)
        {
            if (WasPressed(keyboard, SlotKeys[i]))
            {
                WeaponSlotPressed = i + 1;
                break;
            }
        }

        MenuLeftPressed = WasPressed(keyboard, keys.MoveLeft) || WasPressed(keyboard, Keys.Left);
        MenuRightPressed = WasPressed(keyboard, keys.MoveRight) || WasPressed(keyboard, Keys.Right);
        MenuUpPressed = WasPressed(keyboard, keys.MoveUp) || WasPressed(keyboard, Keys.Up);
        BackPressed = WasPressed(keyboard, Keys.Escape);
        MenuDownPressed = WasPressed(keyboard, keys.MoveDown) || WasPressed(keyboard, Keys.Down);
        KeyPressed = FirstNewKey(keyboard);

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

    private Keys FirstNewKey(KeyboardState keyboard)
    {
        var count = System.Math.Min(keyboard.GetPressedKeyCount(), MaxKeysHeld);
        if (count == 0)
        {
            return Keys.None;
        }

        keyboard.GetPressedKeys(_held);
        for (var i = 0; i < count; i++)
        {
            if (_previousKeyboard.IsKeyUp(_held[i]))
            {
                return _held[i];
            }
        }

        return Keys.None;
    }

    private bool WasPressed(KeyboardState keyboard, Keys key)
    {
        return keyboard.IsKeyDown(key) && _previousKeyboard.IsKeyUp(key);
    }
}
