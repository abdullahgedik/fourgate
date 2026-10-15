namespace Fourgate.Core.Data;

/// <summary>Nightmare mode: everything from the start and the waves of a much later stage.</summary>
public sealed class NightmareModeConfig
{
    /// <summary>
    /// Waves are built (budget, enemy mix, pacing) as if this many waves later; the wave counter
    /// and boss waves stay as they are.
    /// </summary>
    public required int DifficultyWaveOffset { get; init; }

    /// <summary>Each wave's enemy budget is multiplied by this.</summary>
    public required float BudgetMultiplier { get; init; }

    /// <summary>Zombies walk this much faster.</summary>
    public required float EnemySpeedMultiplier { get; init; }

    /// <summary>The chance that a dropped crate is a health crate is multiplied by this.</summary>
    public required float HealthCrateChanceMultiplier { get; init; }

    /// <summary>True when every weapon is unlocked and handed to every player at the start.</summary>
    public required bool AllWeaponsAtStart { get; init; }

    public void Validate(string file)
    {
        DataLoadException.Require(
            DifficultyWaveOffset >= 0, file, "nightmare.difficultyWaveOffset", "must not be negative");
        DataLoadException.Require(BudgetMultiplier > 0, file, "nightmare.budgetMultiplier", "must be greater than 0");
        DataLoadException.Require(
            EnemySpeedMultiplier > 0, file, "nightmare.enemySpeedMultiplier", "must be greater than 0");
        DataLoadException.Require(
            HealthCrateChanceMultiplier >= 0, file, "nightmare.healthCrateChanceMultiplier", "must not be negative");
    }
}
