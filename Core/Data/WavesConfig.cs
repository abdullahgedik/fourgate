namespace Fourgate.Core.Data;

/// <summary>Wave pacing loaded from <c>waves.json</c>.</summary>
public sealed class WavesConfig
{
    public required WaveBudgetConfig Budget { get; init; }

    /// <summary>Extra pick weight per step of "newness" when choosing which enemy type to spawn; 0 picks evenly.</summary>
    public required float NewerTypeBias { get; init; }

    /// <summary>Seconds between spawn groups in wave 1.</summary>
    public required float SpawnIntervalStart { get; init; }

    /// <summary>Seconds the interval shrinks by with each further wave.</summary>
    public required float SpawnIntervalStepPerWave { get; init; }

    /// <summary>Shortest interval the shrinking can reach.</summary>
    public required float SpawnIntervalMin { get; init; }

    /// <summary>Enemies per spawn group in wave 1.</summary>
    public required float GroupSizeBase { get; init; }

    /// <summary>Growth of the group size with each further wave.</summary>
    public required float GroupSizePerWave { get; init; }

    public required int GroupSizeMax { get; init; }

    /// <summary>Seconds a door glows before its group emerges.</summary>
    public required float DoorWarningSeconds { get; init; }

    /// <summary>Checks value ranges; throws <see cref="DataLoadException"/> naming the bad field.</summary>
    public void Validate(string file)
    {
        DataLoadException.Require(Budget is not null, file, "budget", "is required");
        var budget = Budget!;
        DataLoadException.Require(budget.Base >= 0, file, "budget.base", "must not be negative");
        DataLoadException.Require(budget.PerWave >= 0, file, "budget.perWave", "must not be negative");
        DataLoadException.Require(budget.Quadratic >= 0, file, "budget.quadratic", "must not be negative");

        DataLoadException.Require(NewerTypeBias >= 0, file, "newerTypeBias", "must not be negative");
        DataLoadException.Require(SpawnIntervalMin > 0, file, "spawnIntervalMin", "must be greater than 0");
        DataLoadException.Require(
            SpawnIntervalStart >= SpawnIntervalMin, file, "spawnIntervalStart", "must not be below spawnIntervalMin");
        DataLoadException.Require(
            SpawnIntervalStepPerWave >= 0, file, "spawnIntervalStepPerWave", "must not be negative");
        DataLoadException.Require(GroupSizeBase >= 1, file, "groupSizeBase", "must be 1 or greater");
        DataLoadException.Require(GroupSizePerWave >= 0, file, "groupSizePerWave", "must not be negative");
        DataLoadException.Require(GroupSizeMax >= 1, file, "groupSizeMax", "must be 1 or greater");
        DataLoadException.Require(DoorWarningSeconds >= 0, file, "doorWarningSeconds", "must not be negative");
    }
}
