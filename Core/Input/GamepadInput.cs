using System;
using Fourgate.Core.Rendering;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace Fourgate.Core.Input;

/// <summary>Gamepad controls (Xbox layout): left stick to move, right stick to aim, right trigger to fire.</summary>
public sealed class GamepadInput : IPlayerInput
{
    private const float TriggerThreshold = 0.5f;

    private readonly PlayerIndex _index;
    private GamePadState _previous;

    public GamepadInput(PlayerIndex index)
    {
        _index = index;
    }

    public bool IsConnected { get; private set; }

    public Vector2 Move { get; private set; }

    public Vector2 AimDirection { get; private set; } = Vector2.UnitX;

    public Vector2 AimScreen { get; private set; }

    public bool HasPointer => false;

    public bool FireHeld { get; private set; }

    public bool FirePressed { get; private set; }

    public bool DashPressed { get; private set; }

    public bool ReloadPressed { get; private set; }

    public bool HasActivity { get; private set; }

    public void Update(Vector2 playerPosition, Camera2D camera)
    {
        // The circular dead zone already zeroes a resting stick.
        var state = GamePad.GetState(_index, GamePadDeadZone.Circular);
        IsConnected = state.IsConnected;

        // Stick Y points up; world Y points down.
        var move = new Vector2(state.ThumbSticks.Left.X, -state.ThumbSticks.Left.Y);
        if (move.LengthSquared() > 1f)
        {
            move.Normalize();
        }

        Move = move;

        var aim = new Vector2(state.ThumbSticks.Right.X, -state.ThumbSticks.Right.Y);
        if (aim != Vector2.Zero)
        {
            AimDirection = Vector2.Normalize(aim);
        }

        // A fully tilted stick leads the camera as far as a cursor at the edge of the view.
        var reach = Math.Min(camera.ViewRect.Width, camera.ViewRect.Height) / 2f;
        AimScreen = camera.ViewCenter + aim * reach;

        var wasFiring = _previous.Triggers.Right > TriggerThreshold;
        FireHeld = state.Triggers.Right > TriggerThreshold;
        FirePressed = FireHeld && !wasFiring;
        DashPressed = state.IsButtonDown(Buttons.A) && _previous.IsButtonUp(Buttons.A);
        ReloadPressed = state.IsButtonDown(Buttons.X) && _previous.IsButtonUp(Buttons.X);

        HasActivity = move != Vector2.Zero || aim != Vector2.Zero || FireHeld || DashPressed || ReloadPressed;

        _previous = state;
    }
}
