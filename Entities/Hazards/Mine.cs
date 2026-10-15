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

    /// <summary>The yellow of a barrel's bands.</summary>
    public static Color BandColor => BarrelBandColor;

    private float _armLeft;
    private float _flightLeft;
    private float _fuseLeft = -1f;

    // How fast a burning fuse makes a barrel flash.
    private const float FuseFlashSpeed = 45f;
    private float _flightTime;
    private float _age;
    private int _enemyHitsTaken;

    public MineShot Shot { get; private set; }

    /// <summary>Where it is now: on its way while thrown, then where it landed.</summary>
    public Vector2 Position => IsFlying ? Vector2.Lerp(Shot.From, Shot.Position, FlightProgress) : Shot.Position;

    /// <summary>True while a thrown one is still in the air.</summary>
    public bool IsFlying => _flightLeft > 0f;

    /// <summary>0 when thrown, 1 on landing.</summary>
    public float FlightProgress => _flightTime > 0f ? 1f - _flightLeft / _flightTime : 1f;

    /// <summary>0 on the ground, 1 at the top of the throw.</summary>
    public float ArcHeight => IsFlying ? MathF.Sin(MathF.PI * FlightProgress) : 0f;

    /// <summary>True while a chain has lit its fuse: it goes off when the fuse burns down.</summary>
    public bool IsFused => _fuseLeft >= 0f;

    /// <summary>True on the update a thrown one lands.</summary>
    public bool JustLanded { get; private set; }

    /// <summary>Half the side of a barrel, or a mine's radius.</summary>
    public float Radius => !Shot.Shootable
        ? VisualRadius
        : Shot.GridSize > 0f ? Shot.GridSize / 2f - GridGap : LooseBarrelRadius;

    // Any hit sets a barrel off.
    public float HitPointsLeft => 1f;

    /// <summary>Order of placing; lower is older.</summary>
    public long PlacedOrder { get; private set; }

    /// <summary>Its arming delay only starts once it lies on the floor.</summary>
    public bool IsArmed => !IsFlying && _armLeft <= 0f;

    /// <summary>A player hit it; it goes off on the next update.</summary>
    public bool IsTriggered { get; private set; }

    /// <summary>Set once it goes off; it explodes and is removed this frame.</summary>
    public bool IsDetonating { get; set; }

    /// <summary>True when zombies attack it instead of just being blocked by it.</summary>
    public bool CanBeStruck => Shot.Solid && Shot.EnemyHitsToDetonate > 0 && !IsTriggered && !IsDetonating;

    // A barrel darkens with each zombie hit, so the player can see how long it will hold.
    // A lit fuse makes it flash white.
    public VisualDescriptor BodyVisual
    {
        get
        {
            var color = Shot.Shootable ? Color.Lerp(BarrelColor, BodyColor, DamageShown) : BodyColor;
            if (IsFused)
            {
                color = Color.Lerp(color, Color.White, 0.25f + 0.35f * MathF.Sin(_age * FuseFlashSpeed));
            }

            return Shot.Shootable
                ? new VisualDescriptor(ShapeKind.Square, Radius, color)
                : new VisualDescriptor(ShapeKind.Circle, VisualRadius, color);
        }
    }

    private float DamageShown => Shot.EnemyHitsToDetonate > 0
        ? 0.7f * _enemyHitsTaken / Shot.EnemyHitsToDetonate
        : 0f;

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
        _flightTime = shot.ThrowSpeed > 0f ? Vector2.Distance(shot.From, shot.Position) / shot.ThrowSpeed : 0f;
        _flightLeft = _flightTime;
        _fuseLeft = -1f;
        JustLanded = false;
        _age = 0f;
        IsTriggered = false;
        IsDetonating = false;
        _enemyHitsTaken = 0;
    }

    /// <summary>Lights its fuse (once): it goes off when the fuse has burnt down.</summary>
    public void LightFuse(float seconds)
    {
        if (!IsFused && !IsTriggered && !IsDetonating)
        {
            _fuseLeft = seconds;
        }
    }

    /// <summary>A zombie hit; the last one it can take sets it off.</summary>
    public void Strike()
    {
        _enemyHitsTaken++;
        if (_enemyHitsTaken >= Shot.EnemyHitsToDetonate)
        {
            IsTriggered = true;
        }
    }

    public void Tick(float deltaSeconds)
    {
        _age += deltaSeconds;
        JustLanded = false;
        if (IsFused)
        {
            _fuseLeft -= deltaSeconds;
            if (_fuseLeft <= 0f)
            {
                IsTriggered = true;
            }
        }

        if (IsFlying)
        {
            _flightLeft -= deltaSeconds;
            JustLanded = _flightLeft <= 0f;
            return;
        }

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
