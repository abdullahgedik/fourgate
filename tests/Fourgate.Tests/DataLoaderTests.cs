using Fourgate.Core.Data;

namespace Fourgate.Tests;

public class DataLoaderTests
{
    private const string GameFile = "game.json";
    private const string WeaponsFile = "weapons.json";

    private const string ValidGameJson = """
        {
          "virtualWidth": 1920,
          "virtualHeight": 1080,
          "arena": { "x": 0, "y": 0, "size": 2000, "doorWidth": 280, "wallThickness": 24 },
          "camera": { "zoom": 1.15, "leadFraction": 0.35, "leadMaxDistance": 260, "smoothing": 10, "edgeMargin": 120 },
          "maxAliveEnemies": 60,
          "restCountdownSeconds": 5,
          "xp": { "base": 60, "exponent": 1.35, "sharedFullToAllPlayers": true },
          "reloadsEnabled": true,
          "globalReloadTimeMultiplier": 1.0,
          "player": {
            "maxHp": 100, "moveSpeed": 300, "radius": 20, "startingWeapon": "pistol",
            "dash": { "cooldown": 2.0, "duration": 0.18, "distance": 200, "iFrames": true }
          }
        }
        """;

    private const string ValidWeaponsJson = """
        {
          "weapons": [
            {
              "id": "pistol", "displayName": "Pistol", "slot": 1, "unlockWave": 1, "type": "projectile",
              "damage": 15, "fireRate": 3, "automatic": false,
              "ammo": { "magSize": 12, "reserveMax": 0, "reloadTime": 0.8, "infiniteReserve": true },
              "projectile": { "speed": 6000, "radius": 4, "range": 1100 }
            }
          ]
        }
        """;

    [Fact]
    public void ShippedDataFiles_LoadAndValidate()
    {
        var data = DataLoader.LoadAll(DataLoader.DefaultDirectory);

        Assert.NotNull(data.GetWeapon(data.Game.Player.StartingWeapon));
        Assert.Equal(60f, data.GetEnemy("normal").Hp);
        Assert.Equal(6f, data.Waves.Budget.Base);
    }

    [Fact]
    public void ValidGameFile_Loads()
    {
        var config = DataLoader.ParseGameConfig(ValidGameJson, GameFile);

        Assert.Equal(1920, config.VirtualWidth);
        Assert.Equal(2000f, config.Arena.Size);
        Assert.Equal(1.15f, config.Camera.Zoom);
        Assert.Equal(300f, config.Player.MoveSpeed);
        Assert.True(config.ReloadsEnabled);
    }

    [Fact]
    public void MissingField_NamesTheFile()
    {
        var json = ValidGameJson.Replace("\"virtualHeight\": 1080,", string.Empty);

        var error = Assert.Throws<DataLoadException>(() => DataLoader.ParseGameConfig(json, GameFile));

        Assert.Equal(GameFile, error.File);
        Assert.Contains("virtualHeight", error.Message);
    }

    [Theory]
    [InlineData("\"doorWidth\": 280", "\"doorWidth\": 2000", "arena.doorWidth")]
    [InlineData("\"zoom\": 1.15", "\"zoom\": 0", "camera.zoom")]
    [InlineData("\"leadFraction\": 0.35", "\"leadFraction\": 1.5", "camera.leadFraction")]
    [InlineData("\"moveSpeed\": 300", "\"moveSpeed\": -1", "player.moveSpeed")]
    [InlineData("\"maxHp\": 100", "\"maxHp\": 0", "player.maxHp")]
    [InlineData("\"base\": 60", "\"base\": 0", "xp.base")]
    [InlineData("\"duration\": 0.18", "\"duration\": 0", "player.dash.duration")]
    [InlineData("\"globalReloadTimeMultiplier\": 1.0", "\"globalReloadTimeMultiplier\": -1", "globalReloadTimeMultiplier")]
    public void OutOfRangeGameValue_NamesTheField(string original, string replacement, string expectedField)
    {
        var json = ValidGameJson.Replace(original, replacement);

        var error = Assert.Throws<DataLoadException>(() => DataLoader.ParseGameConfig(json, GameFile));

        Assert.Equal(expectedField, error.Field);
    }

    [Fact]
    public void MalformedJson_IsReportedAsDataLoadException()
    {
        Assert.Throws<DataLoadException>(() => DataLoader.ParseGameConfig("{ not json", GameFile));
    }

    [Fact]
    public void ValidWeaponsFile_Loads()
    {
        var weapons = DataLoader.ParseWeapons(ValidWeaponsJson, WeaponsFile);

        var pistol = Assert.Single(weapons);
        Assert.Equal("pistol", pistol.Id);
        Assert.Equal(WeaponType.Projectile, pistol.Type);
        Assert.Equal(12, pistol.Ammo.MagSize);
        Assert.True(pistol.Ammo.InfiniteReserve);
    }

    [Theory]
    [InlineData("\"fireRate\": 3", "\"fireRate\": 0", "weapons[0].fireRate")]
    [InlineData("\"magSize\": 12", "\"magSize\": 0", "weapons[0].ammo.magSize")]
    [InlineData("\"range\": 1100", "\"range\": -5", "weapons[0].projectile.range")]
    public void OutOfRangeWeaponValue_NamesTheField(string original, string replacement, string expectedField)
    {
        var json = ValidWeaponsJson.Replace(original, replacement);

        var error = Assert.Throws<DataLoadException>(() => DataLoader.ParseWeapons(json, WeaponsFile));

        Assert.Equal(WeaponsFile, error.File);
        Assert.Equal(expectedField, error.Field);
    }

    [Fact]
    public void UnknownWeaponType_IsRejected()
    {
        var json = ValidWeaponsJson.Replace("\"projectile\",", "\"laserSword\",");

        var error = Assert.Throws<DataLoadException>(() => DataLoader.ParseWeapons(json, WeaponsFile));

        Assert.Contains("type", error.Field);
    }

    [Fact]
    public void WavesFile_WithIntervalBelowItsMinimum_IsRejected()
    {
        const string json = """
            {
              "budget": { "base": 6, "perWave": 2.2, "quadratic": 0.04 },
              "newerTypeBias": 0.5,
              "spawnIntervalStart": 0.2, "spawnIntervalStepPerWave": 0.03, "spawnIntervalMin": 0.35,
              "groupSizeBase": 2, "groupSizePerWave": 0.15, "groupSizeMax": 6,
              "doorWarningSeconds": 0.8
            }
            """;

        var error = Assert.Throws<DataLoadException>(() => DataLoader.ParseWaves(json, "waves.json"));

        Assert.Equal("spawnIntervalStart", error.Field);
    }

    [Fact]
    public void StartingWeaponMissingFromWeaponsFile_IsRejected()
    {
        var game = DataLoader.ParseGameConfig(ValidGameJson.Replace("\"pistol\"", "\"bazooka\""), GameFile);
        var weapons = DataLoader.ParseWeapons(ValidWeaponsJson, WeaponsFile);

        var error = Assert.Throws<DataLoadException>(() => new GameData(game, weapons, new[] { TestData.Zombie() }, TestData.Waves()));

        Assert.Equal("player.startingWeapon", error.Field);
    }
}
