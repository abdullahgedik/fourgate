using System;
using Fourgate.Core.Rendering;
using Fourgate.Weapons;
using Microsoft.Xna.Framework;

namespace Fourgate.Entities.Projectiles;

/// <summary>A grenade in flight. Pooled; <see cref="Launch"/> resets all state.</summary>
public sealed class ThrownExplosive
{
    // How much bigger the object looks at the top of its arc, to suggest height in a top-down view.
    private const float ArcScale = 0.7f;

    private static readonly Color BodyColor = new(70, 110, 60);

    private float _travelled;
    private float _distance;

    public ExplosiveShot Shot { get; private set; }

    public Vector2 Position { get; private set; }

    /// <summary>0 when thrown, 1 on landing.</summary>
    public float Progress => _distance > 0f ? Math.Min(1f, _travelled / _distance) : 1f;

    public bool HasLanded => _travelled >= _distance;

    /// <summary>The object as seen in the air: larger near the top of the arc.</summary>
    public VisualDescriptor Visual
    {
        get
        {
            var height = MathF.Sin(MathF.PI * Progress);
            return new VisualDescriptor(ShapeKind.Circle, Shot.VisualRadius * (1f + ArcScale * height), BodyColor);
        }
    }

    /// <summary>0 on the ground, 1 at the top of the throw.</summary>
    public float ArcHeight => MathF.Sin(MathF.PI * Progress);

    public void Launch(in ExplosiveShot shot)
    {
        Shot = shot;
        Position = shot.Origin;
        _travelled = 0f;
        _distance = Vector2.Distance(shot.Origin, shot.Target);
    }

    public void Advance(float deltaSeconds)
    {
        _travelled = Math.Min(_distance, _travelled + Shot.Speed * deltaSeconds);
        Position = Vector2.Lerp(Shot.Origin, Shot.Target, Progress);
    }
}
