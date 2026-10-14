namespace Fourgate.Core.Data;

/// <summary>Enemies a boss bursts into when it dies (the Swarm Mother splitting into her brood).</summary>
public sealed class BossDeathSpawnConfig
{
    /// <summary>Id of the enemy in <c>enemies.json</c>.</summary>
    public required string EnemyId { get; init; }

    public required int Count { get; init; }

    /// <summary>How far from where the boss died they appear.</summary>
    public required float SpawnRadius { get; init; }

    public void Validate(string file, string path)
    {
        DataLoadException.Require(!string.IsNullOrWhiteSpace(EnemyId), file, $"{path}.enemyId", "must not be empty");
        DataLoadException.Require(Count >= 1, file, $"{path}.count", "must be 1 or greater");
        DataLoadException.Require(SpawnRadius >= 0, file, $"{path}.spawnRadius", "must not be negative");
    }
}
