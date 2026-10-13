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

    // Half the gap between the two muzzles of a dual-wielded weapon.
    private const float DualWieldOffset = 7f;

    private readonly GameConfig _rules;
    private readonly IAudioManager _audio;
    private readonly GameRandom? _random;
    private float _cooldown;
    private float _reloadLeft;
    private bool _fireQueued;

    /// <param name="config">The weapon's data.</param>
    /// <param name="rules">Global reload rules.</param>
    /// <param name="audio">Receives the weapon's sound events.</param>
    /// <param name="random">Source of shot spread; without one the weapon fires dead straight.</param>
    public Weapon(WeaponConfig config, GameConfig rules, IAudioManager audio, GameRandom? random = null)
    {
        Config = config;
        _rules = rules;
        _audio = audio;
        _random = random;

        Level = 1;
        Stats = WeaponStats.Compute(config, Level);
        Magazine = Stats.MagSize;
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
    public int ReserveMax => (int)MathF.Round(Config.Ammo.ReserveMax * ReserveCapacityMultiplier);

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

    private float ReloadSeconds => Stats.ReloadTime * _rules.GlobalReloadTimeMultiplier;

    private bool ReserveHasAmmo => HasInfiniteReserve || Reserve > 0;

    /// <summary>Sets the weapon's level and recomputes its stats. Rounds already loaded are kept.</summary>
    public void SetLevel(int level)
    {
        Level = Math.Clamp(level, 1, WeaponConfig.MaxLevel);
        Stats = WeaponStats.Compute(Config, Level);
    }

    /// <summary>Advances timers and fires or reloads according to the input.</summary>
    /// <param name="deltaSeconds">Frame time.</param>
    /// <param name="input">The owner's controls.</param>
    /// <param name="muzzle">World position shots start from.</param>
    /// <param name="direction">Unit-length aim direction.</param>
    /// <param name="ownerIndex">Index of the owning player.</param>
    /// <param name="shots">Resolves the fired shots.</param>
    public void Update(
        float deltaSeconds,
        IPlayerInput input,
        Vector2 muzzle,
        Vector2 direction,
        int ownerIndex,
        IShotHandler shots)
    {
        // Goes negative only on the frame the cooldown runs out; that overshoot is carried
        // into the next shot so the fire rate stays exact. An idle weapon rests at zero.
        _cooldown = _cooldown <= 0f ? 0f : _cooldown - deltaSeconds;

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
        else if (input.ReloadPressed)
        {
            TryStartReload();
        }

        if (!WantsToFire(input) || IsReloading || _cooldown > TimeEpsilon)
        {
            return;
        }

        _fireQueued = false;
        if (!TakeRound())
        {
            // A dry trigger pull also waits out the fire interval, so holding fire on an
            // empty weapon does not click every frame.
            _cooldown = 1f / Stats.FireRate;
            return;
        }

        _cooldown += 1f / Stats.FireRate;
        Fire(muzzle, direction, ownerIndex, shots);
        _audio.PlaySfx(SfxIds.WeaponFire);

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
        _reloadLeft = 0f;
        _fireQueued = false;
    }

    /// <summary>Called when the owner switches to this weapon; an empty magazine starts reloading at once.</summary>
    public void OnSelected()
    {
        if (_rules.ReloadsEnabled && Magazine == 0)
        {
            TryStartReload();
        }
    }

    private void Fire(Vector2 muzzle, Vector2 direction, int ownerIndex, IShotHandler shots)
    {
        if (!Stats.DualWield)
        {
            FireBullet(muzzle, direction, ownerIndex, shots);
            return;
        }

        // Two muzzles side by side, one bullet from each.
        var side = new Vector2(-direction.Y, direction.X) * DualWieldOffset;
        FireBullet(muzzle + side, direction, ownerIndex, shots);
        FireBullet(muzzle - side, direction, ownerIndex, shots);
    }

    private void FireBullet(Vector2 muzzle, Vector2 direction, int ownerIndex, IShotHandler shots)
    {
        shots.FireProjectile(
            muzzle,
            Scatter(direction),
            Config.Projectile,
            Stats.Damage,
            ownerIndex,
            Stats.Range,
            Stats.Pierce,
            Stats.Ricochets);
    }

    // Turns the aim by a random angle inside the weapon's spread cone.
    private Vector2 Scatter(Vector2 direction)
    {
        if (_random is null || Stats.SpreadDegrees <= 0f)
        {
            return direction;
        }

        var half = MathHelper.ToRadians(Stats.SpreadDegrees) / 2f;
        var angle = _random.Range(-half, half);
        var cos = MathF.Cos(angle);
        var sin = MathF.Sin(angle);
        return new Vector2(direction.X * cos - direction.Y * sin, direction.X * sin + direction.Y * cos);
    }

    private bool WantsToFire(IPlayerInput input)
    {
        if (Config.Automatic)
        {
            return input.FireHeld;
        }

        // A press made just before the weapon is ready still counts, so fast clicking
        // does not drop shots.
        if (input.FirePressed)
        {
            _fireQueued = true;
        }
        else if (!input.FireHeld)
        {
            _fireQueued = false;
        }

        return _fireQueued;
    }

    private bool TakeRound()
    {
        if (!_rules.ReloadsEnabled)
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
        if (IsReloading || Magazine >= Stats.MagSize || !ReserveHasAmmo)
        {
            return false;
        }

        _fireQueued = false;
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
