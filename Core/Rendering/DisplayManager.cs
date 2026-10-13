using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Fourgate.Core.Rendering;

/// <summary>Owns the window mode and windowed resolution. The game itself always renders at the virtual resolution.</summary>
public sealed class DisplayManager
{
    private static readonly Point[] WindowedResolutions =
    {
        new(1280, 720),
        new(1600, 900),
        new(1920, 1080),
        new(2560, 1440),
    };

    private readonly GraphicsDeviceManager _graphics;
    private int _resolutionIndex;

    public DisplayManager(GraphicsDeviceManager graphics)
    {
        _graphics = graphics;

        // Borderless: keep the desktop resolution instead of switching the display mode.
        _graphics.HardwareModeSwitch = false;
        _graphics.SynchronizeWithVerticalRetrace = true;
        _graphics.PreferredBackBufferWidth = WindowedResolutions[0].X;
        _graphics.PreferredBackBufferHeight = WindowedResolutions[0].Y;
    }

    public WindowMode Mode { get; private set; } = WindowMode.Windowed;

    public Point WindowedResolution => WindowedResolutions[_resolutionIndex];

    public void ToggleWindowMode()
    {
        Mode = Mode == WindowMode.Windowed ? WindowMode.BorderlessFullscreen : WindowMode.Windowed;
        Apply();
    }

    /// <summary>Switches to the next windowed resolution that fits the desktop. Ignored while borderless.</summary>
    public void CycleWindowedResolution()
    {
        if (Mode != WindowMode.Windowed)
        {
            return;
        }

        var desktop = GraphicsAdapter.DefaultAdapter.CurrentDisplayMode;
        for (var step = 1; step <= WindowedResolutions.Length; step++)
        {
            var index = (_resolutionIndex + step) % WindowedResolutions.Length;
            var candidate = WindowedResolutions[index];
            if (candidate.X <= desktop.Width && candidate.Y <= desktop.Height)
            {
                _resolutionIndex = index;
                break;
            }
        }

        Apply();
    }

    private void Apply()
    {
        if (Mode == WindowMode.BorderlessFullscreen)
        {
            var desktop = GraphicsAdapter.DefaultAdapter.CurrentDisplayMode;
            _graphics.PreferredBackBufferWidth = desktop.Width;
            _graphics.PreferredBackBufferHeight = desktop.Height;
            _graphics.IsFullScreen = true;
        }
        else
        {
            _graphics.PreferredBackBufferWidth = WindowedResolution.X;
            _graphics.PreferredBackBufferHeight = WindowedResolution.Y;
            _graphics.IsFullScreen = false;
        }

        _graphics.ApplyChanges();
    }
}
