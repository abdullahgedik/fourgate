using System.Collections.Generic;

namespace Fourgate.Core.Data;

/// <summary>
/// A later phase of a boss: once its HP falls to <see cref="HpFraction"/> it pauses, cannot be
/// hurt for <see cref="TransitionSeconds"/>, then fights with this phase's patterns.
/// </summary>
public sealed class BossPhaseConfig
{
    /// <summary>The phase starts when HP is at or below this share of max HP (0–1).</summary>
    public required float HpFraction { get; init; }

    /// <summary>How long the invulnerable, telegraphed change of phase lasts.</summary>
    public required float TransitionSeconds { get; init; }

    /// <summary>Seconds of walking between attacks in this phase; the boss's own value when left out.</summary>
    public float? IdleSeconds { get; init; }

    public BossPatternOrder PatternOrder { get; init; } = BossPatternOrder.Random;

    public required List<BossPatternConfig> Patterns { get; init; }

    public void Validate(string file, string path)
    {
        DataLoadException.Require(HpFraction > 0 && HpFraction < 1, file, $"{path}.hpFraction", "must be between 0 and 1");
        DataLoadException.Require(TransitionSeconds > 0, file, $"{path}.transitionSeconds", "must be greater than 0");
        DataLoadException.Require(IdleSeconds is null or >= 0, file, $"{path}.idleSeconds", "must not be negative");
        DataLoadException.Require(Patterns is { Count: > 0 }, file, $"{path}.patterns", "must list at least one pattern");
        for (var i = 0; i < Patterns!.Count; i++)
        {
            DataLoadException.Require(Patterns[i] is not null, file, $"{path}.patterns[{i}]", "must not be null");
            Patterns[i].Validate(file, $"{path}.patterns[{i}]");
        }
    }
}
