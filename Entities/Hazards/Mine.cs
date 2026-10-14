using System;
using Fourgate.Core.Rendering;
using Fourgate.Weapons;
using Microsoft.Xna.Framework;

namespace Fourgate.Entities.Hazards;

/// <summary>
/// A placed explosive lying on the floor: a mine set off by enemies coming close, or a
/// barrel set off by players hitting it. Pooled; <see cref="Place"/> resets all state.
/// </summary>
public sealed class Mine : IHittable
{
    public const float VisualRadius = 12f;

    // A barrel without a grid; a gridded one nearly fills its cell.
    private const float LooseBarrelRadius = 18f;
    private const float GridGap = 2f;

    private const float BlinkSpeed = 6f;

    private static readonly Color BodyColor = new(55, 60, 70);
    private static readonly Color ArmingLightColor = new(255, 200, 60);
    private static readonly Color ArmedLightColor = new(255, 50, 50);
    private static readonly Color BarrelColor = new(190, 50, 40);
    private static readonly Color BarrelBandColor = new(240, 200, 60);

    private float _armLeft;
    private float _age;

    public MineShot Shot { get; private set; }

    public Vector2 Position => Shot.Position;

    /// <summary>Half the side of a barrel, or a mine's radius.</summary>
    public float Radius => !Shot.Shootable
        ? VisualRadius
        : Shot.GridSize > 0f ? Shot.GridSize / 2f - GridGap : LooseBarrelRadius;

    // Any hit sets a barrel off.
    public float HitPointsLeft => 1f;

    /// <summary>Order of placing; lower is older.</summary>
    public long PlacedOrder { get; private set; }

    public bool IsArmed => _armLeft <= 0f;

    /// <summary>A player hit it; it goes off on the next update.</summary>
    public bool IsTriggered { get; private set; }

    /// <summary>Set once it goes off; it explodes and is removed this frame.</summary>
    public bool IsDetonating { get; set; }

    public VisualDescriptor BodyVisual => Shot.Shootable
        ? new VisualDescriptor(ShapeKind.Square, Radius, BarrelColor)
        : new VisualDescriptor(ShapeKind.Circle, VisualRadius, BodyColor);

    /// <summary>A mine's small light (yellow while arming, blinking red once armed), or a barrel's warning band.</summary>
    public VisualDescriptor LightVisual
    {
        get
        {
            if (Shot.Shootable)
            {
                return new VisualDescriptor(ShapeKind.Square, Radius * 0.45f, BarrelBandColor);
            }

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
        IsTriggered = false;
        IsDetonating = false;
    }

    public void Tick(float deltaSeconds)
    {
        _age += deltaSeconds;
        _armLeft = Math.Max(0f, _armLeft - deltaSeconds);
    }

    public void Hit(float damage, int ownerIndex)
    {
        if (Shot.Shootable)
        {
            IsTriggered = true;
        }
    }

    public void Burn(float damage, int ownerIndex)
    {
        Hit(damage, ownerIndex);
    }

    // Barrels do not catch fire; burning simply sets them off.
    public void Ignite(float seconds, float damagePerSecond, int ownerIndex)
    {
    }
}
