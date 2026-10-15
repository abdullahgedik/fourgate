namespace Fourgate.Core.Data;

/// <summary>Root object of <c>modes.json</c>: the rules of the Fast and Nightmare toggles.</summary>
public sealed class ModesConfig
{
    /// <summary>Modes that change nothing; used when no modes file is given (tests).</summary>
    public static readonly ModesConfig Neutral = new()
    {
        Fast = new FastModeConfig { UnlockWaveMultiplier = 1f, BudgetMultiplier = 1f, SpawnIntervalMultiplier = 1f },
        Nightmare = new NightmareModeConfig
        {
            DifficultyWaveOffset = 0, BudgetMultiplier = 1f, EnemySpeedMultiplier = 1f,
            HealthCrateChanceMultiplier = 1f, AllWeaponsAtStart = false,
        },
    };

    public required FastModeConfig Fast { get; init; }

    public required NightmareModeConfig Nightmare { get; init; }

    public void Validate(string file)
    {
        DataLoadException.Require(Fast is not null, file, "fast", "is required");
        DataLoadException.Require(Nightmare is not null, file, "nightmare", "is required");
        Fast!.Validate(file);
        Nightmare!.Validate(file);
    }
}
