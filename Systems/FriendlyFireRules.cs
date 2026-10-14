using System;
using Fourgate.Core.Data;
using Fourgate.Entities;

namespace Fourgate.Systems;

/// <summary>
/// Damage players take from their own or each other's weapons: a share of the damage, capped,
/// and (normally) never lethal. Mistakes hurt, but a player's own weapons do not kill them.
/// </summary>
public sealed class FriendlyFireRules
{
    private readonly FriendlyFireConfig _config;

    public FriendlyFireRules(FriendlyFireConfig config)
    {
        _config = config;
    }

    /// <summary>Applies the player's part of one explosion.</summary>
    /// <param name="player">The player caught in the blast.</param>
    /// <param name="ownerIndex">Index of the player whose explosive it was.</param>
    /// <param name="damage">The explosion's full damage.</param>
    public void ApplyExplosion(Player player, int ownerIndex, float damage)
    {
        var amount = Shared(player, ownerIndex, damage);
        if (_config.MaxDamagePerExplosion > 0f)
        {
            amount = Math.Min(amount, _config.MaxDamagePerExplosion);
        }

        Apply(player, amount, silent: false);
    }

    /// <summary>Applies the player's part of one frame of fire.</summary>
    /// <param name="player">The player standing in the fire.</param>
    /// <param name="ownerIndex">Index of the player whose fire it is.</param>
    /// <param name="damagePerSecond">The fire's full damage per second.</param>
    /// <param name="seconds">Length of this frame.</param>
    public void ApplyFire(Player player, int ownerIndex, float damagePerSecond, float seconds)
    {
        var perSecond = Shared(player, ownerIndex, damagePerSecond);
        if (_config.MaxDamagePerSecond > 0f)
        {
            perSecond = Math.Min(perSecond, _config.MaxDamagePerSecond);
        }

        // Fire ticks every frame; the hurt sound would play constantly.
        Apply(player, perSecond * seconds, silent: true);
    }

    private float Shared(Player player, int ownerIndex, float damage)
    {
        if (player.Index == ownerIndex)
        {
            return damage * _config.SelfExplosiveDamage;
        }

        return _config.Enabled ? damage * _config.DamageToOther : 0f;
    }

    private void Apply(Player player, float amount, bool silent)
    {
        if (amount > 0f)
        {
            player.TakeDamage(amount, silent, nonLethal: !_config.CanKill);
        }
    }
}
