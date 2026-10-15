using System;
using Fourgate.Core.Data;

namespace Fourgate.Systems;

/// <summary>The combined effect of the chosen toggles; Fast and Nightmare stack.</summary>
public sealed class ModeRules
{
    /// <summary>Normal mode: nothing changed.</summary>
    public static readonly ModeRules Normal = new(ModesConfig.Neutral, default);

    public ModeRules(ModesConfig config, RunMode mode)
    {
        Mode = mode;
        var fast = config.Fast;
        var nightmare = config.Nightmare;

        UnlockWaveMultiplier = mode.Fast ? fast.UnlockWaveMultiplier : 1f;
        SpawnIntervalMultiplier = mode.Fast ? fast.SpawnIntervalMultiplier : 1f;
        BudgetMultiplier = (mode.Fast ? fast.BudgetMultiplier : 1f) * (mode.Nightmare ? nightmare.BudgetMultiplier : 1f);
        DifficultyWaveOffset = mode.Nightmare ? nightmare.DifficultyWaveOffset : 0;
        EnemySpeedMultiplier = mode.Nightmare ? nightmare.EnemySpeedMultiplier : 1f;
        HealthCrateChanceMultiplier = mode.Nightmare ? nightmare.HealthCrateChanceMultiplier : 1f;
        AllWeaponsAtStart = mode.Nightmare && nightmare.AllWeaponsAtStart;
    }

    public RunMode Mode { get; }

    public float UnlockWaveMultiplier { get; }

    public float SpawnIntervalMultiplier { get; }

    public float BudgetMultiplier { get; }

    public int DifficultyWaveOffset { get; }

    public float EnemySpeedMultiplier { get; }

    public float HealthCrateChanceMultiplier { get; }

    public bool AllWeaponsAtStart { get; }

    /// <summary>The wave something unlocks at in this mode (rounded up, never before wave 1).</summary>
    public int UnlockWave(int unlockWave)
    {
        return Math.Max(1, (int)MathF.Ceiling(unlockWave * UnlockWaveMultiplier - 0.0001f));
    }
}
