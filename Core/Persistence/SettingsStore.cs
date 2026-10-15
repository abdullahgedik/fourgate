using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Fourgate.Core.Persistence;

/// <summary>
/// Reads and writes <c>settings.json</c> (by default under <c>%APPDATA%/Fourgate/</c>). A missing
/// file gives the defaults; a broken one is set aside as <c>.bad</c>. Saving never throws.
/// </summary>
public sealed class SettingsStore
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
    };

    public SettingsStore(string path)
    {
        FilePath = path;
    }

    /// <summary>The standard place: <c>%APPDATA%/Fourgate/settings.json</c>.</summary>
    public static string DefaultPath =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Fourgate", "settings.json");

    public string FilePath { get; }

    /// <summary>Set when the last load or save had a problem.</summary>
    public string? LastProblem { get; private set; }

    public GameSettings Load()
    {
        if (!File.Exists(FilePath))
        {
            return new GameSettings();
        }

        try
        {
            var settings = JsonSerializer.Deserialize<GameSettings>(File.ReadAllText(FilePath), Options)
                ?? throw new JsonException("empty file");
            settings.Clamp();
            return settings;
        }
        catch (Exception exception) when (exception is JsonException or IOException or UnauthorizedAccessException
                                              or NotSupportedException)
        {
            LastProblem = $"{FilePath} could not be read ({exception.Message}); using the default settings.";
            try
            {
                File.Move(FilePath, FilePath + ".bad", overwrite: true);
            }
            catch (Exception moveFailure) when (moveFailure is IOException or UnauthorizedAccessException)
            {
            }

            return new GameSettings();
        }
    }

    /// <summary>Writes the settings through a temporary file. False on failure.</summary>
    public bool Save(GameSettings settings)
    {
        try
        {
            var folder = Path.GetDirectoryName(FilePath);
            if (!string.IsNullOrEmpty(folder))
            {
                Directory.CreateDirectory(folder);
            }

            var temporary = FilePath + ".tmp";
            File.WriteAllText(temporary, JsonSerializer.Serialize(settings, Options));
            File.Move(temporary, FilePath, overwrite: true);
            return true;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or NotSupportedException)
        {
            LastProblem = $"{FilePath} could not be saved ({exception.Message}).";
            return false;
        }
    }
}
