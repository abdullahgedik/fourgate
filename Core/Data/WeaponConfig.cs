namespace Fourgate.Core.Data;

/// <summary>One weapon entry from <c>weapons.json</c>.</summary>
public sealed class WeaponConfig
{
    public required string Id { get; init; }

    public required string DisplayName { get; init; }

    /// <summary>Weapon slot number; slot order equals unlock order.</summary>
    public required int Slot { get; init; }

    public required int UnlockWave { get; init; }

    public required WeaponType Type { get; init; }

    public required float Damage { get; init; }

    /// <summary>Shots per second.</summary>
    public required float FireRate { get; init; }

    /// <summary>True when holding fire keeps shooting; false needs one press per shot.</summary>
    public required bool Automatic { get; init; }

    public required WeaponAmmoConfig Ammo { get; init; }

    public required ProjectileConfig Projectile { get; init; }

    /// <summary>Checks value ranges; <paramref name="path"/> locates this entry inside the file.</summary>
    public void Validate(string file, string path)
    {
        DataLoadException.Require(!string.IsNullOrWhiteSpace(Id), file, $"{path}.id", "must not be empty");
        DataLoadException.Require(
            !string.IsNullOrWhiteSpace(DisplayName), file, $"{path}.displayName", "must not be empty");
        DataLoadException.Require(Slot >= 1, file, $"{path}.slot", "must be 1 or greater");
        DataLoadException.Require(UnlockWave >= 1, file, $"{path}.unlockWave", "must be 1 or greater");
        DataLoadException.Require(Damage > 0, file, $"{path}.damage", "must be greater than 0");
        DataLoadException.Require(FireRate > 0, file, $"{path}.fireRate", "must be greater than 0");

        DataLoadException.Require(Ammo is not null, file, $"{path}.ammo", "is required");
        var ammo = Ammo!;
        DataLoadException.Require(ammo.MagSize >= 1, file, $"{path}.ammo.magSize", "must be 1 or greater");
        DataLoadException.Require(ammo.ReserveMax >= 0, file, $"{path}.ammo.reserveMax", "must not be negative");
        DataLoadException.Require(ammo.ReloadTime >= 0, file, $"{path}.ammo.reloadTime", "must not be negative");

        DataLoadException.Require(Projectile is not null, file, $"{path}.projectile", "is required");
        var projectile = Projectile!;
        DataLoadException.Require(projectile.Speed > 0, file, $"{path}.projectile.speed", "must be greater than 0");
        DataLoadException.Require(projectile.Radius > 0, file, $"{path}.projectile.radius", "must be greater than 0");
        DataLoadException.Require(projectile.Range > 0, file, $"{path}.projectile.range", "must be greater than 0");
    }
}
