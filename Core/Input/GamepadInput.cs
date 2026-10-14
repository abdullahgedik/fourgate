using System;
using Fourgate.Core.Rendering;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace Fourgate.Core.Input;

/// <summary>Gamepad controls (Xbox layout): left stick to move, right stick to aim, right trigger to fire.</summary>
public sealed class GamepadInput : IPlayerInput
{
    private const float TriggerThreshold = 0.5f;
    private const float MenuStickThreshold = 0.5f;

    private readonly PlayerIndex _index;
    private GamePadState _previous;

    public GamepadInput(PlayerIndex index)
    {
        _index = index;
    }

    public bool IsConnected { get; private set; }

    public Vector2 Move { get; private set; }

    public Vector2 AimDirection { get; private set; } = Vector2.UnitX;

    // A stick gives a direction only; throws go as far as the weapon allows.
    public float AimDistance => float.PositiveInfinity;

    public Vector2 AimScreen { get; private set; }

    public bool HasPointer => false;

    public bool FireHeld { get; private set; }

    public bool FirePressed { get; private set; }

    public bool DashPressed { get; private set; }

    public bool ReloadPressed { get; private set; }

    public int WeaponCycle { get; private set; }

    public int WeaponSlotPressed => 0;

    public bool ConfirmPressed { get; private set; }

    public bool MenuLeftPressed { get; private set; }

    public bool MenuRightPressed { get; private set; }

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
        ConfirmPressed = (state.IsButtonDown(Buttons.A) && _previous.IsButtonUp(Buttons.A))
            || (state.IsButtonDown(Buttons.Start) && _previous.IsButtonUp(Buttons.Start));

        WeaponCycle = 0;
        if (state.IsButtonDown(Buttons.RightShoulder) && _previous.IsButtonUp(Buttons.RightShoulder))
        {
            WeaponCycle = 1;
        }
        else if (state.IsButtonDown(Buttons.LeftShoulder) && _previous.IsButtonUp(Buttons.LeftShoulder))
        {
            WeaponCycle = -1;
        }

        // A stick flick counts as one step, like a d-pad press.
        var stickX = state.ThumbSticks.Left.X;
        var previousStickX = _previous.ThumbSticks.Left.X;
        MenuLeftPressed = (state.IsButtonDown(Buttons.DPadLeft) && _previous.IsButtonUp(Buttons.DPadLeft))
            || (stickX < -MenuStickThreshold && previousStickX >= -MenuStickThreshold);
        MenuRightPressed = (state.IsButtonDown(Buttons.DPadRight) && _previous.IsButtonUp(Buttons.DPadRight))
            || (stickX > MenuStickThreshold && previousStickX <= MenuStickThreshold);

        HasActivity = move != Vector2.Zero || aim != Vector2.Zero || FireHeld || DashPressed || ReloadPressed
            || ConfirmPressed || MenuLeftPressed || MenuRightPressed || WeaponCycle != 0;

        _previous = state;
    }
}
