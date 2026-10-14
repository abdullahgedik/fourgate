using System;
using Fourgate.Core.Rendering;
using Fourgate.Weapons;
using Microsoft.Xna.Framework;

namespace Fourgate.Entities.Hazards;

/// <summary>A mine lying on the floor. Pooled; <see cref="Place"/> resets all state.</summary>
public sealed class Mine
{
    public const float VisualRadius = 12f;

    private const float BlinkSpeed = 6f;

    private static readonly Color BodyColor = new(55, 60, 70);
    private static readonly Color ArmingLightColor = new(255, 200, 60);
    private static readonly Color ArmedLightColor = new(255, 50, 50);

    private float _armLeft;
    private float _age;

    public MineShot Shot { get; private set; }

    public Vector2 Position => Shot.Position;

    /// <summary>Order of placing; lower is older.</summary>
    public long PlacedOrder { get; private set; }

    public bool IsArmed => _armLeft <= 0f;

    /// <summary>Set once it has been triggered; it explodes and is removed this frame.</summary>
    public bool IsDetonating { get; set; }

    public VisualDescriptor BodyVisual => new(ShapeKind.Circle, VisualRadius, BodyColor);

    /// <summary>A small light: yellow while arming, blinking red once armed.</summary>
    public VisualDescriptor LightVisual
    {
        get
        {
            var color = IsArmed ? ArmedLightColor : ArmingLightColor;
            var on = !IsArmed || MathF.Sin(_age * BlinkSpeed) > 0f;
            return new VisualDescriptor(ShapeKind.Circle, VisualRadius * 0.4f, on ? color : color * 0.3f);
        }
    }

    public void Place(in MineShot shot, long order)
    {
        Shot = shot;
        PlacedOrder = order;
        _armLeft = shot.ArmSeconds;
        _age = 0f;
        IsDetonating = false;
    }

    public void Tick(float deltaSeconds)
    {
        _age += deltaSeconds;
        _armLeft = Math.Max(0f, _armLeft - deltaSeconds);
    }
}
