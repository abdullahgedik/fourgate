namespace Fourgate.Core.Data;

/// <summary>A ranged enemy's attack: when it stops to shoot, how long it warns, and what it throws.</summary>
public sealed class EnemyAttackConfig
{
    public required EnemyAttackType Type { get; init; }

    /// <summary>Damage of one fireball.</summary>
    public required float Damage { get; init; }

    /// <summary>The enemy stops and attacks when its target is this close and in clear sight.</summary>
    public required float Range { get; init; }

    /// <summary>Wind-up before the throw, shown as an aim line; never skipped.</summary>
    public required float TelegraphSeconds { get; init; }

    /// <summary>Wait after a throw before the next wind-up can start.</summary>
    public required float CooldownSeconds { get; init; }

    /// <summary>World units per second a fireball flies.</summary>
    public required float ProjectileSpeed { get; init; }

    /// <summary>Size of a fireball.</summary>
    public required float ProjectileRadius { get; init; }

    /// <summary>How far a fireball flies before it fizzles out.</summary>
    public required float ProjectileRange { get; init; }

    public void Validate(string file, string path)
    {
        DataLoadException.Require(Damage > 0, file, $"{path}.damage", "must be greater than 0");
        DataLoadException.Require(Range > 0, file, $"{path}.range", "must be greater than 0");
        DataLoadException.Require(
            TelegraphSeconds > 0, file, $"{path}.telegraphSeconds", "must be greater than 0 (telegraphs are mandatory)");
        DataLoadException.Require(CooldownSeconds >= 0, file, $"{path}.cooldownSeconds", "must not be negative");
        DataLoadException.Require(ProjectileSpeed > 0, file, $"{path}.projectileSpeed", "must be greater than 0");
        DataLoadException.Require(ProjectileRadius > 0, file, $"{path}.projectileRadius", "must be greater than 0");
        DataLoadException.Require(
            ProjectileRange >= Range, file, $"{path}.projectileRange", "must reach at least as far as range");
    }
}
