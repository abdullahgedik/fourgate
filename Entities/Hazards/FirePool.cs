using System;
using Fourgate.Core.Rendering;
using Microsoft.Xna.Framework;

namespace Fourgate.Entities.Hazards;

/// <summary>A patch of burning ground. Pooled; <see cref="Start"/> resets all state.</summary>
public sealed class FirePool
{
    // The pool fades out over its last moments instead of vanishing.
    private const float FadeSeconds = 0.5f;
    private const float FlickerSpeed = 9f;

    private static readonly Color OuterColor = new(230, 90, 30);
    private static readonly Color InnerColor = new(255, 190, 70);

    private float _age;

    public Vector2 Position { get; private set; }

    public float Radius { get; private set; }

    /// <summary>Damage per second to everything inside.</summary>
    public float DamagePerSecond { get; private set; }

    public int OwnerIndex { get; private set; }

    /// <summary>How long enemies keep burning after being caught in the pool; 0 when they do not.</summary>
    public float IgniteSeconds { get; private set; }

    public float SecondsLeft { get; private set; }

    public bool IsFinished => SecondsLeft <= 0f;

    /// <summary>The wide, dim outer glow of the pool.</summary>
    public VisualDescriptor OuterVisual => new(ShapeKind.Circle, Radius, OuterColor * (0.35f * Fade));

    /// <summary>The flickering bright core.</summary>
    public VisualDescriptor InnerVisual
    {
        get
        {
            var flicker = 0.55f + 0.08f * MathF.Sin(_age * FlickerSpeed);
            return new VisualDescriptor(ShapeKind.Circle, Radius * flicker, InnerColor * (0.4f * Fade));
        }
    }

    private float Fade => Math.Clamp(SecondsLeft / FadeSeconds, 0f, 1f);

    public void Start(
        Vector2 position, float radius, float seconds, float damagePerSecond, int ownerIndex, float igniteSeconds)
    {
        Position = position;
        Radius = radius;
        SecondsLeft = seconds;
        DamagePerSecond = damagePerSecond;
        OwnerIndex = ownerIndex;
        IgniteSeconds = igniteSeconds;
        _age = 0f;
    }

    public void Tick(float deltaSeconds)
    {
        _age += deltaSeconds;
        SecondsLeft -= deltaSeconds;
    }
}
