namespace Fourgate.Core.Data;

/// <summary>Dash tuning.</summary>
public sealed class DashConfig
{
    /// <summary>Seconds between dash starts.</summary>
    public required float Cooldown { get; init; }

    /// <summary>Seconds the dash lasts.</summary>
    public required float Duration { get; init; }

    /// <summary>World units covered by one dash.</summary>
    public required float Distance { get; init; }

    /// <summary>Whether the player is invulnerable for the whole dash.</summary>
    public required bool IFrames { get; init; }
}
