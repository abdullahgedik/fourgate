using Microsoft.Xna.Framework.Input;

namespace Fourgate.Core.Input;

/// <summary>
/// Always-on keyboard shortcuts for the display: window mode, window size, 2D / 3D look.
/// </summary>
public sealed class WindowHotkeys
{
    private const Keys ToggleWindowModeKey = Keys.F11;
    // Not F10: Windows treats it as the window-menu key and takes focus away.
    private const Keys CycleResolutionKey = Keys.F9;
    private const Keys ToggleRendererKey = Keys.F12;

    private KeyboardState _previous;

    public bool ToggleWindowModePressed { get; private set; }

    public bool CycleResolutionPressed { get; private set; }

    /// <summary>Switch between the 2D and the experimental 3D look.</summary>
    public bool ToggleRendererPressed { get; private set; }

    public void Update()
    {
        var current = Keyboard.GetState();
        ToggleWindowModePressed = current.IsKeyDown(ToggleWindowModeKey) && _previous.IsKeyUp(ToggleWindowModeKey);
        CycleResolutionPressed = current.IsKeyDown(CycleResolutionKey) && _previous.IsKeyUp(CycleResolutionKey);
        ToggleRendererPressed = current.IsKeyDown(ToggleRendererKey) && _previous.IsKeyUp(ToggleRendererKey);
        _previous = current;
    }
}
