using System.Collections.Generic;

namespace Fourgate.Core.Data;

/// <summary>One weapon entry from <c>weapons.json</c>.</summary>
public sealed class WeaponConfig
{
    /// <summary>Highest weapon level; reaching it grants the weapon's special abilities.</summary>
    public const int MaxLevel = 10;

    /// <summary>Guns are selected with the number keys 1 to 9.</summary>
    public const int MaxGunSlot = 9;

    public required string Id { get; init; }

    public required string DisplayName { get; init; }

    /// <summary>Gun or equipment; guns are the default.</summary>
    public WeaponCategory Category { get; init; } = WeaponCategory.Gun;

    /// <summary>
    /// For a gun, the number key that selects it (1–9). For equipment, its place in the
    /// equipment cycle.
    /// </summary>
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

    /// <summary>Blast values of a thrown weapon that explodes.</summary>
    public ExplosionConfig? Explosion { get; init; }

    /// <summary>Fire pool of a thrown weapon that burns; its damage is then per second.</summary>
    public FireConfig? Fire { get; init; }

    /// <summary>Trigger values; required for <see cref="WeaponType.Placed"/> weapons.</summary>
    public PlacedConfig? Placed { get; init; }

    /// <summary>Flame values; required for <see cref="WeaponType.Stream"/> weapons.</summary>
    public StreamConfig? Stream { get; init; }

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
        DataLoadException.Require(
            Slot >= 1 && (Category != WeaponCategory.Gun || Slot <= MaxGunSlot),
            file, $"{path}.slot", $"must be 1 or greater, and at most {MaxGunSlot} for a gun");
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

        switch (Type)
        {
            case WeaponType.Projectile:
                ValidateProjectile(file, path);
                break;

            case WeaponType.Thrown:
                ValidateThrown(file, path);
                break;

            case WeaponType.Placed:
                ValidatePlaced(file, path);
                break;

            case WeaponType.Stream:
                ValidateStream(file, path);
                break;
        }
    }

    private void ValidateStream(string file, string path)
    {
        DataLoadException.Require(Stream is not null, file, $"{path}.stream", "is required for this weapon type");
        var stream = Stream!;
        DataLoadException.Require(stream.Speed > 0, file, $"{path}.stream.speed", "must be greater than 0");
        DataLoadException.Require(stream.Range > 0, file, $"{path}.stream.range", "must be greater than 0");
        DataLoadException.Require(stream.RadiusStart > 0, file, $"{path}.stream.radiusStart", "must be greater than 0");
        DataLoadException.Require(stream.RadiusEnd > 0, file, $"{path}.stream.radiusEnd", "must be greater than 0");
    }

    private void ValidatePlaced(string file, string path)
    {
        DataLoadException.Require(Placed is not null, file, $"{path}.placed", "is required for this weapon type");
        var placed = Placed!;
        DataLoadException.Require(
            placed.TriggerRadius > 0, file, $"{path}.placed.triggerRadius", "must be greater than 0");
        DataLoadException.Require(placed.ArmSeconds >= 0, file, $"{path}.placed.armSeconds", "must not be negative");
        DataLoadException.Require(
            placed.MaxPlaced >= 0, file, $"{path}.placed.maxPlaced", "must not be negative (0 means no limit)");

        DataLoadException.Require(Explosion is not null, file, $"{path}.explosion", "is required for this weapon type");
        DataLoadException.Require(
            Explosion!.Radius > 0, file, $"{path}.explosion.radius", "must be greater than 0");
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

        DataLoadException.Require(
            Explosion is not null || Fire is not null,
            file, $"{path}.explosion", "a thrown weapon needs an explosion block, a fire block, or both");
        if (Explosion is not null)
        {
            DataLoadException.Require(
                Explosion.Radius > 0, file, $"{path}.explosion.radius", "must be greater than 0");
        }

        if (Fire is not null)
        {
            DataLoadException.Require(Fire.Radius > 0, file, $"{path}.fire.radius", "must be greater than 0");
            DataLoadException.Require(Fire.Seconds > 0, file, $"{path}.fire.seconds", "must be greater than 0");
        }
    }
}
