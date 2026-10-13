using Fourgate.Core.Data;

namespace Fourgate.Systems;

/// <summary>One card offered to a player for an upgrade pick.</summary>
public sealed class UpgradeCard
{
    private UpgradeCard(UpgradeConfig? upgrade, string title, string description, string detail)
    {
        Upgrade = upgrade;
        Title = title;
        Description = description;
        Detail = detail;
    }

    /// <summary>The stat upgrade this card grants; null for the full-heal fallback.</summary>
    public UpgradeConfig? Upgrade { get; }

    public string Title { get; }

    public string Description { get; }

    /// <summary>Small print, e.g. the level the card would bring the upgrade to.</summary>
    public string Detail { get; }

    /// <summary>Card for a stat upgrade the player currently has at <paramref name="currentLevel"/>.</summary>
    public static UpgradeCard ForUpgrade(UpgradeConfig upgrade, int currentLevel)
    {
        return new UpgradeCard(
            upgrade,
            upgrade.DisplayName.ToUpperInvariant(),
            upgrade.Description,
            $"LEVEL {currentLevel + 1} OF {upgrade.MaxLevel}");
    }

    /// <summary>The full-heal card shown when nothing else can be taken.</summary>
    public static UpgradeCard ForFallback(UpgradeFallbackConfig fallback)
    {
        return new UpgradeCard(null, fallback.DisplayName.ToUpperInvariant(), fallback.Description, string.Empty);
    }
}
