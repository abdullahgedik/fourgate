using System.Collections.Generic;

namespace Fourgate.Core.Data;

/// <summary>What weapon level 10 grants: special abilities and a last stat bump.</summary>
public sealed class WeaponLevelTenConfig
{
    /// <summary>Short lines shown on the upgrade card.</summary>
    public required List<string> Description { get; init; }

    public required List<WeaponAbilityConfig> Abilities { get; init; }

    public float DamageMult { get; init; } = 1f;

    public float FireRateMult { get; init; } = 1f;

    /// <summary>Extra rounds in the magazine.</summary>
    public int MagSizeBonus { get; init; }

    /// <summary>Lengthens how long a fire pool burns.</summary>
    public float DurationMult { get; init; } = 1f;

    /// <summary>Checks value ranges; <paramref name="path"/> locates this entry inside the file.</summary>
    public void Validate(string file, string path)
    {
        DataLoadException.Require(
            Description is { Count: > 0 }, file, $"{path}.description", "must contain at least one line");
        DataLoadException.Require(Abilities is not null, file, $"{path}.abilities", "is required");
        DataLoadException.Require(DamageMult > 0, file, $"{path}.damageMult", "must be greater than 0");
        DataLoadException.Require(FireRateMult > 0, file, $"{path}.fireRateMult", "must be greater than 0");
        DataLoadException.Require(MagSizeBonus >= 0, file, $"{path}.magSizeBonus", "must not be negative");
        DataLoadException.Require(DurationMult > 0, file, $"{path}.durationMult", "must be greater than 0");

        for (var i = 0; i < Abilities!.Count; i++)
        {
            DataLoadException.Require(Abilities[i] is not null, file, $"{path}.abilities[{i}]", "must not be null");
            DataLoadException.Require(
                Abilities[i].Count >= 1, file, $"{path}.abilities[{i}].count", "must be 1 or greater");
            DataLoadException.Require(
                Abilities[i].Radius >= 0, file, $"{path}.abilities[{i}].radius", "must not be negative");
            DataLoadException.Require(
                Abilities[i].DamageMult > 0, file, $"{path}.abilities[{i}].damageMult", "must be greater than 0");
            DataLoadException.Require(
                Abilities[i].RadiusMult > 0, file, $"{path}.abilities[{i}].radiusMult", "must be greater than 0");
            DataLoadException.Require(
                Abilities[i].FireRateMult > 0, file, $"{path}.abilities[{i}].fireRateMult", "must be greater than 0");
            DataLoadException.Require(
                Abilities[i].Seconds >= 0, file, $"{path}.abilities[{i}].seconds", "must not be negative");
        }
    }
}
