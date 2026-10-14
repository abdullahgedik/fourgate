using Fourgate.Core.Rendering;
using Microsoft.Xna.Framework;

namespace Fourgate.Entities.Effects;

/// <summary>Short-lived flash showing an explosion's reach. Pooled; <see cref="Start"/> resets all state.</summary>
public sealed class ExplosionEffect
{
    private const float DurationSeconds = 0.35f;

    private static readonly Color FlashColor = new(255, 170, 60);

    private float _age;

    public Vector2 Position { get; private set; }

    public float Radius { get; private set; }

    public bool IsFinished => _age >= DurationSeconds;

    /// <summary>A disc that grows quickly to the blast radius and fades out.</summary>
    public VisualDescriptor Visual
    {
        get
        {
            var t = MathHelper.Clamp(_age / DurationSeconds, 0f, 1f);
            var growth = MathHelper.Clamp(t * 4f, 0.35f, 1f);
            return new VisualDescriptor(ShapeKind.Circle, Radius * growth, FlashColor * ((1f - t) * 0.75f));
        }
    }

    public void Start(Vector2 position, float radius)
    {
        Position = position;
        Radius = radius;
        _age = 0f;
    }

    public void Tick(float deltaSeconds)
    {
        _age += deltaSeconds;
    }
}
