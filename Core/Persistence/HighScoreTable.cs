using System;
using System.Collections.Generic;

namespace Fourgate.Core.Persistence;

/// <summary>
/// The local high scores: a best-first top-10 list for each mode (normal, fast, nightmare,
/// nightmareFast). An equal score goes below the ones already there.
/// </summary>
public sealed class HighScoreTable
{
    /// <summary>Lines kept per mode.</summary>
    public const int MaxEntries = 10;

    private readonly Dictionary<string, List<HighScoreEntry>> _lists = new();

    /// <summary>Goes up on every change, so views can tell when to rebuild their text.</summary>
    public int Version { get; private set; }

    /// <summary>The modes that have lists, for saving.</summary>
    public IReadOnlyDictionary<string, List<HighScoreEntry>> Lists => _lists;

    /// <summary>The list for a mode, best first; empty when nothing was recorded yet.</summary>
    public IReadOnlyList<HighScoreEntry> Get(string modeKey)
    {
        return _lists.TryGetValue(modeKey, out var list) ? list : Array.Empty<HighScoreEntry>();
    }

    /// <summary>True when a score would make the list (it has to be above 0).</summary>
    public bool Qualifies(string modeKey, int score)
    {
        if (score <= 0)
        {
            return false;
        }

        var list = Get(modeKey);
        return list.Count < MaxEntries || score > list[^1].Score;
    }

    /// <summary>Adds an entry where it belongs and returns its place (0 = best), or -1 if it did not make it.</summary>
    public int Insert(string modeKey, HighScoreEntry entry)
    {
        if (!Qualifies(modeKey, entry.Score))
        {
            return -1;
        }

        if (!_lists.TryGetValue(modeKey, out var list))
        {
            list = new List<HighScoreEntry>();
            _lists[modeKey] = list;
        }

        var index = 0;
        while (index < list.Count && list[index].Score >= entry.Score)
        {
            index++;
        }

        list.Insert(index, entry);
        if (list.Count > MaxEntries)
        {
            list.RemoveAt(list.Count - 1);
        }

        Version++;
        return index;
    }

    /// <summary>Puts back a list read from disk, keeping only valid lines, best first, at most ten.</summary>
    public void Restore(string modeKey, IEnumerable<HighScoreEntry?> entries)
    {
        var list = new List<HighScoreEntry>();
        foreach (var entry in entries)
        {
            if (entry is { Score: > 0, Initials.Length: 3 } && entry.Date is not null)
            {
                list.Add(entry);
            }
        }

        list.Sort((a, b) => b.Score.CompareTo(a.Score));
        if (list.Count > MaxEntries)
        {
            list.RemoveRange(MaxEntries, list.Count - MaxEntries);
        }

        _lists[modeKey] = list;
        Version++;
    }
}
