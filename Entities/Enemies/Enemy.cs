using System;
using Fourgate.Core.Data;
using Fourgate.Core.Rendering;
using Microsoft.Xna.Framework;

namespace Fourgate.Entities.Enemies;

/// <summary>An enemy in the arena. Pooled; <see cref="Spawn"/> resets all state.</summary>
public sealed class Enemy : IHittable
{
    private const float FlashSeconds = 0.12f;

    private static readonly Color HitColor = new(255, 50, 50);
    private static readonly Color BurnColor = new(255, 140, 40);

    // How long the orange tint lingers after a tick of fire damage.
    private const float ScorchSeconds = 0.15f;

    private float _flashLeft;
    private bool _wasHit;
    private float _attackCooldownLeft;
    private Vector2 _knockLeft;
    private float _burnLeft;
    private float _burnSeconds;
    private float _burnDamagePerSecond;
    private int _burnOwner;
    private bool _burnCanSpread;
    private float _scorchLeft;
    private Color _bodyColor;

    /// <summary>Unique per spawn; gives overlapping enemies a stable order.</summary>
    public int Id { get; private set; }

    public EnemyConfig Config { get; private set; } = null!;

    public Vector2 Position { get; set; }

    /// <summary>Angle (radians) of the way it last walked; turns a triangle body.</summary>
    public float Facing { get; set; }

    public float Hp { get; private set; }

    public float Radius => Config.Radius;

    public bool IsAlive => Hp > 0f;

    public float HitPointsLeft => Math.Max(0f, Hp);

    /// <summary>True when the enemy's contact attack is off cooldown.</summary>
    public bool CanAttack => _attackCooldownLeft <= 0f;

    /// <summary>Index of the player whose shot landed last.</summary>
    public int LastHitBy { get; private set; }

    /// <summary>True while it keeps burning on its own.</summary>
    public bool IsBurning => _burnLeft > 0f;

    /// <summary>True while it is burning and can still set the enemies touching it alight.</summary>
    public bool CanSpreadFire => IsBurning && _burnCanSpread;

    public VisualDescriptor Visual
    {
        get
        {
            var body = IsBurning || _scorchLeft > 0f ? Color.Lerp(_bodyColor, BurnColor, 0.6f) : _bodyColor;
            return new VisualDescriptor(Config.Shape, Radius, Color.Lerp(body, HitColor, _flashLeft / FlashSeconds));
        }
    }

    public void Spawn(int id, EnemyConfig config, Vector2 position)
    {
        Id = id;
        Config = config;
        Position = position;
        Facing = 0f;
        _bodyColor = new Color(config.Color[0], config.Color[1], config.Color[2]);
        Hp = config.Hp;
        LastHitBy = 0;
        _flashLeft = 0f;
        _wasHit = false;
        _attackCooldownLeft = 0f;
        _knockLeft = Vector2.Zero;
        _burnLeft = 0f;
        _burnCanSpread = false;
        _scorchLeft = 0f;
    }

    public void Knock(Vector2 push)
    {
        if (IsAlive)
        {
            _knockLeft += push * (1f - Config.KnockbackResist);
        }
    }

    /// <summary>Takes a share (0–1) of the push still owed; the caller moves the enemy by it.</summary>
    public Vector2 TakeKnockStep(float share)
    {
        var step = _knockLeft * share;
        _knockLeft -= step;
        return step;
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

    public void Burn(float damage, int ownerIndex)
    {
        if (!IsAlive || damage <= 0f)
        {
            return;
        }

        Hp -= damage;
        LastHitBy = ownerIndex;
        _scorchLeft = ScorchSeconds;
    }

    public void Ignite(float seconds, float damagePerSecond, int ownerIndex)
    {
        Ignite(seconds, damagePerSecond, ownerIndex, canSpread: true);
    }

    /// <summary>
    /// Sets it on fire. An enemy set alight by another burning enemy cannot pass the fire on,
    /// so a dense crowd does not keep itself burning forever.
    /// </summary>
    public void Ignite(float seconds, float damagePerSecond, int ownerIndex, bool canSpread)
    {
        if (!IsAlive || seconds <= 0f)
        {
            return;
        }

        // A fire that can spread is never downgraded by one that cannot.
        var wasBurning = IsBurning;
        _burnCanSpread = canSpread || (wasBurning && _burnCanSpread);
        _burnSeconds = seconds;
        _burnLeft = Math.Max(_burnLeft, seconds);
        _burnDamagePerSecond = wasBurning ? Math.Max(_burnDamagePerSecond, damagePerSecond) : damagePerSecond;
        _burnOwner = ownerIndex;
    }

    /// <summary>Damage per second of the fire it is burning with.</summary>
    public float BurnDamagePerSecond => _burnDamagePerSecond;

    /// <summary>How long a fire it passes on keeps burning.</summary>
    public float BurnSeconds => _burnSeconds;

    /// <summary>Index of the player whose fire it is burning with.</summary>
    public int BurnOwner => _burnOwner;

    /// <summary>Advances timers, and burns while on fire.</summary>
    public void Tick(float deltaSeconds)
    {
        _flashLeft = Math.Max(0f, _flashLeft - deltaSeconds);
        _attackCooldownLeft = Math.Max(0f, _attackCooldownLeft - deltaSeconds);
        _scorchLeft = Math.Max(0f, _scorchLeft - deltaSeconds);

        if (IsBurning)
        {
            var seconds = Math.Min(deltaSeconds, _burnLeft);
            _burnLeft -= deltaSeconds;
            Burn(_burnDamagePerSecond * seconds, _burnOwner);
            if (!IsBurning)
            {
                _burnLeft = 0f;
                _burnCanSpread = false;
                _burnDamagePerSecond = 0f;
            }
        }
    }

    /// <summary>Returns true once after each frame in which the enemy was hit.</summary>
    public bool ConsumeHit()
    {
        var wasHit = _wasHit;
        _wasHit = false;
        return wasHit;
    }
}
