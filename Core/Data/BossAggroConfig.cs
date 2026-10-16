namespace Fourgate.Core.Data;

/// <summary>
/// How a boss chooses whom to go after in co-op. Every player builds threat by hurting the boss
/// and by staying close; threat fades over time. The boss commits to a target for a while,
/// then switches to whoever has clearly more threat. Without this block a boss simply chases
/// the nearest player.
/// </summary>
public sealed class BossAggroConfig
{
    /// <summary>Shortest time the boss stays on one target before it may switch.</summary>
    public required float MinTargetSeconds { get; init; }

    /// <summary>Longest such time; each commitment lasts a random time in between.</summary>
    public required float MaxTargetSeconds { get; init; }

    /// <summary>Threat gained per point of damage dealt to the boss.</summary>
    public required float ThreatPerDamage { get; init; }

    /// <summary>Threat gained per second by a player standing right next to the boss.</summary>
    public required float ProximityThreatPerSecond { get; init; }

    /// <summary>Distance at which closeness stops adding threat (it grows toward the boss).</summary>
    public required float ProximityRange { get; init; }

    /// <summary>Share of their threat every player loses per second.</summary>
    public required float ThreatDecayPerSecond { get; init; }

    /// <summary>Another player needs this many times the current target's threat to take the boss over.</summary>
    public required float SwitchMargin { get; init; }

    /// <summary>Share of every player's threat kept when the boss enters a new phase (1 keeps it all).</summary>
    public float PhaseThreatKeep { get; init; } = 1f;

    public void Validate(string file, string path)
    {
        DataLoadException.Require(MinTargetSeconds > 0, file, $"{path}.minTargetSeconds", "must be greater than 0");
        DataLoadException.Require(
            MaxTargetSeconds >= MinTargetSeconds, file, $"{path}.maxTargetSeconds", "must not be less than minTargetSeconds");
        DataLoadException.Require(ThreatPerDamage >= 0, file, $"{path}.threatPerDamage", "must not be negative");
        DataLoadException.Require(
            ProximityThreatPerSecond >= 0, file, $"{path}.proximityThreatPerSecond", "must not be negative");
        DataLoadException.Require(ProximityRange > 0, file, $"{path}.proximityRange", "must be greater than 0");
        DataLoadException.Require(
            ThreatDecayPerSecond >= 0 && ThreatDecayPerSecond < 1, file, $"{path}.threatDecayPerSecond", "must be from 0 up to (not including) 1");
        DataLoadException.Require(SwitchMargin >= 1, file, $"{path}.switchMargin", "must be 1 or greater");
        DataLoadException.Require(
            PhaseThreatKeep is >= 0 and <= 1, file, $"{path}.phaseThreatKeep", "must be between 0 and 1");
    }
}
