using System.Collections.Generic;
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

    /// <summary>The windowed sizes on offer that fit the desktop.</summary>
    public IReadOnlyList<Point> AvailableResolutions
    {
        get
        {
            var desktop = GraphicsAdapter.DefaultAdapter.CurrentDisplayMode;
            var fitting = new List<Point>();
            foreach (var candidate in WindowedResolutions)
            {
                if (candidate.X <= desktop.Width && candidate.Y <= desktop.Height)
                {
                    fitting.Add(candidate);
                }
            }

            return fitting;
        }
    }

    /// <summary>
    /// Applies saved settings: the window mode, and the saved windowed size if it is one of the
    /// sizes on offer and fits the desktop (otherwise the current one stays).
    /// </summary>
    public void Apply(WindowMode mode, Point windowedResolution)
    {
        var desktop = GraphicsAdapter.DefaultAdapter.CurrentDisplayMode;
        for (var i = 0; i < WindowedResolutions.Length; i++)
        {
            var candidate = WindowedResolutions[i];
            if (candidate == windowedResolution && candidate.X <= desktop.Width && candidate.Y <= desktop.Height)
            {
                _resolutionIndex = i;
            }
        }

        Mode = mode;
        Apply();
    }

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
