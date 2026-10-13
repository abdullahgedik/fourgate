using System.Collections.Generic;
using Fourgate.Core.Data;
using Fourgate.Weapons;

namespace Fourgate.Systems;

/// <summary>One card offered to a player for an upgrade pick.</summary>
public sealed class UpgradeCard
{
    private UpgradeCard(
        UpgradeConfig? upgrade, WeaponConfig? weapon, string title, IReadOnlyList<string> lines, string detail)
    {
        Upgrade = upgrade;
        Weapon = weapon;
        Title = title;
        Lines = lines;
        Detail = detail;
    }

    /// <summary>The stat upgrade this card grants, if it is a stat card.</summary>
    public UpgradeConfig? Upgrade { get; }

    /// <summary>The weapon this card levels up, if it is a weapon card.</summary>
    public WeaponConfig? Weapon { get; }

    public string Title { get; }

    /// <summary>Short lines describing what the card does.</summary>
    public IReadOnlyList<string> Lines { get; }

    /// <summary>Small print, e.g. the level the card would bring the upgrade to.</summary>
    public string Detail { get; }

    /// <summary>Card for a stat upgrade the player currently has at <paramref name="currentLevel"/>.</summary>
    public static UpgradeCard ForUpgrade(UpgradeConfig upgrade, int currentLevel)
    {
        return new UpgradeCard(
            upgrade,
            null,
            upgrade.DisplayName.ToUpperInvariant(),
            new[] { upgrade.Description },
            $"LEVEL {currentLevel + 1} OF {upgrade.MaxLevel}");
    }

    /// <summary>Card that raises a weapon the player has at <paramref name="currentLevel"/> by one level.</summary>
    public static UpgradeCard ForWeapon(WeaponConfig weapon, int currentLevel)
    {
        var lines = new List<string>();
        WeaponStats.DescribeLevel(weapon, currentLevel + 1, lines);

        return new UpgradeCard(
            null,
            weapon,
            weapon.DisplayName.ToUpperInvariant(),
            lines,
            $"WEAPON LEVEL {currentLevel + 1} OF {WeaponConfig.MaxLevel}");
    }

    /// <summary>The full-heal card shown when nothing else can be taken.</summary>
    public static UpgradeCard ForFallback(UpgradeFallbackConfig fallback)
    {
        return new UpgradeCard(
            null, null, fallback.DisplayName.ToUpperInvariant(), new[] { fallback.Description }, string.Empty);
    }
}
