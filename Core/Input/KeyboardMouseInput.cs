using Fourgate.Core.Rendering;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace Fourgate.Core.Input;

/// <summary>Player 1 controls: WASD to move, mouse to aim and fire.</summary>
public sealed class KeyboardMouseInput : IPlayerInput
{
    private readonly VirtualScreen _screen;
    private KeyboardState _previousKeyboard;
    private MouseState _previousMouse;

    public KeyboardMouseInput(VirtualScreen screen)
    {
        _screen = screen;
    }

    public Vector2 Move { get; private set; }

    public Vector2 AimDirection { get; private set; } = Vector2.UnitX;

    public Vector2 AimScreen { get; private set; }

    public bool HasPointer => true;

    public bool FireHeld { get; private set; }

    public bool FirePressed { get; private set; }

    public bool DashPressed { get; private set; }

    public bool ReloadPressed { get; private set; }

    public bool ConfirmPressed { get; private set; }

    public bool HasActivity { get; private set; }

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
        if (toAim.LengthSquared() > 0.0001f)
        {
            AimDirection = Vector2.Normalize(toAim);
        }

        FireHeld = mouse.LeftButton == ButtonState.Pressed;
        FirePressed = FireHeld && _previousMouse.LeftButton == ButtonState.Released;
        DashPressed = keyboard.IsKeyDown(Keys.Space) && _previousKeyboard.IsKeyUp(Keys.Space);
        ReloadPressed = keyboard.IsKeyDown(Keys.R) && _previousKeyboard.IsKeyUp(Keys.R);
        ConfirmPressed = keyboard.IsKeyDown(Keys.Enter) && _previousKeyboard.IsKeyUp(Keys.Enter);

        HasActivity = move != Vector2.Zero
            || FireHeld
            || DashPressed
            || ReloadPressed
            || ConfirmPressed
            || mouse.X != _previousMouse.X
            || mouse.Y != _previousMouse.Y;

        _previousKeyboard = keyboard;
        _previousMouse = mouse;
    }
}
