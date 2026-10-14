using System;
using Fourgate.Core.Rendering;
using Microsoft.Xna.Framework;

namespace Fourgate.Entities;

/// <summary>
/// Indestructible dummy for trying out weapons: flashes red when shot, and can patrol
/// back and forth along a line. Debug only.
/// </summary>
public sealed class PracticeTarget : IHittable
{
    private const float FlashSeconds = 0.25f;

    private static readonly Color IdleColor = new(150, 155, 170);
    private static readonly Color HitColor = new(255, 50, 50);

    private readonly Vector2 _start;
    private readonly Vector2 _end;
    private readonly float _speed;
    private float _travelled;
    private float _flashLeft;

    /// <summary>Creates a target that stays in one place.</summary>
    public PracticeTarget(Vector2 position, float radius)
        : this(position, position, 0f, radius)
    {
    }

    /// <summary>Creates a target that patrols between two points.</summary>
    public PracticeTarget(Vector2 start, Vector2 end, float speed, float radius)
    {
        _start = start;
        _end = end;
        _speed = speed;
        Radius = radius;
        Position = start;
    }

    public Vector2 Position { get; private set; }

    public float Radius { get; }

    // Dummies never die.
    public float HitPointsLeft => float.PositiveInfinity;

    /// <summary>Number of hits taken so far.</summary>
    public int HitCount { get; private set; }

    public bool IsFlashing => _flashLeft > 0f;

    public VisualDescriptor Visual =>
        new(ShapeKind.Circle, Radius, Color.Lerp(IdleColor, HitColor, _flashLeft / FlashSeconds));

    public void Hit(float damage, int ownerIndex)
    {
        HitCount++;
        _flashLeft = FlashSeconds;
    }

    public void Update(float deltaSeconds)
    {
        _flashLeft = Math.Max(0f, _flashLeft - deltaSeconds);

        var length = Vector2.Distance(_start, _end);
        if (_speed <= 0f || length <= 0f)
        {
            return;
        }

        // Ping-pong along the line: out for one length, back for the next.
        _travelled = (_travelled + _speed * deltaSeconds) % (length * 2f);
        var along = _travelled <= length ? _travelled : length * 2f - _travelled;
        Position = Vector2.Lerp(_start, _end, along / length);
    }
}
