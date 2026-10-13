using System;
using System.Collections.Generic;
using Fourgate.Core.Audio;
using Fourgate.Core.Data;
using Fourgate.Core.Rng;
using Fourgate.Entities;

namespace Fourgate.Systems;

/// <summary>
/// Deals upgrade cards for banked picks and applies the chosen one. Every player has
/// their own offer, so players pick independently.
/// </summary>
public sealed class UpgradeSystem
{
    private readonly UpgradesConfig _config;
    private readonly GameRandom _random;
    private readonly IAudioManager _audio;

    // _offers[i] belongs to players[i]; an empty list means no offer is open.
    private readonly List<List<UpgradeCard>> _offers = new();
    private readonly List<UpgradeConfig> _eligible = new();

    public UpgradeSystem(UpgradesConfig config, GameRandom random, IAudioManager audio)
    {
        _config = config;
        _random = random;
        _audio = audio;
    }

    /// <summary>True while at least one player has cards to choose from.</summary>
    public bool AnyOfferOpen
    {
        get
        {
            for (var i = 0; i < _offers.Count; i++)
            {
                if (_offers[i].Count > 0)
                {
                    return true;
                }
            }

            return false;
        }
    }

    /// <summary>
    /// Deals cards to every player who has a banked pick and no open offer.
    /// Returns true when any player has an offer open afterwards.
    /// </summary>
    public bool OpenOffers(IReadOnlyList<Player> players)
    {
        while (_offers.Count < players.Count)
        {
            _offers.Add(new List<UpgradeCard>());
        }

        for (var i = 0; i < players.Count; i++)
        {
            if (_offers[i].Count == 0 && players[i].Progression.BankedPicks > 0)
            {
                Deal(players[i], _offers[i]);
            }
        }

        return AnyOfferOpen;
    }

    /// <summary>The cards offered to <c>players[playerSlot]</c>; empty when that player has no offer open.</summary>
    public IReadOnlyList<UpgradeCard> GetOffer(int playerSlot)
    {
        return playerSlot < _offers.Count ? _offers[playerSlot] : Array.Empty<UpgradeCard>();
    }

    /// <summary>Applies one card of the player's open offer, spends the pick and closes the offer.</summary>
    public void Choose(Player player, int playerSlot, int cardIndex)
    {
        var offer = _offers[playerSlot];
        if (cardIndex < 0 || cardIndex >= offer.Count)
        {
            return;
        }

        var card = offer[cardIndex];
        if (card.Upgrade is not null)
        {
            player.ApplyUpgrade(card.Upgrade);
        }
        else
        {
            player.HealToFull();
        }

        player.Progression.SpendPick();
        offer.Clear();
        _audio.PlaySfx(SfxIds.UpgradePicked);
    }

    // Draws distinct random cards from the upgrades the player has not maxed out.
    private void Deal(Player player, List<UpgradeCard> offer)
    {
        _eligible.Clear();
        for (var i = 0; i < _config.Upgrades.Count; i++)
        {
            var upgrade = _config.Upgrades[i];
            if (player.GetUpgradeLevel(upgrade.Id) < upgrade.MaxLevel)
            {
                _eligible.Add(upgrade);
            }
        }

        if (_eligible.Count == 0)
        {
            offer.Add(UpgradeCard.ForFallback(_config.Fallback));
            return;
        }

        var count = Math.Min(_config.CardsPerPick, _eligible.Count);
        for (var i = 0; i < count; i++)
        {
            var pick = i + _random.NextIndex(_eligible.Count - i);
            (_eligible[i], _eligible[pick]) = (_eligible[pick], _eligible[i]);
            offer.Add(UpgradeCard.ForUpgrade(_eligible[i], player.GetUpgradeLevel(_eligible[i].Id)));
        }
    }
}
