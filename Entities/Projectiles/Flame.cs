using System;
using System.Collections.Generic;
using Fourgate.Core.Rendering;
using Fourgate.Weapons;
using Microsoft.Xna.Framework;

namespace Fourgate.Entities.Projectiles;

/// <summary>A flame from a stream weapon: grows as it flies and fades out at its reach. Pooled.</summary>
public sealed class Flame
{
    private static readonly Color HotColor = new(255, 220, 110);
    private static readonly Color CoolColor = new(220, 70, 20);

    private readonly List<IHittable> _alreadyHit = new();
    private float _travelled;
    private float _nextTrailAt;

    public FlameShot Shot { get; private set; }

    public Vector2 Position { get; private set; }

    /// <summary>0 at the nozzle, 1 at the end of its reach.</summary>
    public float Progress => Shot.Range > 0f ? Math.Min(1f, _travelled / Shot.Range) : 1f;

    public float Radius => MathHelper.Lerp(Shot.RadiusStart, Shot.RadiusEnd, Progress);

    /// <summary>True once it has reached its range, a wall, or (without pass-through) an enemy.</summary>
    public bool IsSpent { get; set; }

    /// <summary>Hot and bright at the nozzle, darker red and fainter toward its reach.</summary>
    public VisualDescriptor Visual
    {
        get
        {
            var progress = Progress;
            var color = Color.Lerp(HotColor, CoolColor, progress) * (0.85f * (1f - progress * 0.7f));
            return new VisualDescriptor(ShapeKind.Circle, Radius, color);
        }
    }

    public void Launch(in FlameShot shot)
    {
        Shot = shot;
        Position = shot.Origin;
        _travelled = 0f;
        _alreadyHit.Clear();
        IsSpent = false;

        // The first pool is one spacing out, so it does not land on the player holding the nozzle.
        _nextTrailAt = shot.TrailSpacing > 0f ? shot.TrailSpacing : float.PositiveInfinity;
    }

    public void Advance(float deltaSeconds)
    {
        var step = Math.Min(Shot.Speed * deltaSeconds, Shot.Range - _travelled);
        _travelled += step;
        Position += Shot.Direction * step;
        if (_travelled >= Shot.Range)
        {
            IsSpent = true;
        }
    }

    /// <summary>
    /// The next point along its path where it should leave a fire pool, if it has flown past it;
    /// call repeatedly until it returns false.
    /// </summary>
    public bool TryTakeTrailPoint(out Vector2 point)
    {
        if (Shot.TrailRadius <= 0f || _travelled < _nextTrailAt)
        {
            point = default;
            return false;
        }

        point = Shot.Origin + Shot.Direction * _nextTrailAt;
        _nextTrailAt += Shot.TrailSpacing;
        return true;
    }

    /// <summary>True the first time it touches this target; a flame burns each target once.</summary>
    public bool TryTouch(IHittable target)
    {
        if (_alreadyHit.Contains(target))
        {
            return false;
        }

        _alreadyHit.Add(target);
        return true;
    }
}
