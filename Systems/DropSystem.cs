using System;
using System.Collections.Generic;
using Fourgate.Core.Audio;
using Fourgate.Core.Data;
using Fourgate.Core.Pooling;
using Fourgate.Core.Rendering;
using Fourgate.Core.Rng;
using Fourgate.Entities;
using Fourgate.Entities.Enemies;
using Fourgate.Entities.Pickups;
using Microsoft.Xna.Framework;

namespace Fourgate.Systems;

/// <summary>Decides what killed enemies drop, and owns the crates until they are picked up or expire.</summary>
public sealed class DropSystem
{
    private const int InitialCrates = 32;

    private static readonly Color BorderColor = new(20, 20, 26);
    private static readonly Color GlyphColor = new(245, 245, 245);

    private readonly CratesConfig _config;
    private readonly GameRandom _random;
    private readonly IAudioManager _audio;
    private readonly WeaponUnlocks _unlocks;
    private readonly Pool<Crate> _crates = new(InitialCrates);

    /// <summary>Called when a crate gives a player a weapon they did not carry (for a notice).</summary>
    public Action<Player, WeaponConfig>? WeaponGained { get; set; }

    public DropSystem(CratesConfig config, GameRandom random, IAudioManager audio, WeaponUnlocks unlocks)
    {
        _config = config;
        _random = random;
        _audio = audio;
        _unlocks = unlocks;
    }

    public int Count => _crates.Count;

    /// <summary>Multiplies the chance that a drop is a health crate (Nightmare lowers it).</summary>
    public float HealthChanceMultiplier { get; set; } = 1f;

    public Crate this[int index] => _crates[index];

    /// <summary>
    /// Picks the crate type for a drop from two random rolls (each 0–1): upgrade crates are
    /// rare, and health becomes far more likely while a player is low on HP.
    /// </summary>
    public static CrateType ChooseType(
        CratesConfig config, bool anyPlayerLowHp, float upgradeRoll, float healthRoll, float healthChanceMultiplier = 1f)
    {
        if (upgradeRoll < config.UpgradeCrateChance)
        {
            return CrateType.Upgrade;
        }

        var healthChance = (anyPlayerLowHp ? config.HealthChanceLowHp : config.HealthChanceNormal) * healthChanceMultiplier;
        return healthRoll < healthChance ? CrateType.Health : CrateType.Ammo;
    }

    /// <summary>Rolls the dead enemy's drop chance and leaves a crate where it died.</summary>
    public void OnEnemyKilled(in EnemyDeath death, IReadOnlyList<Player> players)
    {
        if (!_random.Chance(death.Config.DropChance))
        {
            return;
        }

        var type = ChooseType(
            _config, AnyLivingPlayerLowOnHp(players), _random.NextFloat(), _random.NextFloat(), HealthChanceMultiplier);

        // An ammo crate nobody could pick up would only clutter the floor.
        if (type == CrateType.Ammo && !AnyLivingPlayerCanUseAmmoCrate(players))
        {
            return;
        }

        Spawn(type, death.Position);
    }

    public Crate Spawn(CrateType type, Vector2 position)
    {
        var crate = _crates.Rent();
        crate.Drop(type, position, _config.DespawnSeconds, null);
        _audio.PlaySfx(SfxIds.CrateDrop);
        return crate;
    }

    /// <summary>Places a crate holding a specific weapon. It stays until someone who lacks that weapon takes it.</summary>
    public Crate SpawnWeaponCrate(WeaponConfig weapon, Vector2 position)
    {
        var crate = _crates.Rent();
        crate.Drop(CrateType.Weapon, position, float.PositiveInfinity, weapon);
        _audio.PlaySfx(SfxIds.CrateDrop);
        return crate;
    }

    public void Update(float deltaSeconds, IReadOnlyList<Player> players)
    {
        for (var i = _crates.Count - 1; i >= 0; i--)
        {
            var crate = _crates[i];
            crate.Tick(deltaSeconds);

            if (crate.IsExpired || TryPickUp(crate, players))
            {
                _crates.ReturnAt(i);
            }
        }
    }

    public void Draw(IRenderer renderer)
    {
        for (var i = 0; i < _crates.Count; i++)
        {
            var crate = _crates[i];
            if (!crate.IsVisible(_config.BlinkSeconds))
            {
                continue;
            }

            var border = new Vector2(Crate.HalfSize + 3f);
            renderer.FillRect(crate.Position - border, border * 2f, BorderColor);
            renderer.DrawVisual(crate.Visual, crate.Position, 0f);
            DrawGlyph(renderer, crate);
        }
    }

    // Whoever touches the crate first gets it, if they can use it.
    private bool TryPickUp(Crate crate, IReadOnlyList<Player> players)
    {
        for (var i = 0; i < players.Count; i++)
        {
            var player = players[i];
            var reach = _config.PickupRadius * player.PickupRangeMultiplier;
            if (!player.IsAlive || Vector2.DistanceSquared(player.Position, crate.Position) > reach * reach)
            {
                continue;
            }

            if (TryApply(crate, player))
            {
                return true;
            }
        }

        return false;
    }

    // Gives a crate's contents to a player. Returns false when the player has no use for it.
    private bool TryApply(Crate crate, Player player)
    {
        switch (crate.Type)
        {
            case CrateType.Health:
                if (!player.Heal(_config.HealAmount))
                {
                    return false;
                }

                _audio.PlaySfx(SfxIds.PickupHealth);
                return true;

            case CrateType.Upgrade:
                player.Progression.AddPick();
                _audio.PlaySfx(SfxIds.PickupUpgrade);
                return true;

            case CrateType.Weapon:
                if (crate.Weapon is null || !_unlocks.Grant(player, crate.Weapon))
                {
                    return false;
                }

                WeaponGained?.Invoke(player, crate.Weapon);
                _audio.PlaySfx(SfxIds.PickupWeapon);
                return true;

            default:
                // A player's first crate after a weapon unlocks holds that weapon; later ones hold ammo.
                var missing = _unlocks.FirstMissingFor(player);
                if (missing is not null)
                {
                    _unlocks.Grant(player, missing);
                    WeaponGained?.Invoke(player, missing);
                    _audio.PlaySfx(SfxIds.PickupWeapon);
                    return true;
                }

                if (!AmmoDistribution.Distribute(
                    player.Weapons, _config.AmmoPrimaryFraction, _config.AmmoOthersFraction))
                {
                    return false;
                }

                _audio.PlaySfx(SfxIds.PickupAmmo);
                return true;
        }
    }

    private bool AnyLivingPlayerLowOnHp(IReadOnlyList<Player> players)
    {
        for (var i = 0; i < players.Count; i++)
        {
            var player = players[i];
            if (player.IsAlive && player.Hp < player.MaxHp * _config.LowHpThreshold)
            {
                return true;
            }
        }

        return false;
    }

    private bool AnyLivingPlayerCanUseAmmoCrate(IReadOnlyList<Player> players)
    {
        for (var i = 0; i < players.Count; i++)
        {
            var player = players[i];
            if (player.IsAlive
                && (AmmoDistribution.CanUse(player.Weapons) || _unlocks.FirstMissingFor(player) is not null))
            {
                return true;
            }
        }

        return false;
    }

    // Simple marks so crate types can be told apart without relying on color alone; on the lid in 3D.
    private static void DrawGlyph(IRenderer renderer, Crate crate)
    {
        var center = crate.Position;
        var lid = Depth3D.BodyHeight(Crate.HalfSize) + 0.5f;
        switch (crate.Type)
        {
            case CrateType.Health:
                renderer.FillRectRaised(center - new Vector2(10f, 3f), new Vector2(20f, 6f), lid, GlyphColor);
                renderer.FillRectRaised(center - new Vector2(3f, 10f), new Vector2(6f, 20f), lid, GlyphColor);
                break;

            case CrateType.Upgrade:
                renderer.FillRectRaised(center - new Vector2(3f, 2f), new Vector2(6f, 12f), lid, GlyphColor);
                renderer.FillRectRaised(center - new Vector2(9f, 6f), new Vector2(18f, 5f), lid, GlyphColor);
                renderer.FillRectRaised(center - new Vector2(4f, 11f), new Vector2(8f, 5f), lid, GlyphColor);
                break;

            case CrateType.Weapon:
                // A small gun: barrel and grip.
                renderer.FillRectRaised(center + new Vector2(-11f, -6f), new Vector2(22f, 7f), lid, GlyphColor);
                renderer.FillRectRaised(center + new Vector2(-9f, 1f), new Vector2(7f, 9f), lid, GlyphColor);
                break;

            default:
                for (var i = -1; i <= 1; i++)
                {
                    renderer.FillRectRaised(center + new Vector2(i * 8f - 2.5f, -9f), new Vector2(5f, 18f), lid, GlyphColor);
                }

                break;
        }
    }
}
