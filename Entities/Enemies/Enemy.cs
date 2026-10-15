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
    private float _windUpLeft;
    private float _windUpSeconds;
    private float _rangedCooldownLeft;
    private bool _useSecondAttack;
    private int _burstsLeft;
    private float _nextBurstIn;

    /// <summary>Unique per spawn; gives overlapping enemies a stable order.</summary>
    public int Id { get; private set; }

    public EnemyConfig Config { get; private set; } = null!;

    public Vector2 Position { get; set; }

    /// <summary>Direction of the attack being wound up; locked when the wind-up starts.</summary>
    public Vector2 AimDirection { get; private set; }

    /// <summary>True while it stands still, showing its aim line, before a ranged attack.</summary>
    public bool IsWindingUp => _windUpLeft > 0f;

    /// <summary>How far the wind-up has got, 0 to 1.</summary>
    public float WindUpProgress => _windUpSeconds > 0f ? 1f - _windUpLeft / _windUpSeconds : 1f;

    /// <summary>Which way it sidesteps between throws: 1 or -1; switches after every throw.</summary>
    public float StrafeSide { get; private set; } = 1f;

    /// <summary>How high it is off the floor (a leaping boss); 0 on the ground.</summary>
    public float Elevation { get; set; }

    /// <summary>The ranged attack it uses next; the two attacks take turns when it has two.</summary>
    public EnemyAttackConfig? CurrentAttack =>
        _useSecondAttack && Config.SecondAttack is not null ? Config.SecondAttack : Config.Attack;

    /// <summary>True while it keeps throwing the rest of a stream after its wind-up.</summary>
    public bool IsFiringBursts => _burstsLeft > 0;

    /// <summary>True when its ranged attack is off cooldown.</summary>
    public bool CanStartRangedAttack => _rangedCooldownLeft <= 0f && !IsWindingUp;

    /// <summary>Angle (radians) of the way it last walked; turns a triangle body.</summary>
    public float Facing { get; set; }

    public float Hp { get; private set; }

    /// <summary>True while nothing can hurt it (a boss changing phase).</summary>
    public bool IsInvulnerable { get; set; }

    /// <summary>HP it started with (after any scaling), for health bars.</summary>
    public float MaxHp { get; private set; }

    public float Radius => Config.Radius;

    public bool IsAlive => Hp > 0f;

    public float HitPointsLeft => Math.Max(0f, Hp);

    /// <summary>True when the enemy's contact attack is off cooldown.</summary>
    public bool CanAttack => _attackCooldownLeft <= 0f;

    /// <summary>Index of the player whose shot landed last.</summary>
    public int LastHitBy { get; private set; }

    /// <summary>Damage of the last bullet, beam or blast that hit it (burning does not count).</summary>
    public float LastHitDamage { get; private set; }

    // Model animation: how long a lunge lasts, how quickly the walk speed follows, and a jump too
    // big to be a step (a teleport) that does not count as walking.
    private const float LungeSeconds = 0.3f;
    private const float SpeedSmoothing = 10f;
    private const float TeleportDistance = 120f;
    private Vector2 _lastPosition;
    private Vector2 _previousPosition;
    private float _lungeAge;

    // Damage taken from each player since it was last read (bosses use it for their aggro).
    private const int TrackedOwners = 4;
    private readonly float[] _damageByOwner = new float[TrackedOwners];

    /// <summary>Damage the given player has dealt since the last call; reading it starts the count again.</summary>
    public float TakeDamageDealtBy(int ownerIndex)
    {
        if (ownerIndex < 0 || ownerIndex >= TrackedOwners)
        {
            return 0f;
        }

        var dealt = _damageByOwner[ownerIndex];
        _damageByOwner[ownerIndex] = 0f;
        return dealt;
    }

    private void CountDamage(float damage, int ownerIndex)
    {
        if (ownerIndex >= 0 && ownerIndex < TrackedOwners)
        {
            _damageByOwner[ownerIndex] += Math.Min(damage, 1_000_000f);
        }
    }

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

    /// <summary>Distance walked so far; drives the model's step cycle.</summary>
    public float WalkDistance { get; private set; }

    /// <summary>How fast it is moving now, smoothed.</summary>
    public float Speed { get; private set; }

    /// <summary>0–1–0: the lunge of a contact attack.</summary>
    public float LungeAmount => _lungeAge < LungeSeconds ? MathF.Sin(MathF.PI * _lungeAge / LungeSeconds) : 0f;

    /// <summary>0–1: recoiling from a hit.</summary>
    public float FlinchAmount => _flashLeft / FlashSeconds;

    /// <summary>Its own colour, burning included but without the hit flash (a model's tinted parts).</summary>
    public Color BodyColor => IsBurning || _scorchLeft > 0f ? Color.Lerp(_bodyColor, BurnColor, 0.6f) : _bodyColor;

    /// <summary>
    /// How strongly its whole model is washed with a colour, and which: the hit flash first, then
    /// a softer glow while it burns.
    /// </summary>
    public float ModelGlow(out Color color)
    {
        if (_flashLeft > 0f)
        {
            color = HitColor;
            return ModelHitGlow * _flashLeft / FlashSeconds;
        }

        color = BurnColor;
        return IsBurning || _scorchLeft > 0f ? ModelBurnGlow : 0f;
    }

    // How far a hit and burning wash a model's colours.
    private const float ModelHitGlow = 0.75f;
    private const float ModelBurnGlow = 0.35f;

    /// <summary>0–1: how far its ranged wind-up has come (drawing back to throw).</summary>
    public float WindupAmount => IsWindingUp ? WindUpProgress : 0f;

    /// <summary>Which way it last moved (radians); it drives the legs when it sidesteps.</summary>
    public float MoveHeading { get; private set; }

    /// <summary>Set by the boss system: 0–1 while a boss draws back before an attack.</summary>
    public float BossWindup { get; set; }

    /// <summary>Set by the boss system: 0–1 while a boss carries an attack out.</summary>
    public float BossAction { get; set; }

    /// <summary>Plays the throw (or blow) motion: the same lunge as a contact attack.</summary>
    public void PlayAttackMotion()
    {
        _lungeAge = 0f;
    }

    /// <summary>Notes how far it moved this frame, for its walk.</summary>
    public void TrackWalk(float deltaSeconds)
    {
        var moved = Vector2.Distance(Position, _lastPosition);
        _previousPosition = _lastPosition;
        _lastPosition = Position;
        if (deltaSeconds <= 0f || moved > TeleportDistance)
        {
            return;
        }

        WalkDistance += moved;
        Speed = MathHelper.Lerp(Speed, moved / deltaSeconds, Math.Min(1f, deltaSeconds * SpeedSmoothing));
        if (moved > 0.05f)
        {
            var delta = Position - _previousPosition;
            MoveHeading = MathF.Atan2(delta.Y, delta.X);
        }
    }

    /// <summary>Way it ambles while there is nobody to chase (zero: standing still).</summary>
    public Vector2 WanderHeading { get; set; }

    /// <summary>Seconds left on the current amble.</summary>
    public float WanderLeft { get; set; }

    /// <summary>Ambles taken so far (seeds the next one).</summary>
    public int WanderLegs { get; set; }

    public void Spawn(int id, EnemyConfig config, Vector2 position)
    {
        Id = id;
        WanderHeading = Vector2.Zero;
        WanderLeft = 0f;
        WanderLegs = 0;
        WalkDistance = 0f;
        Speed = 0f;
        _lastPosition = position;
        _previousPosition = position;
        MoveHeading = 0f;
        BossWindup = 0f;
        BossAction = 0f;
        _lungeAge = LungeSeconds;
        Config = config;
        Position = position;
        Facing = 0f;
        IsInvulnerable = false;
        AimDirection = Vector2.Zero;
        _windUpLeft = 0f;
        _windUpSeconds = 0f;
        _rangedCooldownLeft = 0f;
        _useSecondAttack = false;
        _burstsLeft = 0;
        _nextBurstIn = 0f;
        Elevation = 0f;
        StrafeSide = id % 2 == 0 ? 1f : -1f;
        _bodyColor = new Color(config.Color[0], config.Color[1], config.Color[2]);
        Hp = config.Hp;
        MaxHp = config.Hp;
        LastHitBy = 0;
        LastHitDamage = 0f;
        Array.Clear(_damageByOwner);
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

    /// <summary>Multiplies current and starting HP (co-op scaling of a boss when it appears).</summary>
    public void ScaleHp(float multiplier)
    {
        Hp *= multiplier;
        MaxHp *= multiplier;
    }

    /// <summary>Starts the telegraphed wind-up of a ranged attack along a locked direction.</summary>
    public void StartWindUp(Vector2 direction, float seconds)
    {
        AimDirection = direction;
        Facing = MathF.Atan2(direction.Y, direction.X);
        _windUpSeconds = seconds;
        _windUpLeft = seconds;
    }

    /// <summary>Advances the wind-up; true on the frame it ends and the attack goes off.</summary>
    public bool TickWindUp(float deltaSeconds, float cooldownSeconds)
    {
        _windUpLeft -= deltaSeconds;
        if (_windUpLeft > 0f)
        {
            return false;
        }

        _windUpLeft = 0f;
        _rangedCooldownLeft = cooldownSeconds;
        StrafeSide = -StrafeSide;
        return true;
    }

    /// <summary>Starts the rest of a stream: this many more throws, one per interval.</summary>
    public void StartBursts(int count, float interval)
    {
        _burstsLeft = count;
        _nextBurstIn = interval;
    }

    /// <summary>Advances a stream; true on the frames a throw goes out.</summary>
    public bool TickBurst(float deltaSeconds, float interval)
    {
        if (_burstsLeft <= 0)
        {
            return false;
        }

        _nextBurstIn -= deltaSeconds;
        if (_nextBurstIn > 0f)
        {
            return false;
        }

        _burstsLeft--;
        _nextBurstIn += interval;
        return true;
    }

    /// <summary>The next ranged attack will be the other one (when there are two).</summary>
    public void SwitchAttack()
    {
        _useSecondAttack = !_useSecondAttack;
    }

    /// <summary>Starts the wait before the next contact attack.</summary>
    public void StartAttackCooldown()
    {
        _attackCooldownLeft = Config.ContactCooldown;
        _lungeAge = 0f;
    }

    /// <summary>
    /// A bullet or beam hit: one landing on the armored front arc only does part of its damage
    /// (none at all with a multiplier of 0, and then the body does not flash).
    /// </summary>
    public void HitAt(float damage, int ownerIndex, Vector2 impact)
    {
        if (IsInFrontArc(impact - Position))
        {
            damage *= Config.FrontArmorDamageMult;
            if (damage <= 0f)
            {
                return;
            }
        }

        Hit(damage, ownerIndex);
    }

    /// <summary>A bullet or beam landing on the armored front stops there, piercing or not.</summary>
    public bool BlocksShotAt(Vector2 impact)
    {
        return IsAlive && IsInFrontArc(impact - Position);
    }

    /// <summary>True when the armor is a band standing out in front of the body.</summary>
    public bool HasShieldBand => Config.FrontArmorArcDegrees > 0f && Config.FrontArmorThickness > 0f;

    /// <summary>Distance from the center to the inner edge of the shield band.</summary>
    public float ShieldInnerRadius => Radius + Config.FrontArmorGap;

    /// <summary>Distance from the center to the outer edge of the shield band.</summary>
    public float ShieldOuterRadius => ShieldInnerRadius + Config.FrontArmorThickness;

    /// <summary>How far from its center shots can meet it: its body or its shield band.</summary>
    public float HitReach => HasShieldBand ? Math.Max(Radius, ShieldOuterRadius) : Radius;

    /// <summary>A shot crossing the outer edge of the shield band inside the armored arc stops there.</summary>
    public bool TryHitShield(
        Vector2 origin, Vector2 direction, float reach, float shotRadius, out float distance, out Vector2 point)
    {
        distance = 0f;
        point = Vector2.Zero;
        if (!IsAlive || !HasShieldBand)
        {
            return false;
        }

        var outer = ShieldOuterRadius + shotRadius;
        var toCenter = Position - origin;
        var along = Vector2.Dot(toCenter, direction);
        var offsetSquared = toCenter.LengthSquared() - along * along;
        if (offsetSquared > outer * outer)
        {
            return false;
        }

        // A shot starting inside the band's circle (fired from up close) is left to the body test.
        var enter = along - MathF.Sqrt(outer * outer - offsetSquared);
        if (enter < 0f || enter > reach)
        {
            return false;
        }

        point = origin + direction * enter;
        if (!IsInFrontArc(point - Position))
        {
            return false;
        }

        distance = enter;
        return true;
    }

    /// <summary>True when a point (given from the enemy's center) lies in its armored front arc.</summary>
    public bool IsInFrontArc(Vector2 offset)
    {
        if (Config.FrontArmorArcDegrees <= 0f || offset == Vector2.Zero)
        {
            return false;
        }

        var facing = new Vector2(MathF.Cos(Facing), MathF.Sin(Facing));
        var halfArc = MathHelper.ToRadians(Config.FrontArmorArcDegrees / 2f);
        return Vector2.Dot(Vector2.Normalize(offset), facing) >= MathF.Cos(halfArc);
    }

    public void Hit(float damage, int ownerIndex)
    {
        if (!IsAlive || IsInvulnerable)
        {
            return;
        }

        Hp -= damage;
        LastHitBy = ownerIndex;
        LastHitDamage = damage;
        CountDamage(damage, ownerIndex);
        _flashLeft = FlashSeconds;
        _wasHit = true;
    }

    public void Burn(float damage, int ownerIndex)
    {
        if (!IsAlive || damage <= 0f || IsInvulnerable)
        {
            return;
        }

        Hp -= damage;
        LastHitBy = ownerIndex;
        CountDamage(damage, ownerIndex);
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
        _lungeAge += deltaSeconds;
        _attackCooldownLeft = Math.Max(0f, _attackCooldownLeft - deltaSeconds);
        _rangedCooldownLeft = Math.Max(0f, _rangedCooldownLeft - deltaSeconds);
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
