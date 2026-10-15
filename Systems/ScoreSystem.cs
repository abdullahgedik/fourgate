using System;
using Fourgate.Core.Data;

namespace Fourgate.Systems;

/// <summary>
/// The run's score (a team score in co-op): kill points + a bonus per cleared wave + points
/// per second survived, all multiplied by the mode's factor.
/// </summary>
public sealed class ScoreSystem
{
    private readonly ScoreConfig _config;

    public ScoreSystem(ScoreConfig config, float modeMultiplier)
    {
        _config = config;
        ModeMultiplier = modeMultiplier;
    }

    public float ModeMultiplier { get; }

    /// <summary>Sum of the score values of everything killed (bosses included).</summary>
    public int KillPoints { get; private set; }

    public int WavesCleared { get; private set; }

    /// <summary>Seconds survived; the clock stands still while upgrade cards are open.</summary>
    public float Seconds { get; private set; }

    public int Total =>
        (int)MathF.Round(
            (KillPoints + WavesCleared * _config.WaveClearBonus + MathF.Floor(Seconds * _config.PointsPerSecond))
            * ModeMultiplier);

    public void AddKill(int scoreValue)
    {
        KillPoints += scoreValue;
    }

    public void AddClearedWave()
    {
        WavesCleared++;
    }

    public void AddTime(float seconds)
    {
        Seconds += seconds;
    }
}
