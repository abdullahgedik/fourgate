using System.Collections.Generic;

namespace Fourgate.Core.Audio;

/// <summary>
/// Passes everything on to the real audio manager and remembers the latest events, so they can be
/// shown on screen while there are no sounds yet (debug).
/// </summary>
public sealed class AudioEventLog : IAudioManager
{
    private const int Kept = 10;

    private readonly IAudioManager _inner;
    private readonly string[] _recent = new string[Kept];
    private int _next;

    public AudioEventLog(IAudioManager inner)
    {
        _inner = inner;
    }

    /// <summary>How many events were logged in total.</summary>
    public int Total { get; private set; }

    /// <summary>The latest events, newest first (at most ten).</summary>
    public IEnumerable<string> Recent
    {
        get
        {
            var count = System.Math.Min(Total, Kept);
            for (var i = 1; i <= count; i++)
            {
                yield return _recent[(_next - i + Kept) % Kept];
            }
        }
    }

    public void PlaySfx(string id, float volume = 1f)
    {
        Remember(id);
        _inner.PlaySfx(id, volume);
    }

    public void PlayMusic(string id)
    {
        Remember(id);
        _inner.PlayMusic(id);
    }

    public void SetVolumes(float master, float music, float effects)
    {
        _inner.SetVolumes(master, music, effects);
    }

    private void Remember(string id)
    {
        _recent[_next] = id;
        _next = (_next + 1) % Kept;
        Total++;
    }
}
