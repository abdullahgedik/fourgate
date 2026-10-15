using Microsoft.Xna.Framework.Input;

namespace Fourgate.Core.Input;

/// <summary>
/// Always-on keyboard shortcuts for the display: window mode and window size.
/// </summary>
public sealed class WindowHotkeys
{
    private const Keys ToggleWindowModeKey = Keys.F11;
    // Not F10: Windows treats it as the window-menu key and takes focus away.
    private const Keys CycleResolutionKey = Keys.F9;

    private KeyboardState _previous;

    public bool ToggleWindowModePressed { get; private set; }

    public bool CycleResolutionPressed { get; private set; }

    public void Update()
    {
        var current = Keyboard.GetState();
        ToggleWindowModePressed = current.IsKeyDown(ToggleWindowModeKey) && _previous.IsKeyUp(ToggleWindowModeKey);
        CycleResolutionPressed = current.IsKeyDown(CycleResolutionKey) && _previous.IsKeyUp(CycleResolutionKey);
        _previous = current;
    }
}
