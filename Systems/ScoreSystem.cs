using System;
using Fourgate.Core.Data;

namespace Fourgate.Systems;

/// <summary>
/// The run's score (a team score in co-op): kill points + a bonus per cleared wave + points
/// per second survived, all multiplied by the mode's factor. Kills in quick succession build a
/// team combo that multiplies each kill's points.
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

    /// <summary>Kills in the current combo; 0 when there is none.</summary>
    public int Combo { get; private set; }

    /// <summary>Longest combo of the run.</summary>
    public int BestCombo { get; private set; }

    /// <summary>Seconds left for the next kill to keep the combo going.</summary>
    public float ComboSecondsLeft { get; private set; }

    /// <summary>0–1: how much of the combo window is left.</summary>
    public float ComboTimeFraction =>
        _config.Combo is { } combo ? Math.Clamp(ComboSecondsLeft / combo.WindowSeconds, 0f, 1f) : 0f;

    /// <summary>The multiplier the current combo gives a kill.</summary>
    public float ComboMultiplier => _config.Combo?.MultiplierFor(Combo) ?? 1f;

    /// <summary>Seconds survived; the clock stands still while upgrade cards are open.</summary>
    public float Seconds { get; private set; }

    public int Total =>
        (int)MathF.Round(
            (KillPoints + WavesCleared * _config.WaveClearBonus + MathF.Floor(Seconds * _config.PointsPerSecond))
            * ModeMultiplier);

    public void AddKill(int scoreValue)
    {
        if (_config.Combo is not { } combo)
        {
            KillPoints += scoreValue;
            return;
        }

        Combo = ComboSecondsLeft > 0f ? Combo + 1 : 1;
        BestCombo = Math.Max(BestCombo, Combo);
        ComboSecondsLeft = combo.WindowSeconds;
        KillPoints += (int)MathF.Round(scoreValue * combo.MultiplierFor(Combo));
    }

    public void AddClearedWave()
    {
        WavesCleared++;
    }

    public void AddTime(float seconds)
    {
        Seconds += seconds;
        if (ComboSecondsLeft > 0f)
        {
            ComboSecondsLeft = Math.Max(0f, ComboSecondsLeft - seconds);
            if (ComboSecondsLeft <= 0f)
            {
                Combo = 0;
            }
        }
    }
}
