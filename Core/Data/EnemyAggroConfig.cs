namespace Fourgate.Core.Data;

/// <summary>How regular enemies keep and change targets in co-op (<c>game.json</c> <c>enemyAggro</c>).</summary>
public sealed class EnemyAggroConfig
{
    /// <summary>Another player must be this much closer (share of the distance) before a chaser switches.</summary>
    public float SwitchCloserShare { get; init; } = 0.3f;

    /// <summary>For the weakest-first rule: another player's health share must be this much lower to switch.</summary>
    public float WeakerShare { get; init; } = 0.15f;

    /// <summary>For the grudge rule: how long an enemy keeps after whoever hurt it.</summary>
    public float GrudgeSeconds { get; init; } = 4f;

    /// <summary>For the committed rule: shortest time a target is held.</summary>
    public float MinCommitSeconds { get; init; } = 4f;

    /// <summary>For the committed rule: longest time a target is held.</summary>
    public float MaxCommitSeconds { get; init; } = 7f;

    public void Validate(string file)
    {
        DataLoadException.Require(
            SwitchCloserShare is >= 0 and < 1, file, "enemyAggro.switchCloserShare", "must be at least 0 and below 1");
        DataLoadException.Require(WeakerShare is >= 0 and <= 1, file, "enemyAggro.weakerShare", "must be between 0 and 1");
        DataLoadException.Require(GrudgeSeconds >= 0, file, "enemyAggro.grudgeSeconds", "must not be negative");
        DataLoadException.Require(MinCommitSeconds > 0, file, "enemyAggro.minCommitSeconds", "must be greater than 0");
        DataLoadException.Require(
            MaxCommitSeconds >= MinCommitSeconds, file, "enemyAggro.maxCommitSeconds", "must not be below minCommitSeconds");
    }
}
