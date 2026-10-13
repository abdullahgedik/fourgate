using System;
using System.IO;
using System.Text.Json;

namespace Fourgate.Core.Data;

/// <summary>Reads and validates the JSON files under <c>Data/</c>.</summary>
public static class DataLoader
{
    private const string GameFile = "game.json";

    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    /// <summary>The <c>Data</c> folder next to the executable.</summary>
    public static string DefaultDirectory => Path.Combine(AppContext.BaseDirectory, "Data");

    public static GameConfig LoadGameConfig(string dataDirectory)
    {
        return ParseGameConfig(ReadFile(dataDirectory, GameFile), GameFile);
    }

    /// <summary>Parses and validates <c>game.json</c> content; <paramref name="file"/> is only used in error messages.</summary>
    public static GameConfig ParseGameConfig(string json, string file)
    {
        var config = Parse<GameConfig>(json, file);
        config.Validate(file);
        return config;
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
