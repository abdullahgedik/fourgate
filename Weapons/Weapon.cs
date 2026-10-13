using System;
using Fourgate.Core.Audio;
using Fourgate.Core.Data;
using Fourgate.Core.Input;
using Microsoft.Xna.Framework;

namespace Fourgate.Weapons;

/// <summary>One player's copy of a weapon: fire timing, magazine, reserve and reload state.</summary>
public sealed class Weapon
{
    // Absorbs float error so a cooldown that should end exactly on a frame does.
    private const float TimeEpsilon = 0.0001f;

    private readonly GameConfig _rules;
    private readonly IAudioManager _audio;
    private float _cooldown;
    private float _reloadLeft;
    private bool _fireQueued;

    public Weapon(WeaponConfig config, GameConfig rules, IAudioManager audio)
    {
        Config = config;
        _rules = rules;
        _audio = audio;
        Magazine = config.Ammo.MagSize;
        Reserve = config.Ammo.InfiniteReserve ? 0 : config.Ammo.ReserveMax;
    }

    public WeaponConfig Config { get; }

    /// <summary>Rounds in the magazine. Unused when reloads are disabled.</summary>
    public int Magazine { get; private set; }

    /// <summary>Rounds in reserve. Meaningless when <see cref="HasInfiniteReserve"/> is true.</summary>
    public int Reserve { get; private set; }

    public bool HasInfiniteReserve => Config.Ammo.InfiniteReserve;

    public bool IsReloading => _reloadLeft > 0f;

    /// <summary>0 at the start of a reload, 1 when it finishes; 0 when not reloading.</summary>
    public float ReloadProgress
    {
        get
        {
            var total = ReloadSeconds;
            return IsReloading && total > 0f ? 1f - _reloadLeft / total : 0f;
        }
    }

    private float ReloadSeconds => Config.Ammo.ReloadTime * _rules.GlobalReloadTimeMultiplier;

    private bool ReserveHasAmmo => HasInfiniteReserve || Reserve > 0;

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
            _cooldown = 1f / Config.FireRate;
            return;
        }

        _cooldown += 1f / Config.FireRate;
        shots.FireProjectile(muzzle, direction, Config.Projectile, Config.Damage, ownerIndex);
        _audio.PlaySfx(SfxIds.WeaponFire);

        if (_rules.ReloadsEnabled && Magazine == 0)
        {
            TryStartReload();
        }
    }

    /// <summary>Drops an unfinished reload; its progress is lost (weapon switch).</summary>
    public void CancelReload()
    {
        _reloadLeft = 0f;
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
        if (IsReloading || Magazine >= Config.Ammo.MagSize || !ReserveHasAmmo)
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

        var needed = Config.Ammo.MagSize - Magazine;
        var loaded = HasInfiniteReserve ? needed : Math.Min(needed, Reserve);
        Magazine += loaded;
        if (!HasInfiniteReserve)
        {
            Reserve -= loaded;
        }

        _audio.PlaySfx(SfxIds.WeaponReloadEnd);
    }
}
