using System.Collections.Generic;

namespace Fourgate.Core.Audio;

/// <summary>
/// Keeps rapid fire from stacking dozens of copies of one sound: at most so many copies of a
/// sound play at once, and the same sound never starts twice within a short gap.
/// </summary>
public sealed class VoiceLimiter
{
    private readonly int _maxVoices;
    private readonly double _minGap;
    private readonly Dictionary<string, Voices> _voices = new();

    public VoiceLimiter(int maxVoices, double minGapSeconds)
    {
        _maxVoices = maxVoices;
        _minGap = minGapSeconds;
    }

    /// <summary>Starts a copy of a sound if there is room; returns false when it should be dropped.</summary>
    /// <param name="id">The sound.</param>
    /// <param name="now">Current time in seconds.</param>
    /// <param name="duration">How long this copy plays, in seconds.</param>
    public bool TryStart(string id, double now, double duration)
    {
        if (!_voices.TryGetValue(id, out var voices))
        {
            voices = new Voices(_maxVoices);
            _voices[id] = voices;
        }

        if (now - voices.LastStart < _minGap)
        {
            return false;
        }

        // Reuse the slot of a copy that has finished.
        for (var i = 0; i < voices.EndTimes.Length; i++)
        {
            if (voices.EndTimes[i] <= now)
            {
                voices.EndTimes[i] = now + duration;
                voices.LastStart = now;
                return true;
            }
        }

        return false;
    }

    private sealed class Voices
    {
        public Voices(int count)
        {
            EndTimes = new double[count];
        }

        public double[] EndTimes { get; }

        public double LastStart { get; set; } = double.NegativeInfinity;
    }
}
