namespace Fourgate.Core.Data;

/// <summary>One non-weapon upgrade card from <c>upgrades.json</c>.</summary>
public sealed class UpgradeConfig
{
    public required string Id { get; init; }

    public required string DisplayName { get; init; }

    /// <summary>One short line shown on the card.</summary>
    public required string Description { get; init; }

    public required UpgradeStat Stat { get; init; }

    /// <summary>Effect of each level; its meaning depends on <see cref="Stat"/>.</summary>
    public required float AmountPerLevel { get; init; }

    /// <summary>How many times the card can be taken.</summary>
    public required int MaxLevel { get; init; }

    /// <summary>Checks value ranges; <paramref name="path"/> locates this entry inside the file.</summary>
    public void Validate(string file, string path)
    {
        DataLoadException.Require(!string.IsNullOrWhiteSpace(Id), file, $"{path}.id", "must not be empty");
        DataLoadException.Require(
            !string.IsNullOrWhiteSpace(DisplayName), file, $"{path}.displayName", "must not be empty");
        DataLoadException.Require(
            !string.IsNullOrWhiteSpace(Description), file, $"{path}.description", "must not be empty");
        DataLoadException.Require(AmountPerLevel > 0, file, $"{path}.amountPerLevel", "must be greater than 0");
        DataLoadException.Require(MaxLevel >= 1, file, $"{path}.maxLevel", "must be 1 or greater");
        DataLoadException.Require(
            Stat != UpgradeStat.DashCooldown || AmountPerLevel * MaxLevel < 1f,
            file, $"{path}.amountPerLevel", "times maxLevel must stay below 1, or the dash cooldown would reach zero");
    }
}
