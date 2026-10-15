namespace Fourgate.Core.Data;

/// <summary>Fast mode: things unlock sooner and more enemies come, faster.</summary>
public sealed class FastModeConfig
{
    /// <summary>Weapon and enemy unlock waves are multiplied by this (rounded up, at least wave 1).</summary>
    public required float UnlockWaveMultiplier { get; init; }

    /// <summary>Each wave's enemy budget is multiplied by this.</summary>
    public required float BudgetMultiplier { get; init; }

    /// <summary>Time between spawn groups is multiplied by this.</summary>
    public required float SpawnIntervalMultiplier { get; init; }

    public void Validate(string file)
    {
        DataLoadException.Require(
            UnlockWaveMultiplier > 0 && UnlockWaveMultiplier <= 1, file, "fast.unlockWaveMultiplier",
            "must be above 0 and at most 1");
        DataLoadException.Require(BudgetMultiplier > 0, file, "fast.budgetMultiplier", "must be greater than 0");
        DataLoadException.Require(
            SpawnIntervalMultiplier > 0, file, "fast.spawnIntervalMultiplier", "must be greater than 0");
    }
}
