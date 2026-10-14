using Microsoft.Xna.Framework;

namespace Fourgate.Entities.Projectiles;

/// <summary>An enemy fireball flying in a straight line. Pooled; <see cref="Launch"/> resets all state.</summary>
public sealed class Fireball
{
    private Vector2 _velocity;

    public Vector2 Position { get; private set; }

    public float Radius { get; private set; }

    public float Damage { get; private set; }

    /// <summary>World units it can still fly.</summary>
    public float DistanceLeft { get; private set; }

    public void Launch(Vector2 origin, Vector2 velocity, float radius, float damage, float range)
    {
        Position = origin;
        _velocity = velocity;
        Radius = radius;
        Damage = damage;
        DistanceLeft = range;
    }

    public void Move(float deltaSeconds)
    {
        var step = _velocity * deltaSeconds;
        Position += step;
        DistanceLeft -= step.Length();
    }
}
