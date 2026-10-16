namespace Fourgate.Core.Data;

/// <summary>
/// Kill combo, as in Boxhead: every kill within <see cref="WindowSeconds"/> of the last one
/// raises the multiplier the next kill's score is multiplied by. XP is not affected.
/// </summary>
public sealed class ComboConfig
{
    /// <summary>Seconds after a kill in which the next kill keeps the combo going.</summary>
    public required float WindowSeconds { get; init; }

    /// <summary>How much each kill in the combo adds to the multiplier (the first kill scores ×1).</summary>
    public required float MultiplierPerKill { get; init; }

    /// <summary>Highest multiplier; 0 for no cap.</summary>
    public float MaxMultiplier { get; init; }

    /// <summary>The multiplier for the <paramref name="combo"/>-th kill in a row (1 for the first).</summary>
    public float MultiplierFor(int combo)
    {
        var multiplier = 1f + (System.Math.Max(1, combo) - 1) * MultiplierPerKill;
        return MaxMultiplier > 0f ? System.Math.Min(multiplier, MaxMultiplier) : multiplier;
    }

    public void Validate(string file)
    {
        DataLoadException.Require(WindowSeconds > 0, file, "score.combo.windowSeconds", "must be greater than 0");
        DataLoadException.Require(
            MultiplierPerKill >= 0, file, "score.combo.multiplierPerKill", "must not be negative");
        DataLoadException.Require(
            MaxMultiplier == 0 || MaxMultiplier >= 1, file, "score.combo.maxMultiplier", "must be 0 (no cap) or at least 1");
    }
}
