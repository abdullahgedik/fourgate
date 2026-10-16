using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using Fourgate.Core.Data;
using Fourgate.Core.Rng;
using Microsoft.Xna.Framework.Audio;
using Microsoft.Xna.Framework.Media;

namespace Fourgate.Core.Audio;

/// <summary>
/// Plays the sound files found in the audio folder: WAV for effects, OGG for music, each named
/// after its event id (see <see cref="SoundCatalog"/>). An event without a file stays silent, a
/// broken file is skipped, and a machine without a sound device simply runs silent.
/// </summary>
public sealed class MonoGameAudioManager : IAudioManager, IDisposable
{
    private readonly AudioConfig _config;
    private readonly GameRandom _random = new(71);
    private readonly Stopwatch _clock = Stopwatch.StartNew();
    private readonly VoiceLimiter _limiter;
    private readonly Dictionary<string, SoundEffect[]?> _effects = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, SoundEffect?> _loaded = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, Song?> _songs = new(StringComparer.OrdinalIgnoreCase);
    private readonly SoundCatalog _effectFiles;
    private readonly SoundCatalog _musicFiles;
    private readonly List<string> _problems = new();
    private bool _silent;
    private string? _currentMusic;
    private float _master = 1f;
    private float _music = 1f;
    private float _sfx = 1f;

    /// <param name="folder">The folder with the sound files (missing: everything is silent).</param>
    /// <param name="config">Mixing rules.</param>
    public MonoGameAudioManager(string folder, AudioConfig config)
    {
        _config = config;
        _limiter = new VoiceLimiter(config.MaxVoicesPerSound, config.MinRepeatSeconds);
        var exists = Directory.Exists(folder);
        _effectFiles = new SoundCatalog(exists ? Directory.EnumerateFiles(folder, "*.wav") : Array.Empty<string>());
        _musicFiles = new SoundCatalog(exists ? Directory.EnumerateFiles(folder, "*.ogg") : Array.Empty<string>());
    }

    /// <summary>
    /// Loads every effect file now, so the first shot of a fight does not wait for the disk.
    /// Call once the game window exists.
    /// </summary>
    public void Preload()
    {
        foreach (var id in _effectFiles.Ids)
        {
            EffectsFor(id);
        }
    }

    /// <summary>Files that could not be loaded, and why (for the debug log).</summary>
    public IReadOnlyList<string> Problems => _problems;

    public void PlaySfx(string id, float volume = 1f)
    {
        if (_silent || volume <= 0f)
        {
            return;
        }

        var variants = EffectsFor(id);
        if (variants is null)
        {
            return;
        }

        var effect = variants[variants.Length == 1 ? 0 : _random.NextIndex(variants.Length)];
        if (!_limiter.TryStart(id, _clock.Elapsed.TotalSeconds, effect.Duration.TotalSeconds))
        {
            return;
        }

        var pitch = _config.PitchVariation > 0f ? _random.Range(-_config.PitchVariation, _config.PitchVariation) : 0f;
        try
        {
            effect.Play(Math.Clamp(volume * _sfx * _master, 0f, 1f), pitch, 0f);
        }
        catch (Exception exception) when (exception is InstancePlayLimitException or NoAudioHardwareException)
        {
            // Too many sounds at once, or the device went away: drop this one.
        }
    }

    public void PlayMusic(string id)
    {
        if (_silent || string.Equals(id, _currentMusic, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        _currentMusic = id;
        try
        {
            var song = SongFor(id);
            if (song is null)
            {
                // No track for this moment: stop, rather than keep playing the wrong one.
                MediaPlayer.Stop();
                return;
            }

            MediaPlayer.IsRepeating = true;
            MediaPlayer.Volume = _music * _master;
            MediaPlayer.Play(song);
        }
        catch (Exception exception) when (exception is NoAudioHardwareException or InvalidOperationException)
        {
            GoSilent(exception);
        }
    }

    public void SetVolumes(float master, float music, float effects)
    {
        _master = Math.Clamp(master, 0f, 1f);
        _music = Math.Clamp(music, 0f, 1f);
        _sfx = Math.Clamp(effects, 0f, 1f);
        if (_silent)
        {
            return;
        }

        try
        {
            MediaPlayer.Volume = _music * _master;
        }
        catch (Exception exception) when (exception is NoAudioHardwareException or InvalidOperationException)
        {
            GoSilent(exception);
        }
    }

    public void Dispose()
    {
        try
        {
            MediaPlayer.Stop();
        }
        catch (Exception exception) when (exception is NoAudioHardwareException or InvalidOperationException)
        {
        }

        foreach (var effect in _loaded.Values)
        {
            effect?.Dispose();
        }

        foreach (var song in _songs.Values)
        {
            song?.Dispose();
        }
    }

    // Worked out the first time an event asks, then kept; ids without files are remembered as null.
    // Each file is loaded only once, however many ids fall back to it.
    private SoundEffect[]? EffectsFor(string id)
    {
        if (_effects.TryGetValue(id, out var cached))
        {
            return cached;
        }

        SoundEffect[]? loaded = null;
        var files = _effectFiles.Resolve(id);
        if (files is not null)
        {
            var list = new List<SoundEffect>(files.Count);
            foreach (var file in files)
            {
                var effect = LoadEffect(file);
                if (effect is not null)
                {
                    list.Add(effect);
                }
            }

            loaded = list.Count > 0 ? list.ToArray() : null;
        }

        _effects[id] = loaded;
        return loaded;
    }

    private SoundEffect? LoadEffect(string file)
    {
        if (_loaded.TryGetValue(file, out var known))
        {
            return known;
        }

        _loaded[file] = null;
        try
        {
            var effect = SoundEffect.FromFile(file);
            _loaded[file] = effect;
            return effect;
        }
        catch (NoAudioHardwareException exception)
        {
            GoSilent(exception);
        }
        catch (Exception exception) when (exception is IOException or ArgumentException or InvalidOperationException
                                              or NotSupportedException or UnauthorizedAccessException)
        {
            _problems.Add($"{file}: {exception.Message}");
        }

        return null;
    }

    private Song? SongFor(string id)
    {
        if (_songs.TryGetValue(id, out var cached))
        {
            return cached;
        }

        Song? song = null;
        var files = _musicFiles.Resolve(id);
        if (files is not null)
        {
            try
            {
                song = Song.FromUri(id, new Uri(Path.GetFullPath(files[0])));
            }
            catch (Exception exception) when (exception is IOException or ArgumentException or InvalidOperationException
                                                  or NotSupportedException or UriFormatException)
            {
                _problems.Add($"{files[0]}: {exception.Message}");
            }
        }

        _songs[id] = song;
        return song;
    }

    private void GoSilent(Exception exception)
    {
        _silent = true;
        _problems.Add($"No sound device ({exception.Message}); the game runs silent.");
    }
}
