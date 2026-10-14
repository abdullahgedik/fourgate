namespace Fourgate.Core.Data;

/// <summary>One attack a boss can use. Which fields matter depends on <see cref="Type"/>.</summary>
public sealed class BossPatternConfig
{
    // Design rule (section 8.1): area and line attacks warn for at least this long.
    private const float MinAreaTelegraphSeconds = 0.8f;

    public required BossPatternType Type { get; init; }

    /// <summary>Warning before it happens: a ground marker that fills up. Never skipped.</summary>
    public required float TelegraphSeconds { get; init; }

    /// <summary>Relative chance of picking this pattern among the ones usable right now.</summary>
    public float Weight { get; init; } = 1f;

    /// <summary>Damage to a player it hits (charge, ring).</summary>
    public float Damage { get; init; }

    /// <summary>Charge: how far it rushes.</summary>
    public float Distance { get; init; }

    /// <summary>Charge: world units per second while rushing.</summary>
    public float Speed { get; init; }

    /// <summary>Ring: how far the shockwave spreads; also how close a player must be for the boss to use it.</summary>
    public float Radius { get; init; }

    /// <summary>Ring: world units per second the shockwave spreads.</summary>
    public float RingSpeed { get; init; }

    /// <summary>Ring: thickness of the shockwave band that hurts.</summary>
    public float RingWidth { get; init; }

    /// <summary>Summon: id of the enemy in <c>enemies.json</c> to call in.</summary>
    public string EnemyId { get; init; } = string.Empty;

    /// <summary>Summon: how many.</summary>
    public int Count { get; init; }

    /// <summary>Summon: how far from the boss they appear.</summary>
    public float SpawnRadius { get; init; }

    public void Validate(string file, string path)
    {
        DataLoadException.Require(Weight > 0, file, $"{path}.weight", "must be greater than 0");
        switch (Type)
        {
            case BossPatternType.LineCharge:
                RequireAreaTelegraph(file, path);
                DataLoadException.Require(Damage > 0, file, $"{path}.damage", "must be greater than 0");
                DataLoadException.Require(Distance > 0, file, $"{path}.distance", "must be greater than 0");
                DataLoadException.Require(Speed > 0, file, $"{path}.speed", "must be greater than 0");
                break;

            case BossPatternType.GroundSlamRing:
                RequireAreaTelegraph(file, path);
                DataLoadException.Require(Damage > 0, file, $"{path}.damage", "must be greater than 0");
                DataLoadException.Require(Radius > 0, file, $"{path}.radius", "must be greater than 0");
                DataLoadException.Require(RingSpeed > 0, file, $"{path}.ringSpeed", "must be greater than 0");
                DataLoadException.Require(RingWidth > 0, file, $"{path}.ringWidth", "must be greater than 0");
                break;

            case BossPatternType.Summon:
                DataLoadException.Require(TelegraphSeconds > 0, file, $"{path}.telegraphSeconds", "must be greater than 0");
                DataLoadException.Require(!string.IsNullOrWhiteSpace(EnemyId), file, $"{path}.enemyId", "must not be empty");
                DataLoadException.Require(Count >= 1, file, $"{path}.count", "must be 1 or greater");
                DataLoadException.Require(SpawnRadius > 0, file, $"{path}.spawnRadius", "must be greater than 0");
                break;
        }
    }

    private void RequireAreaTelegraph(string file, string path)
    {
        DataLoadException.Require(
            TelegraphSeconds >= MinAreaTelegraphSeconds, file, $"{path}.telegraphSeconds",
            $"must be at least {MinAreaTelegraphSeconds} s for an area or line attack");
    }
}
