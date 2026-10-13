namespace Fourgate.Core.Data;

/// <summary>Text of the full-heal card offered when a player has no upgrades left to take.</summary>
public sealed class UpgradeFallbackConfig
{
    public required string DisplayName { get; init; }

    public required string Description { get; init; }
}
