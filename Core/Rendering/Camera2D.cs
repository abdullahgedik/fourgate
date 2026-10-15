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
    // Screen shake: "trauma" (0–1) builds up with hits and blasts and drains away; the picture
    // moves by the square of it, so small knocks barely show and big ones really shake.
    private const float MaxShakePixels = 32f;
    private const float TraumaDrainPerSecond = 1.8f;

    // The 3D view: how steeply it looks down (90 = straight down) and its field of view.
    private const float PitchDegrees = 70f;
    private const float FieldOfViewDegrees = 40f;
    private const float NearPlane = 10f;
    private const float FarPlane = 30000f;

    private readonly CameraConfig _config;
    private float _trauma;
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

    /// <summary>How shaken the view is now, 0–1.</summary>
    public float Trauma => _trauma;

    /// <summary>Screen-space offset of the shake this frame; only the picture moves, never aiming.</summary>
    public Vector2 ShakeOffset { get; private set; }

    /// <summary>Adds shake (0–1 total).</summary>
    public void AddTrauma(float amount)
    {
        _trauma = Math.Clamp(_trauma + amount, 0f, 1f);
    }

    /// <summary>Drains the shake and picks this frame's offset from two random numbers in -1..1.</summary>
    public void UpdateShake(float deltaSeconds, float noiseX, float noiseY)
    {
        _trauma = Math.Max(0f, _trauma - TraumaDrainPerSecond * deltaSeconds);
        var strength = MaxShakePixels * _trauma * _trauma;
        ShakeOffset = new Vector2(noiseX, noiseY) * strength;
    }

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
        if (!Is3D)
        {
            return ViewCenter + (world - Position) * Zoom;
        }

        var clip = Vector4.Transform(new Vector4(world.X, 0f, world.Y, 1f), BuildView(withShake: false) * Projection3D);
        if (clip.W <= 0.0001f)
        {
            return ViewCenter;
        }

        return new Vector2(
            ViewRect.X + (clip.X / clip.W + 1f) * 0.5f * ViewRect.Width,
            ViewRect.Y + (1f - clip.Y / clip.W) * 0.5f * ViewRect.Height);
    }

    /// <summary>Converts a virtual-screen position (e.g. the mouse) to world coordinates.</summary>
    public Vector2 ScreenToWorld(Vector2 screen)
    {
        if (!Is3D)
        {
            return Position + (screen - ViewCenter) / Zoom;
        }

        // A ray through the pixel, from the camera onto the floor (height 0).
        var x = (screen.X - ViewRect.X) / ViewRect.Width * 2f - 1f;
        var y = 1f - (screen.Y - ViewRect.Y) / ViewRect.Height * 2f;
        var inverse = Matrix.Invert(BuildView(withShake: false) * Projection3D);
        var near = Vector4.Transform(new Vector4(x, y, 0f, 1f), inverse);
        var far = Vector4.Transform(new Vector4(x, y, 1f, 1f), inverse);
        var from = new Vector3(near.X, near.Y, near.Z) / near.W;
        var to = new Vector3(far.X, far.Y, far.Z) / far.W;
        var direction = to - from;
        if (MathF.Abs(direction.Y) < 0.0001f)
        {
            return Position + (screen - ViewCenter) / Zoom;
        }

        var hit = from + direction * (-from.Y / direction.Y);
        return new Vector2(hit.X, hit.Z);
    }

    /// <summary>
    /// True while the 3D renderer draws this view: a tilted perspective camera over the same
    /// spot, and screen/world conversions (aiming) go through it. Set by the renderer.
    /// </summary>
    public bool Is3D { get; set; }

    /// <summary>The 3D camera, shake included (for drawing).</summary>
    public Matrix View3D => BuildView(withShake: true);

    public Matrix Projection3D =>
        Matrix.CreatePerspectiveFieldOfView(
            MathHelper.ToRadians(FieldOfViewDegrees), ViewRect.Width / (float)ViewRect.Height, NearPlane, FarPlane);

    // World (x, y) is (x, 0, y) in 3D; the camera looks down from the +z side, so screen up is
    // still world -y. Its distance shows about as much floor as the 2D view at this zoom.
    private Matrix BuildView(bool withShake)
    {
        var shake = withShake ? ShakeOffset / Zoom : Vector2.Zero;
        var target = new Vector3(Position.X + shake.X, 0f, Position.Y + shake.Y);
        var distance = ViewRect.Height / Zoom / 2f / MathF.Tan(MathHelper.ToRadians(FieldOfViewDegrees) / 2f);
        var pitch = MathHelper.ToRadians(PitchDegrees);
        var eye = target + new Vector3(0f, distance * MathF.Sin(pitch), distance * MathF.Cos(pitch));
        return Matrix.CreateLookAt(eye, target, Vector3.Up);
    }

    public Matrix GetTransform()
    {
        var center = ViewCenter;
        return Matrix.CreateTranslation(-Position.X, -Position.Y, 0f)
            * Matrix.CreateScale(Zoom, Zoom, 1f)
            * Matrix.CreateTranslation(center.X + ShakeOffset.X, center.Y + ShakeOffset.Y, 0f);
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
