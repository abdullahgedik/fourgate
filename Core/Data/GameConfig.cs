namespace Fourgate.Core.Data;

/// <summary>Global rules loaded from <c>game.json</c>.</summary>
public sealed class GameConfig
{
    public required int VirtualWidth { get; init; }

    public required int VirtualHeight { get; init; }

    public required ArenaConfig Arena { get; init; }

    public required CameraConfig Camera { get; init; }

    public required PlayerConfig Player { get; init; }

    public required XpConfig Xp { get; init; }

    public required CratesConfig Crates { get; init; }

    public required FriendlyFireConfig FriendlyFire { get; init; }

    public DebugConfig Debug { get; init; } = new();

    /// <summary>How hits push enemies back; no knockback when left out.</summary>
    public KnockbackConfig Knockback { get; init; } = new();

    /// <summary>Most enemies allowed in the arena at once; further spawns wait at the doors.</summary>
    public required int MaxAliveEnemies { get; init; }

    /// <summary>Seconds of rest between waves, and before the first one.</summary>
    public required float RestCountdownSeconds { get; init; }

    /// <summary>When false, weapons have no magazine and fire straight from their ammo pool.</summary>
    public required bool ReloadsEnabled { get; init; }

    /// <summary>Scales every weapon's reload time.</summary>
    public required float GlobalReloadTimeMultiplier { get; init; }

    /// <summary>Checks value ranges; throws <see cref="DataLoadException"/> naming the bad field.</summary>
    public void Validate(string file)
    {
        DataLoadException.Require(VirtualWidth > 0, file, "virtualWidth", "must be greater than 0");
        DataLoadException.Require(VirtualHeight > 0, file, "virtualHeight", "must be greater than 0");

        DataLoadException.Require(MaxAliveEnemies >= 1, file, "maxAliveEnemies", "must be 1 or greater");
        DataLoadException.Require(RestCountdownSeconds >= 0, file, "restCountdownSeconds", "must not be negative");
        DataLoadException.Require(
            Debug is not null && Debug.StartWave >= 1, file, "debug.startWave", "must be 1 or greater");

        DataLoadException.Require(Xp is not null, file, "xp", "is required");
        DataLoadException.Require(Xp!.Base > 0, file, "xp.base", "must be greater than 0");
        DataLoadException.Require(Xp.Exponent >= 0, file, "xp.exponent", "must not be negative");

        DataLoadException.Require(Knockback is not null, file, "knockback", "must not be null");
        Knockback!.Validate(file);

        DataLoadException.Require(FriendlyFire is not null, file, "friendlyFire", "is required");
        FriendlyFire!.Validate(file);

        DataLoadException.Require(Crates is not null, file, "crates", "is required");
        Crates!.Validate(file);

        DataLoadException.Require(Arena is not null, file, "arena", "is required");
        var arena = Arena!;
        DataLoadException.Require(arena.Size > 0, file, "arena.size", "must be greater than 0");
        DataLoadException.Require(arena.WallThickness > 0, file, "arena.wallThickness", "must be greater than 0");
        DataLoadException.Require(
            arena.DoorWidth > 0 && arena.DoorWidth < arena.Size,
            file, "arena.doorWidth", "must be greater than 0 and smaller than arena.size");
        for (var i = 0; i < arena.Pillars.Count; i++)
        {
            var pillar = arena.Pillars[i];
            var field = $"arena.pillars[{i}]";
            DataLoadException.Require(pillar is not null, file, field, "must not be null");
            DataLoadException.Require(
                pillar!.Width > 0 && pillar.Height > 0, file, field, "width and height must be greater than 0");
            DataLoadException.Require(
                pillar.CenterX - pillar.Width / 2f > 0 && pillar.CenterX + pillar.Width / 2f < arena.Size
                && pillar.CenterY - pillar.Height / 2f > 0 && pillar.CenterY + pillar.Height / 2f < arena.Size,
                file, field, "must lie fully inside the arena floor");
        }

        DataLoadException.Require(Camera is not null, file, "camera", "is required");
        var camera = Camera!;
        DataLoadException.Require(camera.Zoom > 0, file, "camera.zoom", "must be greater than 0");
        DataLoadException.Require(
            camera.LeadFraction >= 0 && camera.LeadFraction <= 1,
            file, "camera.leadFraction", "must be between 0 and 1");
        DataLoadException.Require(camera.LeadMaxDistance >= 0, file, "camera.leadMaxDistance", "must not be negative");
        DataLoadException.Require(camera.Smoothing >= 0, file, "camera.smoothing", "must not be negative");
        DataLoadException.Require(camera.EdgeMargin >= 0, file, "camera.edgeMargin", "must not be negative");

        DataLoadException.Require(Player is not null, file, "player", "is required");
        var player = Player!;
        DataLoadException.Require(player.MaxHp > 0, file, "player.maxHp", "must be greater than 0");
        DataLoadException.Require(player.MoveSpeed > 0, file, "player.moveSpeed", "must be greater than 0");
        DataLoadException.Require(
            player.Radius > 0 && player.Radius * 2 < arena.Size,
            file, "player.radius", "must be greater than 0 and fit inside the arena");

        DataLoadException.Require(
            !string.IsNullOrWhiteSpace(player.StartingWeapon), file, "player.startingWeapon", "must not be empty");
        DataLoadException.Require(
            GlobalReloadTimeMultiplier >= 0, file, "globalReloadTimeMultiplier", "must not be negative");

        DataLoadException.Require(player.Dash is not null, file, "player.dash", "is required");
        var dash = player.Dash!;
        DataLoadException.Require(dash.Cooldown >= 0, file, "player.dash.cooldown", "must not be negative");
        DataLoadException.Require(dash.Duration > 0, file, "player.dash.duration", "must be greater than 0");
        DataLoadException.Require(dash.Distance > 0, file, "player.dash.distance", "must be greater than 0");
    }
}
