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
    private readonly Pool<Crate> _crates = new(InitialCrates);

    public DropSystem(CratesConfig config, GameRandom random, IAudioManager audio)
    {
        _config = config;
        _random = random;
        _audio = audio;
    }

    public int Count => _crates.Count;

    public Crate this[int index] => _crates[index];

    /// <summary>
    /// Picks the crate type for a drop from two random rolls (each 0–1): upgrade crates are
    /// rare, and health becomes far more likely while a player is low on HP.
    /// </summary>
    public static CrateType ChooseType(CratesConfig config, bool anyPlayerLowHp, float upgradeRoll, float healthRoll)
    {
        if (upgradeRoll < config.UpgradeCrateChance)
        {
            return CrateType.Upgrade;
        }

        var healthChance = anyPlayerLowHp ? config.HealthChanceLowHp : config.HealthChanceNormal;
        return healthRoll < healthChance ? CrateType.Health : CrateType.Ammo;
    }

    /// <summary>Gives a crate's contents to a player. Returns false when the player has no use for it.</summary>
    public static bool TryApply(CrateType type, Player player, CratesConfig config)
    {
        switch (type)
        {
            case CrateType.Health:
                return player.Heal(config.HealAmount);

            case CrateType.Upgrade:
                player.Progression.AddPick();
                return true;

            default:
                return AmmoDistribution.Distribute(
                    player.Weapons, config.AmmoPrimaryFraction, config.AmmoOthersFraction);
        }
    }

    /// <summary>Rolls the dead enemy's drop chance and leaves a crate where it died.</summary>
    public void OnEnemyKilled(in EnemyDeath death, IReadOnlyList<Player> players)
    {
        if (!_random.Chance(death.Config.DropChance))
        {
            return;
        }

        var type = ChooseType(_config, AnyLivingPlayerLowOnHp(players), _random.NextFloat(), _random.NextFloat());

        // An ammo crate nobody could pick up would only clutter the floor.
        if (type == CrateType.Ammo && !AnyLivingPlayerCanUseAmmo(players))
        {
            return;
        }

        Spawn(type, death.Position);
    }

    public Crate Spawn(CrateType type, Vector2 position)
    {
        var crate = _crates.Rent();
        crate.Drop(type, position, _config.DespawnSeconds);
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

            if (TryApply(crate.Type, player, _config))
            {
                _audio.PlaySfx(crate.Type switch
                {
                    CrateType.Health => SfxIds.PickupHealth,
                    CrateType.Upgrade => SfxIds.PickupUpgrade,
                    _ => SfxIds.PickupAmmo,
                });
                return true;
            }
        }

        return false;
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

    private static bool AnyLivingPlayerCanUseAmmo(IReadOnlyList<Player> players)
    {
        for (var i = 0; i < players.Count; i++)
        {
            if (players[i].IsAlive && AmmoDistribution.CanUse(players[i].Weapons))
            {
                return true;
            }
        }

        return false;
    }

    // Simple marks so crate types can be told apart without relying on color alone.
    private static void DrawGlyph(IRenderer renderer, Crate crate)
    {
        var center = crate.Position;
        switch (crate.Type)
        {
            case CrateType.Health:
                renderer.FillRect(center - new Vector2(10f, 3f), new Vector2(20f, 6f), GlyphColor);
                renderer.FillRect(center - new Vector2(3f, 10f), new Vector2(6f, 20f), GlyphColor);
                break;

            case CrateType.Upgrade:
                renderer.FillRect(center - new Vector2(3f, 2f), new Vector2(6f, 12f), GlyphColor);
                renderer.FillRect(center - new Vector2(9f, 6f), new Vector2(18f, 5f), GlyphColor);
                renderer.FillRect(center - new Vector2(4f, 11f), new Vector2(8f, 5f), GlyphColor);
                break;

            default:
                for (var i = -1; i <= 1; i++)
                {
                    renderer.FillRect(center + new Vector2(i * 8f - 2.5f, -9f), new Vector2(5f, 18f), GlyphColor);
                }

                break;
        }
    }
}
