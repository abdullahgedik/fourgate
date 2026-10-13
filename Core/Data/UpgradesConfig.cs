using System.Collections.Generic;

namespace Fourgate.Core.Data;

/// <summary>Upgrade card pool loaded from <c>upgrades.json</c>.</summary>
public sealed class UpgradesConfig
{
    /// <summary>Cards shown for each pick.</summary>
    public required int CardsPerPick { get; init; }

    public required UpgradeFallbackConfig Fallback { get; init; }

    public required List<UpgradeConfig> Upgrades { get; init; }

    /// <summary>Checks value ranges; throws <see cref="DataLoadException"/> naming the bad field.</summary>
    public void Validate(string file)
    {
        DataLoadException.Require(CardsPerPick >= 1, file, "cardsPerPick", "must be 1 or greater");

        DataLoadException.Require(Fallback is not null, file, "fallback", "is required");
        DataLoadException.Require(
            !string.IsNullOrWhiteSpace(Fallback!.DisplayName), file, "fallback.displayName", "must not be empty");
        DataLoadException.Require(
            !string.IsNullOrWhiteSpace(Fallback.Description), file, "fallback.description", "must not be empty");

        DataLoadException.Require(Upgrades is not null, file, "upgrades", "is required");
        var ids = new HashSet<string>();
        for (var i = 0; i < Upgrades!.Count; i++)
        {
            var path = $"upgrades[{i}]";
            DataLoadException.Require(Upgrades[i] is not null, file, path, "must not be null");
            Upgrades[i].Validate(file, path);
            DataLoadException.Require(
                ids.Add(Upgrades[i].Id), file, $"{path}.id", $"'{Upgrades[i].Id}' is used more than once");
        }
    }
}
