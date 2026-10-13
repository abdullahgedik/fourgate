namespace Fourgate.Core.Data;

/// <summary>What one weapon level (2–9) adds. Unset values change nothing.</summary>
public sealed class WeaponLevelConfig
{
    /// <summary>The level that grants this bonus.</summary>
    public required int Level { get; init; }

    public float DamageMult { get; init; } = 1f;

    public float FireRateMult { get; init; } = 1f;

    /// <summary>Extra rounds in the magazine.</summary>
    public int MagSizeBonus { get; init; }

    /// <summary>Below 1 reloads faster.</summary>
    public float ReloadTimeMult { get; init; } = 1f;

    public float RangeMult { get; init; } = 1f;

    /// <summary>Below 1 tightens the spread.</summary>
    public float SpreadMult { get; init; } = 1f;

    /// <summary>Checks value ranges; <paramref name="path"/> locates this entry inside the file.</summary>
    public void Validate(string file, string path)
    {
        DataLoadException.Require(
            Level >= 2 && Level <= WeaponConfig.MaxLevel - 1,
            file, $"{path}.level", $"must be between 2 and {WeaponConfig.MaxLevel - 1}");
        DataLoadException.Require(DamageMult > 0, file, $"{path}.damageMult", "must be greater than 0");
        DataLoadException.Require(FireRateMult > 0, file, $"{path}.fireRateMult", "must be greater than 0");
        DataLoadException.Require(MagSizeBonus >= 0, file, $"{path}.magSizeBonus", "must not be negative");
        DataLoadException.Require(ReloadTimeMult > 0, file, $"{path}.reloadTimeMult", "must be greater than 0");
        DataLoadException.Require(RangeMult > 0, file, $"{path}.rangeMult", "must be greater than 0");
        DataLoadException.Require(SpreadMult >= 0, file, $"{path}.spreadMult", "must not be negative");
    }
}
