using System.Collections.Generic;
using Fourgate.Core.Rendering;

namespace Fourgate.Core.Data;

/// <summary>One enemy entry from <c>enemies.json</c>.</summary>
public sealed class EnemyConfig
{
    public required string Id { get; init; }

    public required string DisplayName { get; init; }

    public required float Hp { get; init; }

    /// <summary>World units per second.</summary>
    public required float Speed { get; init; }

    public required float Radius { get; init; }

    public required float ContactDamage { get; init; }

    /// <summary>Seconds between contact hits from one enemy.</summary>
    public required float ContactCooldown { get; init; }

    public required int Xp { get; init; }

    public required int ScoreValue { get; init; }

    /// <summary>Spawn budget this enemy uses up.</summary>
    public required float Cost { get; init; }

    public required int UnlockWave { get; init; }

    /// <summary>Chance (0–1) to drop a crate when killed.</summary>
    public required float DropChance { get; init; }

    /// <summary>Placeholder body shape until sprites exist; a triangle points where it walks.</summary>
    public ShapeKind Shape { get; init; } = ShapeKind.Circle;

    /// <summary>Placeholder body color as red, green, blue (0–255).</summary>
    public List<int> Color { get; init; } = new() { 90, 190, 100 };

    /// <summary>Ranged attack; null for enemies that only hit by touch.</summary>
    public EnemyAttackConfig? Attack { get; init; }

    /// <summary>Share of knockback it ignores (0–1); 0 (the default) takes the full push.</summary>
    public float KnockbackResist { get; init; }

    /// <summary>Checks value ranges; <paramref name="path"/> locates this entry inside the file.</summary>
    public void Validate(string file, string path)
    {
        DataLoadException.Require(!string.IsNullOrWhiteSpace(Id), file, $"{path}.id", "must not be empty");
        DataLoadException.Require(
            !string.IsNullOrWhiteSpace(DisplayName), file, $"{path}.displayName", "must not be empty");
        DataLoadException.Require(Hp > 0, file, $"{path}.hp", "must be greater than 0");
        DataLoadException.Require(Speed >= 0, file, $"{path}.speed", "must not be negative");
        DataLoadException.Require(Radius > 0, file, $"{path}.radius", "must be greater than 0");
        DataLoadException.Require(ContactDamage >= 0, file, $"{path}.contactDamage", "must not be negative");
        DataLoadException.Require(ContactCooldown >= 0, file, $"{path}.contactCooldown", "must not be negative");
        DataLoadException.Require(Xp >= 0, file, $"{path}.xp", "must not be negative");
        DataLoadException.Require(ScoreValue >= 0, file, $"{path}.scoreValue", "must not be negative");
        DataLoadException.Require(Cost > 0, file, $"{path}.cost", "must be greater than 0");
        DataLoadException.Require(UnlockWave >= 1, file, $"{path}.unlockWave", "must be 1 or greater");
        DataLoadException.Require(
            DropChance >= 0 && DropChance <= 1, file, $"{path}.dropChance", "must be between 0 and 1");
        DataLoadException.Require(
            KnockbackResist >= 0 && KnockbackResist <= 1, file, $"{path}.knockbackResist", "must be between 0 and 1");
        DataLoadException.Require(
            Color is not null && Color.Count == 3 && Color.TrueForAll(c => c >= 0 && c <= 255),
            file, $"{path}.color", "must be three numbers from 0 to 255 (red, green, blue)");
        Attack?.Validate(file, $"{path}.attack");
    }
}
