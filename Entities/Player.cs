using System;
using System.Collections.Generic;
using Fourgate.Core.Audio;
using Fourgate.Core.Data;
using Fourgate.Core.Input;
using Fourgate.Core.Rendering;
using Fourgate.Weapons;
using Microsoft.Xna.Framework;

namespace Fourgate.Entities;

/// <summary>A player character. Owns its input source and all per-player state.</summary>
public sealed class Player
{
    private static readonly Color[] BodyColors = { new(90, 170, 255), new(255, 170, 70) };
    private static readonly Color DashColor = new(235, 245, 255);
    private static readonly Color HurtColor = new(255, 60, 60);

    private const float HurtFlashSeconds = 0.18f;

    private readonly PlayerConfig _config;
    private readonly IAudioManager _audio;
    private readonly List<Weapon> _weapons = new();
    private readonly Dictionary<string, int> _upgradeLevels = new();
    private readonly Dictionary<string, int> _weaponLevels = new();
    private float _maxHpBonus;
    private float _moveSpeedMultiplier = 1f;
    private float _dashCooldownMultiplier = 1f;
    private float _ammoCapacityMultiplier = 1f;
    private Vector2 _dashDirection;
    private float _dashTimeLeft;
    private float _dashCooldownLeft;
    private float _hurtFlashLeft;

    public Player(int index, IPlayerInput input, PlayerConfig config, IAudioManager audio, Vector2 position)
    {
        Index = index;
        Input = input;
        _config = config;
        _audio = audio;
        Position = position;
        Hp = config.MaxHp;
    }

    /// <summary>Zero-based player number.</summary>
    public int Index { get; }

    public IPlayerInput Input { get; }

    public PlayerProgression Progression { get; } = new();

    public Vector2 Position { get; private set; }

    /// <summary>Unit-length facing direction in world space.</summary>
    public Vector2 AimDirection { get; private set; } = Vector2.UnitX;

    public float Radius => _config.Radius;

    public float Hp { get; private set; }

    public float MaxHp => _config.MaxHp + _maxHpBonus;

    /// <summary>World units per second, including upgrades.</summary>
    public float MoveSpeed => _config.MoveSpeed * _moveSpeedMultiplier;

    /// <summary>Seconds between dash starts, including upgrades.</summary>
    public float DashCooldown => _config.Dash.Cooldown * _dashCooldownMultiplier;

    /// <summary>Scales the crate pickup radius (pickup range upgrade).</summary>
    public float PickupRangeMultiplier { get; private set; } = 1f;

    public bool IsAlive => Hp > 0f;

    /// <summary>Everything carried right now: guns in slot order, then equipment in cycle order.</summary>
    public IReadOnlyList<Weapon> Weapons => _weapons;

    /// <summary>The gun in hand.</summary>
    public Weapon? CurrentWeapon { get; private set; }

    /// <summary>The equipment the equipment button uses.</summary>
    public Weapon? ActiveEquipment { get; private set; }

    public bool IsDashing => _dashTimeLeft > 0f;

    /// <summary>True while nothing can damage the player (dash i-frames).</summary>
    public bool IsInvulnerable => IsDashing && _config.Dash.IFrames;

    /// <summary>Seconds until the next dash is available.</summary>
    public float DashCooldownLeft => _dashCooldownLeft;

    /// <summary>1 when the dash is ready, rising from 0 while it recharges.</summary>
    public float DashReadiness =>
        DashCooldown > 0f ? 1f - _dashCooldownLeft / DashCooldown : 1f;

    public VisualDescriptor Visual
    {
        get
        {
            var body = BodyColors[Index % BodyColors.Length];
            if (IsDashing)
            {
                body = DashColor;
            }
            else if (_dashCooldownLeft > 0f)
            {
                // Dimmed while the dash recharges.
                body = Color.Lerp(body, Color.Black, 0.35f);
            }

            body = Color.Lerp(body, HurtColor, _hurtFlashLeft / HurtFlashSeconds);
            return new VisualDescriptor(ShapeKind.Circle, Radius, body);
        }
    }

    /// <summary>Adds a weapon to the inventory and selects it if nothing of its kind is selected yet.</summary>
    public void GiveWeapon(Weapon weapon)
    {
        weapon.ReserveCapacityMultiplier = _ammoCapacityMultiplier;
        weapon.SetLevel(GetWeaponLevel(weapon.Config.Id));

        // Guns first, then equipment, each in slot order, so cycling follows the slot numbers.
        var index = 0;
        while (index < _weapons.Count && ComesBefore(_weapons[index].Config, weapon.Config))
        {
            index++;
        }

        _weapons.Insert(index, weapon);
        if (weapon.Config.Category == WeaponCategory.Gun)
        {
            CurrentWeapon ??= weapon;
        }
        else
        {
            ActiveEquipment ??= weapon;
        }
    }

    public bool OwnsWeapon(string weaponId)
    {
        for (var i = 0; i < _weapons.Count; i++)
        {
            if (_weapons[i].Config.Id == weaponId)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// This player's level for a weapon. It is kept whether or not the weapon is carried,
    /// so a weapon that is lost and found again comes back at its level.
    /// </summary>
    public int GetWeaponLevel(string weaponId)
    {
        return _weaponLevels.TryGetValue(weaponId, out var level) ? level : 1;
    }

    /// <summary>Raises a weapon's level by one, up to the maximum, and updates the carried copy if there is one.</summary>
    public void LevelUpWeapon(string weaponId)
    {
        var level = Math.Min(WeaponConfig.MaxLevel, GetWeaponLevel(weaponId) + 1);
        _weaponLevels[weaponId] = level;

        for (var i = 0; i < _weapons.Count; i++)
        {
            if (_weapons[i].Config.Id == weaponId)
            {
                _weapons[i].SetLevel(level);
            }
        }

        _audio.PlaySfx(SfxIds.WeaponLevelUp);
    }

    /// <summary>Switches to a carried gun. An unfinished reload of the old gun is lost.</summary>
    public void SelectWeapon(Weapon weapon)
    {
        if (ReferenceEquals(weapon, CurrentWeapon)
            || weapon.Config.Category != WeaponCategory.Gun
            || !_weapons.Contains(weapon))
        {
            return;
        }

        CurrentWeapon?.CancelReload();
        CurrentWeapon = weapon;
        weapon.OnSelected();
        _audio.PlaySfx(SfxIds.WeaponSwitch);
    }

    /// <summary>How many times this player has taken the upgrade with the given id.</summary>
    public int GetUpgradeLevel(string upgradeId)
    {
        return _upgradeLevels.TryGetValue(upgradeId, out var level) ? level : 0;
    }

    /// <summary>Raises an upgrade by one level and applies its effect.</summary>
    public void ApplyUpgrade(UpgradeConfig upgrade)
    {
        var level = GetUpgradeLevel(upgrade.Id) + 1;
        _upgradeLevels[upgrade.Id] = level;

        var total = upgrade.AmountPerLevel * level;
        switch (upgrade.Stat)
        {
            case UpgradeStat.MaxHp:
                _maxHpBonus = total;
                Heal(upgrade.AmountPerLevel);
                break;

            case UpgradeStat.MoveSpeed:
                _moveSpeedMultiplier = 1f + total;
                break;

            case UpgradeStat.DashCooldown:
                _dashCooldownMultiplier = Math.Max(0f, 1f - total);
                break;

            case UpgradeStat.AmmoCapacity:
                _ammoCapacityMultiplier = 1f + total;
                for (var i = 0; i < _weapons.Count; i++)
                {
                    _weapons[i].ReserveCapacityMultiplier = _ammoCapacityMultiplier;
                }

                break;

            case UpgradeStat.PickupRange:
                PickupRangeMultiplier = 1f + total;
                break;
        }
    }

    /// <summary>Restores all HP of a living player.</summary>
    public void HealToFull()
    {
        if (IsAlive)
        {
            Hp = MaxHp;
        }
    }

    /// <summary>Restores HP up to the maximum. Returns false when dead or already at full HP.</summary>
    public bool Heal(float amount)
    {
        if (!IsAlive || Hp >= MaxHp || amount <= 0f)
        {
            return false;
        }

        Hp = Math.Min(MaxHp, Hp + amount);
        return true;
    }

    /// <summary>
    /// Deals damage unless the player is dead or invulnerable. Returns true when it landed.
    /// </summary>
    public bool TakeDamage(float amount)
    {
        if (!IsAlive || IsInvulnerable || amount <= 0f)
        {
            return false;
        }

        Hp = Math.Max(0f, Hp - amount);
        _hurtFlashLeft = HurtFlashSeconds;
        _audio.PlaySfx(IsAlive ? SfxIds.PlayerHurt : SfxIds.PlayerDeath);
        return true;
    }

    /// <summary>Advances the player by one simulation step. The input must already be polled.</summary>
    public void Update(float deltaSeconds, Arena arena, IShotHandler shots)
    {
        _hurtFlashLeft = Math.Max(0f, _hurtFlashLeft - deltaSeconds);
        if (!IsAlive)
        {
            return;
        }

        UpdateMovement(deltaSeconds, arena);
        HandleWeaponSwitch();
        HandleEquipmentCycle();

        var muzzle = Position + AimDirection * Radius;
        CurrentWeapon?.Update(deltaSeconds, WeaponTrigger.ForGun(Input), muzzle, AimDirection, Index, shots);

        // Equipment works alongside the gun, on its own button and its own cooldown.
        ActiveEquipment?.Update(deltaSeconds, WeaponTrigger.ForEquipment(Input), muzzle, AimDirection, Index, shots);
    }

    private void HandleWeaponSwitch()
    {
        var slot = Input.WeaponSlotPressed;
        if (slot > 0)
        {
            for (var i = 0; i < _weapons.Count; i++)
            {
                var config = _weapons[i].Config;
                if (config.Category == WeaponCategory.Gun && config.Slot == slot)
                {
                    SelectWeapon(_weapons[i]);
                    return;
                }
            }

            return;
        }

        var next = NextOfCategory(CurrentWeapon, Input.WeaponCycle);
        if (next is not null)
        {
            SelectWeapon(next);
        }
    }

    private void HandleEquipmentCycle()
    {
        var next = NextOfCategory(ActiveEquipment, Input.EquipmentCycle);
        if (next is null)
        {
            return;
        }

        ActiveEquipment = next;
        _audio.PlaySfx(SfxIds.EquipmentSwitch);
    }

    // The next carried weapon of the same category in the given direction, wrapping
    // around; null when there is no other one or no direction.
    private Weapon? NextOfCategory(Weapon? from, int direction)
    {
        if (from is null || direction == 0)
        {
            return null;
        }

        var count = _weapons.Count;
        var start = _weapons.IndexOf(from);
        for (var step = 1; step < count; step++)
        {
            var index = ((start + Math.Sign(direction) * step) % count + count) % count;
            if (_weapons[index].Config.Category == from.Config.Category)
            {
                return _weapons[index];
            }
        }

        return null;
    }

    private static bool ComesBefore(WeaponConfig a, WeaponConfig b)
    {
        return a.Category != b.Category ? a.Category < b.Category : a.Slot < b.Slot;
    }

    private void UpdateMovement(float deltaSeconds, Arena arena)
    {
        AimDirection = Input.AimDirection;

        var move = Input.Move;
        if (move.LengthSquared() > 1f)
        {
            move.Normalize();
        }

        _dashCooldownLeft = Math.Max(0f, _dashCooldownLeft - deltaSeconds);
        if (Input.DashPressed && !IsDashing && _dashCooldownLeft <= 0f)
        {
            StartDash(move);
        }

        var walkSeconds = deltaSeconds;
        if (IsDashing)
        {
            var dash = _config.Dash;
            var dashSeconds = Math.Min(deltaSeconds, _dashTimeLeft);
            Position += _dashDirection * (dash.Distance / dash.Duration) * dashSeconds;

            _dashTimeLeft -= dashSeconds;
            if (_dashTimeLeft < 0.0001f)
            {
                _dashTimeLeft = 0f;
            }

            walkSeconds -= dashSeconds;
        }

        Position += move * MoveSpeed * walkSeconds;
        Position = arena.ClampInside(Position, Radius);
    }

    private void StartDash(Vector2 move)
    {
        // Dash where the player is heading; standing still dashes toward the aim.
        _dashDirection = move != Vector2.Zero ? Vector2.Normalize(move) : AimDirection;
        _dashTimeLeft = _config.Dash.Duration;
        _dashCooldownLeft = DashCooldown;
        _audio.PlaySfx(SfxIds.PlayerDash);
    }
}
