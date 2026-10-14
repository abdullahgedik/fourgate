using System.Collections.Generic;

namespace Fourgate.Core.Data;

/// <summary>One weapon entry from <c>weapons.json</c>.</summary>
public sealed class WeaponConfig
{
    /// <summary>Highest weapon level; reaching it grants the weapon's special abilities.</summary>
    public const int MaxLevel = 10;

    public required string Id { get; init; }

    public required string DisplayName { get; init; }

    /// <summary>Weapon slot number; slot order equals unlock order.</summary>
    public required int Slot { get; init; }

    public required int UnlockWave { get; init; }

    public required WeaponType Type { get; init; }

    public required float Damage { get; init; }

    /// <summary>Bullets fired by each shot, fanned across the spread cone; each deals the full damage.</summary>
    public int Pellets { get; init; } = 1;

    /// <summary>Shots per second.</summary>
    public required float FireRate { get; init; }

    /// <summary>True when holding fire keeps shooting; false needs one press per shot.</summary>
    public required bool Automatic { get; init; }

    /// <summary>Full width of the cone shots scatter in, in degrees; 0 fires dead straight.</summary>
    public float SpreadDegrees { get; init; }

    public required WeaponAmmoConfig Ammo { get; init; }

    /// <summary>Bullet values; required for <see cref="WeaponType.Projectile"/> weapons.</summary>
    public ProjectileConfig? Projectile { get; init; }

    /// <summary>Flight values; required for <see cref="WeaponType.Thrown"/> weapons.</summary>
    public ThrownConfig? Thrown { get; init; }

    /// <summary>Blast values; required for <see cref="WeaponType.Thrown"/> weapons.</summary>
    public ExplosionConfig? Explosion { get; init; }

    /// <summary>Bonuses of levels 2 to 9. A level without an entry changes nothing.</summary>
    public List<WeaponLevelConfig> Levels { get; init; } = new();

    /// <summary>What level 10 grants; null for a weapon without a special ability.</summary>
    public WeaponLevelTenConfig? LevelTen { get; init; }

    /// <summary>Checks value ranges; <paramref name="path"/> locates this entry inside the file.</summary>
    public void Validate(string file, string path)
    {
        DataLoadException.Require(!string.IsNullOrWhiteSpace(Id), file, $"{path}.id", "must not be empty");
        DataLoadException.Require(
            !string.IsNullOrWhiteSpace(DisplayName), file, $"{path}.displayName", "must not be empty");
        DataLoadException.Require(Slot >= 1, file, $"{path}.slot", "must be 1 or greater");
        DataLoadException.Require(UnlockWave >= 1, file, $"{path}.unlockWave", "must be 1 or greater");
        DataLoadException.Require(Damage > 0, file, $"{path}.damage", "must be greater than 0");
        DataLoadException.Require(Pellets >= 1, file, $"{path}.pellets", "must be 1 or greater");
        DataLoadException.Require(FireRate > 0, file, $"{path}.fireRate", "must be greater than 0");
        DataLoadException.Require(
            SpreadDegrees >= 0 && SpreadDegrees <= 180, file, $"{path}.spreadDegrees", "must be between 0 and 180");

        DataLoadException.Require(Ammo is not null, file, $"{path}.ammo", "is required");
        var ammo = Ammo!;
        DataLoadException.Require(
            !ammo.UsesMagazine || ammo.MagSize >= 1, file, $"{path}.ammo.magSize", "must be 1 or greater");
        DataLoadException.Require(ammo.ReserveMax >= 0, file, $"{path}.ammo.reserveMax", "must not be negative");
        DataLoadException.Require(ammo.ReloadTime >= 0, file, $"{path}.ammo.reloadTime", "must not be negative");

        DataLoadException.Require(Levels is not null, file, $"{path}.levels", "must not be null");
        var seenLevels = new HashSet<int>();
        for (var i = 0; i < Levels!.Count; i++)
        {
            var levelPath = $"{path}.levels[{i}]";
            DataLoadException.Require(Levels[i] is not null, file, levelPath, "must not be null");
            Levels[i].Validate(file, levelPath);
            DataLoadException.Require(
                seenLevels.Add(Levels[i].Level), file, $"{levelPath}.level", $"level {Levels[i].Level} is listed twice");
        }

        LevelTen?.Validate(file, $"{path}.levelTen");

        if (Type == WeaponType.Projectile)
        {
            ValidateProjectile(file, path);
        }
        else
        {
            ValidateThrown(file, path);
        }
    }

    private void ValidateProjectile(string file, string path)
    {
        DataLoadException.Require(Projectile is not null, file, $"{path}.projectile", "is required for this weapon type");
        var projectile = Projectile!;
        DataLoadException.Require(projectile.Speed > 0, file, $"{path}.projectile.speed", "must be greater than 0");
        DataLoadException.Require(projectile.Radius > 0, file, $"{path}.projectile.radius", "must be greater than 0");
        DataLoadException.Require(projectile.Range > 0, file, $"{path}.projectile.range", "must be greater than 0");
    }

    private void ValidateThrown(string file, string path)
    {
        DataLoadException.Require(Thrown is not null, file, $"{path}.thrown", "is required for this weapon type");
        var thrown = Thrown!;
        DataLoadException.Require(thrown.Speed > 0, file, $"{path}.thrown.speed", "must be greater than 0");
        DataLoadException.Require(thrown.MaxRange > 0, file, $"{path}.thrown.maxRange", "must be greater than 0");
        DataLoadException.Require(thrown.Radius > 0, file, $"{path}.thrown.radius", "must be greater than 0");

        DataLoadException.Require(Explosion is not null, file, $"{path}.explosion", "is required for this weapon type");
        DataLoadException.Require(
            Explosion!.Radius > 0, file, $"{path}.explosion.radius", "must be greater than 0");
    }
}
