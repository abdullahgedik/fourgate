using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Fourgate.Core.Data;

/// <summary>Reads and validates the JSON files under <c>Data/</c>.</summary>
public static class DataLoader
{
    public const string GameFileName = "game.json";
    public const string WeaponsFileName = "weapons.json";
    public const string EnemiesFileName = "enemies.json";
    public const string WavesFileName = "waves.json";

    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase, allowIntegerValues: false) },
    };

    /// <summary>The <c>Data</c> folder next to the executable.</summary>
    public static string DefaultDirectory => Path.Combine(AppContext.BaseDirectory, "Data");

    /// <summary>Loads every data file and checks the references between them.</summary>
    public static GameData LoadAll(string dataDirectory)
    {
        var game = ParseGameConfig(ReadFile(dataDirectory, GameFileName), GameFileName);
        var weapons = ParseWeapons(ReadFile(dataDirectory, WeaponsFileName), WeaponsFileName);
        var enemies = ParseEnemies(ReadFile(dataDirectory, EnemiesFileName), EnemiesFileName);
        var waves = ParseWaves(ReadFile(dataDirectory, WavesFileName), WavesFileName);
        return new GameData(game, weapons, enemies, waves);
    }

    /// <summary>Parses and validates <c>waves.json</c> content; <paramref name="file"/> is only used in error messages.</summary>
    public static WavesConfig ParseWaves(string json, string file)
    {
        var config = Parse<WavesConfig>(json, file);
        config.Validate(file);
        return config;
    }

    /// <summary>Parses and validates <c>enemies.json</c> content; <paramref name="file"/> is only used in error messages.</summary>
    public static IReadOnlyList<EnemyConfig> ParseEnemies(string json, string file)
    {
        var enemies = Parse<EnemiesFile>(json, file).Enemies;
        DataLoadException.Require(enemies is { Count: > 0 }, file, "enemies", "must contain at least one enemy");

        var ids = new HashSet<string>();
        for (var i = 0; i < enemies!.Count; i++)
        {
            var path = $"enemies[{i}]";
            DataLoadException.Require(enemies[i] is not null, file, path, "must not be null");
            enemies[i].Validate(file, path);
            DataLoadException.Require(ids.Add(enemies[i].Id), file, $"{path}.id", $"'{enemies[i].Id}' is used more than once");
        }

        return enemies;
    }

    /// <summary>Parses and validates <c>game.json</c> content; <paramref name="file"/> is only used in error messages.</summary>
    public static GameConfig ParseGameConfig(string json, string file)
    {
        var config = Parse<GameConfig>(json, file);
        config.Validate(file);
        return config;
    }

    /// <summary>Parses and validates <c>weapons.json</c> content; <paramref name="file"/> is only used in error messages.</summary>
    public static IReadOnlyList<WeaponConfig> ParseWeapons(string json, string file)
    {
        var weapons = Parse<WeaponsFile>(json, file).Weapons;
        DataLoadException.Require(weapons is { Count: > 0 }, file, "weapons", "must contain at least one weapon");

        var ids = new HashSet<string>();
        for (var i = 0; i < weapons!.Count; i++)
        {
            var path = $"weapons[{i}]";
            DataLoadException.Require(weapons[i] is not null, file, path, "must not be null");
            weapons[i].Validate(file, path);
            DataLoadException.Require(ids.Add(weapons[i].Id), file, $"{path}.id", $"'{weapons[i].Id}' is used more than once");
        }

        return weapons;
    }

    private static string ReadFile(string dataDirectory, string file)
    {
        var path = Path.Combine(dataDirectory, file);
        try
        {
            return File.ReadAllText(path);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            throw new DataLoadException(file, "(file)", $"could not be read from '{path}': {e.Message}", e);
        }
    }

    private static T Parse<T>(string json, string file)
    {
        try
        {
            return JsonSerializer.Deserialize<T>(json, Options)
                ?? throw new DataLoadException(file, "(root)", "must be a JSON object, not null");
        }
        catch (JsonException e)
        {
            var field = string.IsNullOrEmpty(e.Path) || e.Path == "$" ? "(root)" : e.Path;
            throw new DataLoadException(file, field, e.Message, e);
        }
    }
}
