namespace Fourgate.Core.Data;

/// <summary>One attack a boss can use. Which fields matter depends on <see cref="Type"/>.</summary>
public sealed class BossPatternConfig
{
    // Design rule (section 8.1): area and line attacks warn for at least this long, projectile
    // attacks at least the shorter time.
    private const float MinAreaTelegraphSeconds = 0.8f;
    private const float MinProjectileTelegraphSeconds = 0.6f;

    public required BossPatternType Type { get; init; }

    /// <summary>Whom it is aimed at: the boss's current target, or the player farthest away.</summary>
    public BossTargetChoice Target { get; init; } = BossTargetChoice.Current;

    /// <summary>Warning before it happens: a ground marker that fills up. Never skipped.</summary>
    public required float TelegraphSeconds { get; init; }

    /// <summary>Relative chance of picking this pattern among the ones usable right now.</summary>
    public float Weight { get; init; } = 1f;

    /// <summary>Damage to a player it hits (charge, ring, fireballs); per second for a fire stream.</summary>
    public float Damage { get; init; }

    /// <summary>Charge: how far it rushes. Fire stream: how far the fire reaches.</summary>
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

    /// <summary>Ring: bursts thrown, each turned half a gap from the one before. Volley: bursts thrown.</summary>
    public int Bursts { get; init; } = 1;

    /// <summary>Ring: seconds between bursts. Spiral: seconds between throws.</summary>
    public float Interval { get; init; }

    /// <summary>Spiral: how long it keeps throwing. Fire stream: how long it breathes fire. Haste: how long it lasts.</summary>
    public float Seconds { get; init; }

    /// <summary>Spiral, fire stream: how fast it turns, in degrees per second (negative turns the other way).</summary>
    public float SpinDegreesPerSecond { get; init; }

    /// <summary>Haste: how much faster it walks while it lasts.</summary>
    public float SpeedMultiplier { get; init; } = 1f;

    /// <summary>Volley: angle (degrees) to each side of the aim the outer fireballs fly.</summary>
    public float SpreadDegrees { get; init; }

    /// <summary>
    /// A pattern carried out right after this one (e.g. the attack that goes with a summoned
    /// brood); it does not wait for a good spot. Null for none.
    /// </summary>
    public BossPatternConfig? FollowUp { get; init; }

    /// <summary>Fire stream: thickness of the fire.</summary>
    public float Width { get; init; }

    /// <summary>Ring, spiral: world units per second a fireball flies.</summary>
    public float ProjectileSpeed { get; init; }

    /// <summary>Ring, spiral: size of a fireball.</summary>
    public float ProjectileRadius { get; init; }

    /// <summary>Ring, spiral: how far a fireball flies; also how close a player must be for the boss to use it.</summary>
    public float ProjectileRange { get; init; }

    public void Validate(string file, string path)
    {
        DataLoadException.Require(Weight > 0, file, $"{path}.weight", "must be greater than 0");
        ValidateFollowUp(file, path);
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

            case BossPatternType.FireballRing:
                RequireProjectileTelegraph(file, path);
                RequireFireballs(file, path);
                DataLoadException.Require(Count >= 1, file, $"{path}.count", "must be 1 or greater");
                DataLoadException.Require(Bursts >= 1, file, $"{path}.bursts", "must be 1 or greater");
                DataLoadException.Require(Interval >= 0, file, $"{path}.interval", "must not be negative");
                break;

            case BossPatternType.FireballSpiral:
                RequireProjectileTelegraph(file, path);
                RequireFireballs(file, path);
                DataLoadException.Require(Count >= 1, file, $"{path}.count", "must be 1 or greater (arms)");
                DataLoadException.Require(Seconds > 0, file, $"{path}.seconds", "must be greater than 0");
                DataLoadException.Require(Interval > 0, file, $"{path}.interval", "must be greater than 0");
                break;

            case BossPatternType.Teleport:
                DataLoadException.Require(TelegraphSeconds > 0, file, $"{path}.telegraphSeconds", "must be greater than 0");
                break;

            case BossPatternType.Haste:
                DataLoadException.Require(TelegraphSeconds > 0, file, $"{path}.telegraphSeconds", "must be greater than 0");
                DataLoadException.Require(SpeedMultiplier > 0, file, $"{path}.speedMultiplier", "must be greater than 0");
                DataLoadException.Require(Seconds > 0, file, $"{path}.seconds", "must be greater than 0");
                break;

            case BossPatternType.FireballVolley:
                RequireProjectileTelegraph(file, path);
                RequireFireballs(file, path);
                DataLoadException.Require(Count >= 1, file, $"{path}.count", "must be 1 or greater");
                DataLoadException.Require(Bursts >= 1, file, $"{path}.bursts", "must be 1 or greater");
                DataLoadException.Require(Interval >= 0, file, $"{path}.interval", "must not be negative");
                DataLoadException.Require(
                    SpreadDegrees >= 0 && SpreadDegrees < 90, file, $"{path}.spreadDegrees", "must be from 0 up to 90");
                break;

            case BossPatternType.LeapSlam:
                RequireAreaTelegraph(file, path);
                DataLoadException.Require(Damage > 0, file, $"{path}.damage", "must be greater than 0");
                DataLoadException.Require(Radius > 0, file, $"{path}.radius", "must be greater than 0 (landing area)");
                DataLoadException.Require(Distance > 0, file, $"{path}.distance", "must be greater than 0 (longest leap)");
                DataLoadException.Require(Seconds > 0, file, $"{path}.seconds", "must be greater than 0 (time in the air)");
                break;

            case BossPatternType.MissileRain:
                RequireAreaTelegraph(file, path);
                DataLoadException.Require(Damage > 0, file, $"{path}.damage", "must be greater than 0");
                DataLoadException.Require(Radius > 0, file, $"{path}.radius", "must be greater than 0 (blast of one missile)");
                DataLoadException.Require(Count >= 1, file, $"{path}.count", "must be 1 or greater");
                DataLoadException.Require(SpawnRadius >= 0, file, $"{path}.spawnRadius", "must not be negative (scatter)");
                DataLoadException.Require(Seconds > 0, file, $"{path}.seconds", "must be greater than 0 (fall time)");
                break;

            case BossPatternType.FireballStream:
                RequireProjectileTelegraph(file, path);
                RequireFireballs(file, path);
                DataLoadException.Require(Seconds > 0, file, $"{path}.seconds", "must be greater than 0");
                DataLoadException.Require(Interval > 0, file, $"{path}.interval", "must be greater than 0");
                break;

            case BossPatternType.LaserSweep:
            case BossPatternType.FireStream:
                RequireAreaTelegraph(file, path);
                DataLoadException.Require(Damage > 0, file, $"{path}.damage", "must be greater than 0 (per second)");
                DataLoadException.Require(Distance > 0, file, $"{path}.distance", "must be greater than 0");
                DataLoadException.Require(Width > 0, file, $"{path}.width", "must be greater than 0");
                DataLoadException.Require(Seconds > 0, file, $"{path}.seconds", "must be greater than 0");
                break;
        }
    }

    // Called at the end of Validate for every pattern.
    private void ValidateFollowUp(string file, string path)
    {
        FollowUp?.Validate(file, $"{path}.followUp");
    }

    private void RequireProjectileTelegraph(string file, string path)
    {
        DataLoadException.Require(
            TelegraphSeconds >= MinProjectileTelegraphSeconds, file, $"{path}.telegraphSeconds",
            $"must be at least {MinProjectileTelegraphSeconds} s for a projectile attack");
    }

    private void RequireFireballs(string file, string path)
    {
        DataLoadException.Require(Damage > 0, file, $"{path}.damage", "must be greater than 0");
        DataLoadException.Require(ProjectileSpeed > 0, file, $"{path}.projectileSpeed", "must be greater than 0");
        DataLoadException.Require(ProjectileRadius > 0, file, $"{path}.projectileRadius", "must be greater than 0");
        DataLoadException.Require(ProjectileRange > 0, file, $"{path}.projectileRange", "must be greater than 0");
    }

    private void RequireAreaTelegraph(string file, string path)
    {
        DataLoadException.Require(
            TelegraphSeconds >= MinAreaTelegraphSeconds, file, $"{path}.telegraphSeconds",
            $"must be at least {MinAreaTelegraphSeconds} s for an area or line attack");
    }
}
