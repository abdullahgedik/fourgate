using System;
using Fourgate.Core.Audio;
using Fourgate.Core.Data;
using Fourgate.Core.Input;
using Fourgate.Core.Rng;
using Microsoft.Xna.Framework;

namespace Fourgate.Weapons;

/// <summary>One player's copy of a weapon: level, fire timing, magazine, reserve and reload state.</summary>
public sealed class Weapon
{
    // Absorbs float error so a cooldown that should end exactly on a frame does.
    private const float TimeEpsilon = 0.0001f;

    // How long a quick click (pressed and already released) is remembered while the weapon
    // is not ready yet. Input feel, not balance.
    private const float ClickBufferSeconds = 0.2f;

    // Overdrive settles back this many times faster than it builds up.
    private const float OverdriveSettleSpeed = 3f;

    // Half the gap between the two muzzles of a dual-wielded weapon.
    private const float DualWieldOffset = 7f;

    private readonly GameConfig _rules;
    // One sound id per weapon, built once.
    private readonly string _fireSfx;
    private readonly string _throwSfx;
    private readonly IAudioManager _audio;
    private readonly GameRandom? _random;
    private float _cooldown;
    private float _reloadLeft;
    private bool _fireQueued;
    private float _clickBufferLeft;
    private int _shotsFired;
    private float _overdrive;
    private float _trailCooldown;

    /// <param name="config">The weapon's data.</param>
    /// <param name="rules">Global reload rules.</param>
    /// <param name="audio">Receives the weapon's sound events.</param>
    /// <param name="random">Source of shot spread; without one the weapon fires dead straight.</param>
    public Weapon(WeaponConfig config, GameConfig rules, IAudioManager audio, GameRandom? random = null)
    {
        Config = config;
        _rules = rules;
        _audio = audio;
        _fireSfx = SfxIds.WeaponFireFor(config.Id);
        _throwSfx = SfxIds.WeaponThrowFor(config.Id);
        _random = random;

        Level = 1;
        Stats = WeaponStats.Compute(config, Level);
        Magazine = config.Ammo.UsesMagazine ? Stats.MagSize : 0;
        Reserve = config.Ammo.InfiniteReserve ? 0 : config.Ammo.ReserveMax;
    }

    public WeaponConfig Config { get; }

    /// <summary>Weapon level, 1 to <see cref="WeaponConfig.MaxLevel"/>.</summary>
    public int Level { get; private set; }

    /// <summary>The weapon's effective numbers at its current level.</summary>
    public WeaponStats Stats { get; private set; }

    /// <summary>Rounds in the magazine. Unused when reloads are disabled.</summary>
    public int Magazine { get; private set; }

    /// <summary>Rounds in reserve. Meaningless when <see cref="HasInfiniteReserve"/> is true.</summary>
    public int Reserve { get; private set; }

    public bool HasInfiniteReserve => Config.Ammo.InfiniteReserve;

    /// <summary>Scales the maximum reserve (ammo capacity upgrade of the owner).</summary>
    public float ReserveCapacityMultiplier { get; set; } = 1f;

    /// <summary>Largest reserve this weapon can hold right now.</summary>
    public int ReserveMax =>
        (int)MathF.Round((Config.Ammo.ReserveMax + Stats.ReserveMaxBonus) * ReserveCapacityMultiplier);

    /// <summary>False for count-based weapons, which use their reserve directly and never reload.</summary>
    public bool UsesMagazine => Config.Ammo.UsesMagazine;

    public bool IsReloading => _reloadLeft > 0f;

    /// <summary>True when the reserve is limited and not full.</summary>
    public bool NeedsAmmo => !HasInfiniteReserve && Reserve < ReserveMax;

    /// <summary>How full the reserve is, from 0 to 1; always 1 for an unlimited reserve.</summary>
    public float ReserveFraction =>
        HasInfiniteReserve || ReserveMax <= 0 ? 1f : Reserve / (float)ReserveMax;

    /// <summary>0 at the start of a reload, 1 when it finishes; 0 when not reloading.</summary>
    public float ReloadProgress
    {
        get
        {
            var total = ReloadSeconds;
            return IsReloading && total > 0f ? 1f - _reloadLeft / total : 0f;
        }
    }

    /// <summary>How far the overdrive has built up: 0 at rest, 1 at full speed.</summary>
    public float Overdrive => _overdrive;

    /// <summary>Shots per second right now, including any overdrive.</summary>
    public float CurrentFireRate => Stats.FireRate * (1f + (Stats.OverdriveFireRateMult - 1f) * _overdrive);

    private float ReloadSeconds => Stats.ReloadTime * _rules.GlobalReloadTimeMultiplier;

    private bool ReserveHasAmmo => HasInfiniteReserve || Reserve > 0;

    /// <summary>Sets the weapon's level and recomputes its stats. Rounds already loaded are kept.</summary>
    public void SetLevel(int level)
    {
        Level = Math.Clamp(level, 1, WeaponConfig.MaxLevel);
        Stats = WeaponStats.Compute(Config, Level);
    }

    /// <summary>Advances the weapon as a gun, driven by the owner's fire and reload buttons.</summary>
    public void Update(
        float deltaSeconds,
        IPlayerInput input,
        Vector2 muzzle,
        Vector2 direction,
        int ownerIndex,
        IShotHandler shots)
    {
        Update(deltaSeconds, WeaponTrigger.ForGun(input), muzzle, direction, ownerIndex, shots);
    }

    /// <summary>Advances timers and fires or reloads according to the trigger.</summary>
    /// <param name="deltaSeconds">Frame time.</param>
    /// <param name="trigger">The buttons driving this weapon.</param>
    /// <param name="muzzle">World position shots start from.</param>
    /// <param name="direction">Unit-length aim direction.</param>
    /// <param name="ownerIndex">Index of the owning player.</param>
    /// <param name="shots">Resolves the fired shots.</param>
    public void Update(
        float deltaSeconds,
        in WeaponTrigger trigger,
        Vector2 muzzle,
        Vector2 direction,
        int ownerIndex,
        IShotHandler shots)
    {
        // Goes negative only on the frame the cooldown runs out; that overshoot is carried
        // into the next shot so the fire rate stays exact. An idle weapon rests at zero.
        _cooldown = _cooldown <= 0f ? 0f : _cooldown - deltaSeconds;
        _clickBufferLeft = Math.Max(0f, _clickBufferLeft - deltaSeconds);
        UpdateOverdrive(deltaSeconds, trigger.Held);
        _trailCooldown = Math.Max(0f, _trailCooldown - deltaSeconds);

        if (IsReloading)
        {
            _reloadLeft -= deltaSeconds;
            if (_reloadLeft <= TimeEpsilon)
            {
                FinishReload();
            }
        }

        if (!_rules.ReloadsEnabled)
        {
            // Reloads may be switched off mid-reload; nothing should stay pending.
            _reloadLeft = 0f;
        }
        else if (trigger.ReloadPressed)
        {
            TryStartReload();
        }

        if (!WantsToFire(trigger) || IsReloading || _cooldown > TimeEpsilon)
        {
            return;
        }

        _fireQueued = false;
        _clickBufferLeft = 0f;
        if (!TakeRound())
        {
            // A dry trigger pull also waits out the fire interval, so holding fire on an
            // empty weapon does not click every frame.
            _cooldown = 1f / CurrentFireRate;
            return;
        }

        _cooldown += 1f / CurrentFireRate;
        if (!Fire(muzzle, direction, trigger.AimDistance, ownerIndex, shots))
        {
            // Nothing came out (no room to place it): the round and the wait are given back.
            Reserve += HasInfiniteReserve ? 0 : 1;
            _cooldown -= 1f / CurrentFireRate;
            return;
        }

        _audio.PlaySfx(_fireSfx);

        if (_rules.ReloadsEnabled && Magazine == 0)
        {
            TryStartReload();
        }
    }

    /// <summary>Adds rounds to the reserve up to its maximum. Returns how many fit.</summary>
    public int AddReserve(int rounds)
    {
        if (HasInfiniteReserve || rounds <= 0)
        {
            return 0;
        }

        var added = Math.Max(0, Math.Min(rounds, ReserveMax - Reserve));
        Reserve += added;
        return added;
    }

    /// <summary>Drops an unfinished reload; its progress is lost (weapon switch).</summary>
    public void CancelReload()
    {
        _overdrive = 0f;
        _reloadLeft = 0f;
        _fireQueued = false;
        _clickBufferLeft = 0f;
    }

    /// <summary>Called when the owner switches to this weapon; an empty magazine starts reloading at once.</summary>
    public void OnSelected()
    {
        if (_rules.ReloadsEnabled && Magazine == 0)
        {
            TryStartReload();
        }
    }

    // Returns false when nothing came out (a placed weapon with no room where it was aimed).
    private bool Fire(Vector2 muzzle, Vector2 direction, float aimDistance, int ownerIndex, IShotHandler shots)
    {
        if (Config.Type == WeaponType.Thrown)
        {
            Throw(muzzle, direction, aimDistance, ownerIndex, shots);
            return true;
        }

        if (Config.Type == WeaponType.Stream)
        {
            EmitFlame(muzzle, direction, ownerIndex, shots);
            return true;
        }

        if (Config.Type == WeaponType.Placed)
        {
            // A throwable one flies to the aim point (no further than its reach); others go down in front.
            var thrown = Config.Thrown;
            var at = thrown is null
                ? muzzle + direction * Stats.PlaceDistance
                : muzzle + direction * Math.Min(aimDistance, Stats.Range);
            return shots.PlaceMine(new MineShot(
                at,
                Stats.Damage,
                Stats.BlastRadius,
                Stats.TriggerRadius,
                Stats.ArmSeconds,
                Stats.MaxPlaced,
                Stats.ChainDetonation,
                ownerIndex,
                Config.Id,
                Stats.BlastCount,
                Stats.BlastInterval,
                Stats.BlastSpread,
                Stats.Shootable,
                Stats.LeaveFireRadius,
                Stats.LeaveFireSeconds,
                Stats.Damage * Stats.LeaveFireDamageMult,
                Stats.LeaveFireHurtsPlayers,
                Stats.Solid,
                Stats.GridSize,
                Stats.EnemyHitsToDetonate,
                muzzle,
                thrown?.Speed ?? 0f,
                Config.Placed!.ChainDelaySeconds));
        }

        var damage = NextShotDamage();
        if (!Stats.DualWield)
        {
            FireVolley(muzzle, direction, damage, ownerIndex, shots);
            return true;
        }

        // Two muzzles side by side, one volley from each.
        var side = new Vector2(-direction.Y, direction.X) * DualWieldOffset;
        FireVolley(muzzle + side, direction, damage, ownerIndex, shots);
        FireVolley(muzzle - side, direction, damage, ownerIndex, shots);
        return true;
    }

    // Builds up while the trigger is held (and the weapon is not reloading), and settles
    // back faster once it is let go.
    private void UpdateOverdrive(float deltaSeconds, bool triggerHeld)
    {
        if (Stats.OverdriveFireRateMult <= 1f)
        {
            _overdrive = 0f;
            return;
        }

        var rate = Stats.OverdriveSeconds > 0f ? deltaSeconds / Stats.OverdriveSeconds : 1f;
        _overdrive = triggerHeld && !IsReloading
            ? Math.Min(1f, _overdrive + rate)
            : Math.Max(0f, _overdrive - rate * OverdriveSettleSpeed);
    }

    // Counts shots so that every Nth one can be empowered. The count carries over reloads
    // and weapon switches, so the rhythm is not reset by them.
    private float NextShotDamage()
    {
        _shotsFired++;
        var empowered = Stats.EmpoweredEvery > 0 && _shotsFired % Stats.EmpoweredEvery == 0;
        return empowered ? Stats.Damage * Stats.EmpoweredDamageMult : Stats.Damage;
    }

    // One flame per shot; the weapon's damage per second is shared out over the flames fired
    // each second. With a fire trail, one flame every interval leaves a pool where it dies out.
    private void EmitFlame(Vector2 muzzle, Vector2 direction, int ownerIndex, IShotHandler shots)
    {
        var stream = Config.Stream!;
        var leavesTrail = Stats.TrailInterval > 0f && _trailCooldown <= 0f;
        if (leavesTrail)
        {
            _trailCooldown = Stats.TrailInterval;
        }

        shots.EmitFlame(new FlameShot(
            muzzle,
            Scatter(direction, 0, 1),
            stream.Speed,
            Stats.Range,
            stream.RadiusStart,
            stream.RadiusEnd,
            Stats.Damage / CurrentFireRate,
            ownerIndex,
            Stats.PassThrough,
            leavesTrail ? Stats.TrailRadius : 0f,
            Stats.TrailSeconds,
            Stats.Damage * Stats.TrailDamageMult,
            Stats.TrailSpacing));
    }

    // Lands on the aim point, or at the weapon's reach when aiming farther (or with a stick).
    private void Throw(Vector2 muzzle, Vector2 direction, float aimDistance, int ownerIndex, IShotHandler shots)
    {
        var thrown = Config.Thrown!;
        var distance = Math.Min(aimDistance, Stats.Range);
        shots.ThrowExplosive(new ExplosiveShot(
            muzzle,
            muzzle + direction * distance,
            thrown.Speed,
            thrown.Radius,
            Stats.Damage,
            Stats.BlastRadius,
            ownerIndex,
            Stats.ClusterCount,
            Stats.ClusterDistance,
            Stats.ClusterDamageMult,
            Stats.ClusterRadiusMult,
            Stats.FireRadius,
            Stats.FireSeconds,
            Stats.Damage,
            Stats.IgniteSeconds,
            Config.Fire?.SpreadSeconds ?? 0f));
        _audio.PlaySfx(_throwSfx);
    }

    private void FireVolley(Vector2 muzzle, Vector2 direction, float damage, int ownerIndex, IShotHandler shots)
    {
        if (Config.Type == WeaponType.Beam)
        {
            var beam = Config.Beam!;
            shots.FireBeam(new BeamShot(
                muzzle, direction, damage, Stats.Range, beam.Width, beam.Knockback, ownerIndex,
                Stats.EndBlastRadius, damage * Stats.EndBlastDamageMult, beam.HitEffect));
            return;
        }

        for (var pellet = 0; pellet < Stats.Pellets; pellet++)
        {
            FireBullet(muzzle, Scatter(direction, pellet, Stats.Pellets), damage, ownerIndex, shots);
        }
    }

    private void FireBullet(Vector2 muzzle, Vector2 direction, float damage, int ownerIndex, IShotHandler shots)
    {
        shots.FireProjectile(
            muzzle,
            direction,
            Config.Projectile!,
            damage,
            ownerIndex,
            Stats.Range,
            Stats.Pierce,
            Stats.Ricochets,
            Stats.RicochetRadius,
            Stats.RicochetDamageMult,
            Stats.Overkill,
            Config.Explosion is null
                ? null
                : new RocketPayload(
                    Stats.BlastRadius,
                    Stats.RingCount,
                    Stats.RingDistance,
                    Stats.RingDamageMult,
                    Stats.RingRadiusMult,
                    Stats.RingDelay,
                    Stats.RingInterval),
            Stats.WallBounces);
    }

    // Turns the aim inside the weapon's spread cone. A single bullet lands anywhere in the
    // cone at random; several pellets each get their own slice of it, so a volley always
    // covers the whole cone instead of clumping by chance.
    private Vector2 Scatter(Vector2 direction, int pellet, int pelletCount)
    {
        var spread = MathHelper.ToRadians(Stats.SpreadDegrees);
        if (spread <= 0f || (pelletCount <= 1 && _random is null))
        {
            return direction;
        }

        var slice = spread / pelletCount;
        var angle = -spread / 2f + slice * (pellet + 0.5f);
        if (_random is not null)
        {
            angle += _random.Range(-slice / 2f, slice / 2f);
        }

        var cos = MathF.Cos(angle);
        var sin = MathF.Sin(angle);
        return new Vector2(direction.X * cos - direction.Y * sin, direction.X * sin + direction.Y * cos);
    }

    private bool WantsToFire(in WeaponTrigger trigger)
    {
        if (Stats.Automatic)
        {
            return trigger.Held;
        }

        // A press made before the weapon is ready still counts: while the button is held, or
        // for a moment after a quick click, so fast clicking does not drop shots.
        if (trigger.Pressed)
        {
            _fireQueued = true;
            _clickBufferLeft = ClickBufferSeconds;
        }
        else if (!trigger.Held)
        {
            _fireQueued = false;
        }

        return _fireQueued || _clickBufferLeft > 0f;
    }

    private bool TakeRound()
    {
        if (!_rules.ReloadsEnabled || !UsesMagazine)
        {
            if (!ReserveHasAmmo)
            {
                _audio.PlaySfx(SfxIds.WeaponEmpty);
                return false;
            }

            if (!HasInfiniteReserve)
            {
                Reserve--;
            }

            return true;
        }

        if (Magazine == 0)
        {
            if (!TryStartReload())
            {
                _audio.PlaySfx(SfxIds.WeaponEmpty);
            }

            return false;
        }

        Magazine--;
        return true;
    }

    private bool TryStartReload()
    {
        if (!UsesMagazine || IsReloading || Magazine >= Stats.MagSize || !ReserveHasAmmo)
        {
            return false;
        }

        _fireQueued = false;
        _clickBufferLeft = 0f;
        _reloadLeft = ReloadSeconds;
        _audio.PlaySfx(SfxIds.WeaponReloadStart);
        if (_reloadLeft <= TimeEpsilon)
        {
            FinishReload();
        }

        return true;
    }

    private void FinishReload()
    {
        _reloadLeft = 0f;

        var needed = Stats.MagSize - Magazine;
        var loaded = HasInfiniteReserve ? needed : Math.Min(needed, Reserve);
        Magazine += loaded;
        if (!HasInfiniteReserve)
        {
            Reserve -= loaded;
        }

        _audio.PlaySfx(SfxIds.WeaponReloadEnd);
    }
}
