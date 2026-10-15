using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace Fourgate.Core.Persistence;

/// <summary>
/// Reads and writes the high scores as JSON (by default <c>%APPDATA%/Fourgate/scores.json</c>).
/// A missing file gives empty lists; a broken one is set aside as <c>.bad</c> so it is not lost,
/// and the game carries on with empty lists. Saving never throws: high scores are not worth a crash.
/// </summary>
public sealed class HighScoreStore
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
    };

    public HighScoreStore(string path)
    {
        FilePath = path;
    }

    /// <summary>The standard place: <c>%APPDATA%/Fourgate/scores.json</c>.</summary>
    public static string DefaultPath =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Fourgate", "scores.json");

    public string FilePath { get; }

    /// <summary>Set when the last load or save had a problem; shown nowhere yet, useful for debugging.</summary>
    public string? LastProblem { get; private set; }

    public HighScoreTable Load()
    {
        var table = new HighScoreTable();
        if (!File.Exists(FilePath))
        {
            return table;
        }

        try
        {
            var file = JsonSerializer.Deserialize<ScoresFile>(File.ReadAllText(FilePath), Options);
            if (file?.Lists is null)
            {
                throw new JsonException("no lists");
            }

            foreach (var pair in file.Lists)
            {
                if (pair.Value is not null)
                {
                    table.Restore(pair.Key, pair.Value);
                }
            }
        }
        catch (Exception exception) when (exception is JsonException or IOException or UnauthorizedAccessException
                                              or NotSupportedException)
        {
            LastProblem = $"{FilePath} could not be read ({exception.Message}); starting with empty high scores.";
            SetAside();
            return new HighScoreTable();
        }

        return table;
    }

    /// <summary>Writes the lists; a half-written file can never replace a good one. False on failure.</summary>
    public bool Save(HighScoreTable table)
    {
        try
        {
            var folder = Path.GetDirectoryName(FilePath);
            if (!string.IsNullOrEmpty(folder))
            {
                Directory.CreateDirectory(folder);
            }

            var lists = new Dictionary<string, List<HighScoreEntry?>?>();
            foreach (var pair in table.Lists)
            {
                lists[pair.Key] = new List<HighScoreEntry?>(pair.Value);
            }

            var temporary = FilePath + ".tmp";
            File.WriteAllText(temporary, JsonSerializer.Serialize(new ScoresFile { Version = 1, Lists = lists }, Options));
            File.Move(temporary, FilePath, overwrite: true);
            return true;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or NotSupportedException)
        {
            LastProblem = $"{FilePath} could not be saved ({exception.Message}).";
            return false;
        }
    }

    // Keeps a broken file next to the new one instead of overwriting it on the next save.
    private void SetAside()
    {
        try
        {
            File.Move(FilePath, FilePath + ".bad", overwrite: true);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
        }
    }

    private sealed class ScoresFile
    {
        public int Version { get; set; }

        public Dictionary<string, List<HighScoreEntry?>?>? Lists { get; set; }
    }
}
