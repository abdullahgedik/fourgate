using System;
using System.Diagnostics;
using System.Globalization;
using Fourgate.Core.Rendering;
using Microsoft.Xna.Framework;

namespace Fourgate.UI;

/// <summary>
/// A debug line in the top-left corner for checking performance: frames per second, the average
/// time spent updating and drawing, the worst frame of the last second, and how many enemies and
/// particles there are. The line is rebuilt once a second, so it costs next to nothing.
/// </summary>
public sealed class FrameTimeOverlay
{
    private const float TextSize = 22f;
    private static readonly Vector2 Position = new(16f, 12f);
    private static readonly Color TextColor = new(170, 255, 170);
    private static readonly Color BackColor = new Color(0, 0, 0) * 0.6f;

    private readonly Stopwatch _watch = new();
    private double _updateMs;
    private double _drawMs;
    private double _frameMs;
    private double _worstMs;
    private double _secondMs;
    private int _frames;
    private string _line = "measuring…";

    public bool Visible { get; set; }

    /// <summary>Enemies and particles, given by the game while a run is on.</summary>
    public int Enemies { get; set; }

    public int Particles { get; set; }

    public void BeginUpdate()
    {
        _watch.Restart();
    }

    public void EndUpdate()
    {
        var ms = _watch.Elapsed.TotalMilliseconds;
        _updateMs += ms;
        _frameMs = ms;
    }

    public void BeginDraw()
    {
        _watch.Restart();
    }

    /// <summary>Ends the frame's draw and, once a second, rebuilds the line.</summary>
    public void EndDraw(double realSeconds)
    {
        var ms = _watch.Elapsed.TotalMilliseconds;
        _drawMs += ms;
        _worstMs = Math.Max(_worstMs, _frameMs + ms);
        _frames++;
        _secondMs += realSeconds * 1000.0;
        if (_secondMs < 1000.0 || _frames == 0)
        {
            return;
        }

        var culture = CultureInfo.InvariantCulture;
        _line = string.Format(
            culture, "FPS {0:0}   update {1:0.0} ms   draw {2:0.0} ms   worst {3:0.0} ms   enemies {4}   particles {5}",
            _frames * 1000.0 / _secondMs, _updateMs / _frames, _drawMs / _frames, _worstMs, Enemies, Particles);
        _updateMs = 0;
        _drawMs = 0;
        _worstMs = 0;
        _secondMs = 0;
        _frames = 0;
    }

    /// <param name="renderer">Renderer in a screen-space pass.</param>
    public void Draw(IRenderer renderer)
    {
        if (!Visible)
        {
            return;
        }

        renderer.FillRect(Position - new Vector2(8f, 4f), new Vector2(_line.Length * TextSize * 0.5f + 16f, TextSize + 10f), BackColor);
        renderer.DrawText(_line, Position, TextSize, TextColor);
    }
}
