using System.Collections.Generic;
using Fourgate.Core.Rendering;

namespace Fourgate.Core.Data;

/// <summary>One boss from <c>bosses.json</c>: its body, the wave it leads and the patterns it attacks with.</summary>
public sealed class BossConfig
{
    private EnemyConfig? _asEnemy;

    public required string Id { get; init; }

    public required string DisplayName { get; init; }

    /// <summary>The wave this boss appears in.</summary>
    public required int Wave { get; init; }

    public required float Hp { get; init; }

    /// <summary>World units per second while walking between attacks.</summary>
    public required float Speed { get; init; }

    public required float Radius { get; init; }

    public required float ContactDamage { get; init; }

    public required float ContactCooldown { get; init; }

    public required int Xp { get; init; }

    public required int ScoreValue { get; init; }

    /// <summary>Share of a normal wave's enemy budget spent on other enemies alongside the boss.</summary>
    public required float AddsBudgetFraction { get; init; }

    /// <summary>Seconds it walks after an attack before it can start the next one.</summary>
    public required float IdleSeconds { get; init; }

    public float KnockbackResist { get; init; }

    /// <summary>How fast it turns to face its target, in degrees per second; 0 turns at once.</summary>
    public float TurnDegreesPerSecond { get; init; }

    /// <summary>Width (degrees) of its armored front arc; 0 for no armor.</summary>
    public float FrontArmorArcDegrees { get; init; }

    /// <summary>Share of bullet and beam damage that gets through the front armor (0–1).</summary>
    public float FrontArmorDamageMult { get; init; } = 1f;

    /// <summary>Space between the body and the shield band in front of it.</summary>
    public float FrontArmorGap { get; init; }

    /// <summary>Thickness of the shield band; shots crossing it inside the arc stop there.</summary>
    public float FrontArmorThickness { get; init; }

    public ShapeKind Shape { get; init; } = ShapeKind.Circle;

    public List<int> Color { get; init; } = new() { 200, 60, 60 };

    public required List<BossPatternConfig> Patterns { get; init; }

    /// <summary>Random (weighted) or the listed sequence.</summary>
    public BossPatternOrder PatternOrder { get; init; } = BossPatternOrder.Random;

    /// <summary>Enemies it bursts into when it dies; null for none.</summary>
    public BossDeathSpawnConfig? DeathSpawn { get; init; }

    /// <summary>
    /// The boss's body as an enemy, so every weapon, the spatial hash and the wave count treat
    /// it like any other enemy. <see cref="EnemyConfig.IsBoss"/> hands its movement to the boss system.
    /// </summary>
    public EnemyConfig AsEnemy => _asEnemy ??= new EnemyConfig
    {
        Id = Id,
        DisplayName = DisplayName,
        Hp = Hp,
        Speed = Speed,
        Radius = Radius,
        ContactDamage = ContactDamage,
        ContactCooldown = ContactCooldown,
        Xp = Xp,
        ScoreValue = ScoreValue,
        Cost = 1f,
        UnlockWave = Wave,
        DropChance = 0f,
        KnockbackResist = KnockbackResist,
        FrontArmorArcDegrees = FrontArmorArcDegrees,
        FrontArmorDamageMult = FrontArmorDamageMult,
        FrontArmorGap = FrontArmorGap,
        FrontArmorThickness = FrontArmorThickness,
        Shape = Shape,
        Color = Color,
        IsBoss = true,
    };

    public void Validate(string file, string path)
    {
        DataLoadException.Require(!string.IsNullOrWhiteSpace(Id), file, $"{path}.id", "must not be empty");
        DataLoadException.Require(!string.IsNullOrWhiteSpace(DisplayName), file, $"{path}.displayName", "must not be empty");
        DataLoadException.Require(Wave >= 1, file, $"{path}.wave", "must be 1 or greater");
        DataLoadException.Require(Hp > 0, file, $"{path}.hp", "must be greater than 0");
        DataLoadException.Require(Speed >= 0, file, $"{path}.speed", "must not be negative");
        DataLoadException.Require(Radius > 0, file, $"{path}.radius", "must be greater than 0");
        DataLoadException.Require(ContactDamage >= 0, file, $"{path}.contactDamage", "must not be negative");
        DataLoadException.Require(ContactCooldown >= 0, file, $"{path}.contactCooldown", "must not be negative");
        DataLoadException.Require(Xp >= 0, file, $"{path}.xp", "must not be negative");
        DataLoadException.Require(ScoreValue >= 0, file, $"{path}.scoreValue", "must not be negative");
        DataLoadException.Require(
            AddsBudgetFraction >= 0 && AddsBudgetFraction <= 1, file, $"{path}.addsBudgetFraction", "must be between 0 and 1");
        DataLoadException.Require(IdleSeconds >= 0, file, $"{path}.idleSeconds", "must not be negative");
        DataLoadException.Require(
            KnockbackResist >= 0 && KnockbackResist <= 1, file, $"{path}.knockbackResist", "must be between 0 and 1");
        DataLoadException.Require(
            TurnDegreesPerSecond >= 0, file, $"{path}.turnDegreesPerSecond", "must not be negative");
        DataLoadException.Require(
            FrontArmorArcDegrees >= 0 && FrontArmorArcDegrees <= 360, file, $"{path}.frontArmorArcDegrees",
            "must be between 0 and 360");
        DataLoadException.Require(
            FrontArmorDamageMult >= 0 && FrontArmorDamageMult <= 1, file, $"{path}.frontArmorDamageMult",
            "must be between 0 and 1");
        DataLoadException.Require(FrontArmorGap >= 0, file, $"{path}.frontArmorGap", "must not be negative");
        DataLoadException.Require(FrontArmorThickness >= 0, file, $"{path}.frontArmorThickness", "must not be negative");
        DataLoadException.Require(
            Color is not null && Color.Count == 3 && Color.TrueForAll(c => c >= 0 && c <= 255),
            file, $"{path}.color", "must be three numbers from 0 to 255 (red, green, blue)");
        DeathSpawn?.Validate(file, $"{path}.deathSpawn");
        DataLoadException.Require(Patterns is { Count: > 0 }, file, $"{path}.patterns", "must list at least one pattern");
        for (var i = 0; i < Patterns!.Count; i++)
        {
            DataLoadException.Require(Patterns[i] is not null, file, $"{path}.patterns[{i}]", "must not be null");
            Patterns[i].Validate(file, $"{path}.patterns[{i}]");
        }
    }
}
