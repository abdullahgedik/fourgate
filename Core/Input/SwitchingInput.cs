using Fourgate.Core.Rendering;
using Microsoft.Xna.Framework;

namespace Fourgate.Core.Input;

/// <summary>
/// Lets one player use either of two devices, following whichever was used last.
/// Single-player uses it so keyboard+mouse and a gamepad both work.
/// </summary>
public sealed class SwitchingInput : IPlayerInput
{
    private readonly IPlayerInput _first;
    private readonly IPlayerInput _second;
    private IPlayerInput _current;

    public SwitchingInput(IPlayerInput first, IPlayerInput second)
    {
        _first = first;
        _second = second;
        _current = first;
    }

    public Vector2 Move => _current.Move;

    public Vector2 AimDirection => _current.AimDirection;

    public float AimDistance => _current.AimDistance;

    public Vector2 AimScreen => _current.AimScreen;

    public bool HasPointer => _current.HasPointer;

    public bool FireHeld => _current.FireHeld;

    public bool FirePressed => _current.FirePressed;

    public bool DashPressed => _current.DashPressed;

    public bool ReloadPressed => _current.ReloadPressed;

    public int WeaponCycle => _current.WeaponCycle;

    public int WeaponSlotPressed => _current.WeaponSlotPressed;

    public bool ConfirmPressed => _current.ConfirmPressed;

    public bool MenuLeftPressed => _current.MenuLeftPressed;

    public bool MenuRightPressed => _current.MenuRightPressed;

    public bool HasActivity => _current.HasActivity;

    public void Update(Vector2 playerPosition, Camera2D camera)
    {
        _first.Update(playerPosition, camera);
        _second.Update(playerPosition, camera);

        if (!_current.HasActivity)
        {
            var other = ReferenceEquals(_current, _first) ? _second : _first;
            if (other.HasActivity)
            {
                _current = other;
            }
        }
    }
}
