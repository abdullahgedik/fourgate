using System;

namespace Fourgate.Core.Rng;

/// <summary>
/// The game's random source. Everything random in gameplay draws from one seeded instance,
/// so a run can be reproduced from its seed.
/// </summary>
public sealed class GameRandom
{
    private readonly Random _random;

    public GameRandom(int seed)
    {
        Seed = seed;
        _random = new Random(seed);
    }

    public int Seed { get; }

    /// <summary>Returns a value from 0 (inclusive) to 1 (exclusive).</summary>
    public float NextFloat()
    {
        return _random.NextSingle();
    }

    /// <summary>Returns a value from <paramref name="min"/> (inclusive) to <paramref name="max"/> (exclusive).</summary>
    public float Range(float min, float max)
    {
        return min + (max - min) * _random.NextSingle();
    }

    /// <summary>Returns an integer from 0 (inclusive) to <paramref name="count"/> (exclusive).</summary>
    public int NextIndex(int count)
    {
        return _random.Next(count);
    }

    /// <summary>Returns true with the given probability (0–1).</summary>
    public bool Chance(float probability)
    {
        return _random.NextSingle() < probability;
    }
}
