using System;
using System.Collections.Generic;
using System.IO;

namespace Fourgate.Core.Audio;

/// <summary>
/// Which sound files belong to which event id. A file is named after its id
/// (<c>weapon_fire_shotgun.wav</c>); variants add <c>-2</c>, <c>-3</c>… (<c>enemy_death-2.wav</c>)
/// and one is picked at random each time. A weapon without its own fire or throw sound uses the
/// generic <c>weapon_fire</c> / <c>weapon_throw</c> one.
/// </summary>
public sealed class SoundCatalog
{
    private readonly Dictionary<string, List<string>> _files = new(StringComparer.OrdinalIgnoreCase);

    /// <param name="paths">Sound file paths; only the file names matter for the ids.</param>
    public SoundCatalog(IEnumerable<string> paths)
    {
        foreach (var path in paths)
        {
            var id = IdOf(path);
            if (id.Length == 0)
            {
                continue;
            }

            if (!_files.TryGetValue(id, out var list))
            {
                list = new List<string>();
                _files[id] = list;
            }

            list.Add(path);
        }

        // Variants in a steady order, so a seeded pick is repeatable.
        foreach (var list in _files.Values)
        {
            list.Sort(StringComparer.OrdinalIgnoreCase);
        }
    }

    /// <summary>Every id that has at least one file.</summary>
    public IEnumerable<string> Ids => _files.Keys;

    /// <summary>The files for an event, falling back to the generic weapon sounds; null when there are none.</summary>
    public IReadOnlyList<string>? Resolve(string id)
    {
        if (_files.TryGetValue(id, out var files))
        {
            return files;
        }

        if (id.StartsWith(SfxIds.WeaponFire + "_", StringComparison.Ordinal)
            && _files.TryGetValue(SfxIds.WeaponFire, out files))
        {
            return files;
        }

        if (id.StartsWith(SfxIds.WeaponThrow + "_", StringComparison.Ordinal)
            && _files.TryGetValue(SfxIds.WeaponThrow, out files))
        {
            return files;
        }

        return null;
    }

    /// <summary>The event id a file stands for: its name without the extension and any "-N" variant number.</summary>
    public static string IdOf(string path)
    {
        var name = Path.GetFileNameWithoutExtension(path);
        var dash = name.LastIndexOf('-');
        if (dash > 0 && dash < name.Length - 1)
        {
            var allDigits = true;
            for (var i = dash + 1; i < name.Length; i++)
            {
                allDigits &= char.IsDigit(name[i]);
            }

            if (allDigits)
            {
                name = name.Substring(0, dash);
            }
        }

        return name.ToLowerInvariant();
    }
}
