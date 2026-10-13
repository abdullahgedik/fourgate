using System;
using Fourgate.Core.Data;
using Microsoft.Xna.Framework;

namespace Fourgate.Core.Rendering;

/// <summary>
/// Follow camera for one view. Maps world coordinates into a rectangle of the virtual
/// screen (the whole screen in single-player, one half in split-screen).
/// </summary>
public sealed class Camera2D
{
    private readonly CameraConfig _config;
    private Vector2 _boundsMin;
    private Vector2 _boundsMax;
    private bool _hasBounds;

    public Camera2D(Rectangle viewRect, CameraConfig config)
    {
        ViewRect = viewRect;
        _config = config;
    }

    /// <summary>Area of the virtual screen this camera draws into.</summary>
    public Rectangle ViewRect { get; set; }

    /// <summary>World point shown at the center of the view.</summary>
    public Vector2 Position { get; private set; }

    public float Zoom => _config.Zoom;

    public Vector2 ViewCenter => new(ViewRect.X + ViewRect.Width / 2f, ViewRect.Y + ViewRect.Height / 2f);

    /// <summary>Limits the visible area to this world rectangle plus the configured edge margin.</summary>
    public void SetBounds(Vector2 min, Vector2 max)
    {
        _boundsMin = min;
        _boundsMax = max;
        _hasBounds = true;
    }

    public Vector2 WorldToScreen(Vector2 world)
    {
        return ViewCenter + (world - Position) * Zoom;
    }

    /// <summary>Converts a virtual-screen position (e.g. the mouse) to world coordinates.</summary>
    public Vector2 ScreenToWorld(Vector2 screen)
    {
        return Position + (screen - ViewCenter) / Zoom;
    }

    public Matrix GetTransform()
    {
        var center = ViewCenter;
        return Matrix.CreateTranslation(-Position.X, -Position.Y, 0f)
            * Matrix.CreateScale(Zoom, Zoom, 1f)
            * Matrix.CreateTranslation(center.X, center.Y, 0f);
    }

    /// <summary>Moves toward the follow target for this frame.</summary>
    /// <param name="focus">World position to follow (the player).</param>
    /// <param name="aimScreen">Aim point in virtual-screen coordinates.</param>
    /// <param name="deltaSeconds">Frame time.</param>
    public void Follow(Vector2 focus, Vector2 aimScreen, float deltaSeconds)
    {
        var target = Clamp(GetTarget(focus, aimScreen));
        var blend = _config.Smoothing > 0f ? 1f - MathF.Exp(-_config.Smoothing * deltaSeconds) : 1f;
        Position = Clamp(Vector2.Lerp(Position, target, blend));
    }

    /// <summary>Jumps straight to the follow target (spawn, revive, view change).</summary>
    public void SnapTo(Vector2 focus, Vector2 aimScreen)
    {
        Position = Clamp(GetTarget(focus, aimScreen));
    }

    private Vector2 GetTarget(Vector2 focus, Vector2 aimScreen)
    {
        // The lead is measured from the view center, not from the player, so moving the
        // camera does not feed back into its own target.
        var lead = (aimScreen - ViewCenter) / Zoom * _config.LeadFraction;
        var length = lead.Length();
        if (length > _config.LeadMaxDistance && length > 0f)
        {
            lead *= _config.LeadMaxDistance / length;
        }

        return focus + lead;
    }

    private Vector2 Clamp(Vector2 position)
    {
        if (!_hasBounds)
        {
            return position;
        }

        var halfView = new Vector2(ViewRect.Width, ViewRect.Height) / (2f * Zoom);
        var margin = _config.EdgeMargin;
        return new Vector2(
            ClampAxis(position.X, _boundsMin.X - margin, _boundsMax.X + margin, halfView.X),
            ClampAxis(position.Y, _boundsMin.Y - margin, _boundsMax.Y + margin, halfView.Y));
    }

    private static float ClampAxis(float value, float min, float max, float halfView)
    {
        var low = min + halfView;
        var high = max - halfView;

        // The view is larger than the bounds on this axis: keep the bounds centered.
        return low > high ? (min + max) / 2f : Math.Clamp(value, low, high);
    }
}
