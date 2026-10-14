namespace Fourgate.Core.Data;

/// <summary>Two-player rules from <c>game.json</c>.</summary>
public sealed class CoopConfig
{
    /// <summary>Share of max HP (0–1) a downed player comes back with when the wave is cleared.</summary>
    public required float ReviveHpFraction { get; init; }

    /// <summary>Each wave's enemy budget is multiplied by this with two players.</summary>
    public required float EnemyBudgetMultiplier { get; init; }

    /// <summary>A boss's HP is multiplied by this with two players.</summary>
    public required float BossHpMultiplier { get; init; }

    public void Validate(string file)
    {
        DataLoadException.Require(
            ReviveHpFraction > 0 && ReviveHpFraction <= 1, file, "coop.reviveHpFraction", "must be above 0 and at most 1");
        DataLoadException.Require(
            EnemyBudgetMultiplier > 0, file, "coop.enemyBudgetMultiplier", "must be greater than 0");
        DataLoadException.Require(BossHpMultiplier > 0, file, "coop.bossHpMultiplier", "must be greater than 0");
    }
}
