using System;
using Fourgate.Core.Data;
using Fourgate.Core.Rendering;
using Microsoft.Xna.Framework;

namespace Fourgate.Entities.Enemies;

/// <summary>An enemy in the arena. Pooled; <see cref="Spawn"/> resets all state.</summary>
public sealed class Enemy : IHittable
{
    private const float FlashSeconds = 0.12f;

    private static readonly Color BodyColor = new(90, 190, 100);
    private static readonly Color HitColor = new(255, 50, 50);

    private float _flashLeft;
    private bool _wasHit;
    private float _attackCooldownLeft;

    /// <summary>Unique per spawn; gives overlapping enemies a stable order.</summary>
    public int Id { get; private set; }

    public EnemyConfig Config { get; private set; } = null!;

    public Vector2 Position { get; set; }

    public float Hp { get; private set; }

    public float Radius => Config.Radius;

    public bool IsAlive => Hp > 0f;

    /// <summary>True when the enemy's contact attack is off cooldown.</summary>
    public bool CanAttack => _attackCooldownLeft <= 0f;

    /// <summary>Index of the player whose shot landed last.</summary>
    public int LastHitBy { get; private set; }

    public VisualDescriptor Visual =>
        new(ShapeKind.Circle, Radius, Color.Lerp(BodyColor, HitColor, _flashLeft / FlashSeconds));

    public void Spawn(int id, EnemyConfig config, Vector2 position)
    {
        Id = id;
        Config = config;
        Position = position;
        Hp = config.Hp;
        LastHitBy = 0;
        _flashLeft = 0f;
        _wasHit = false;
        _attackCooldownLeft = 0f;
    }

    /// <summary>Starts the wait before the next contact attack.</summary>
    public void StartAttackCooldown()
    {
        _attackCooldownLeft = Config.ContactCooldown;
    }

    public void Hit(float damage, int ownerIndex)
    {
        if (!IsAlive)
        {
            return;
        }

        Hp -= damage;
        LastHitBy = ownerIndex;
        _flashLeft = FlashSeconds;
        _wasHit = true;
    }

    /// <summary>Advances timers.</summary>
    public void Tick(float deltaSeconds)
    {
        _flashLeft = Math.Max(0f, _flashLeft - deltaSeconds);
        _attackCooldownLeft = Math.Max(0f, _attackCooldownLeft - deltaSeconds);
    }

    /// <summary>Returns true once after each frame in which the enemy was hit.</summary>
    public bool ConsumeHit()
    {
        var wasHit = _wasHit;
        _wasHit = false;
        return wasHit;
    }
}
