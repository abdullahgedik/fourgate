namespace Fourgate.Core.Persistence;

/// <summary>One line of a high-score list.</summary>
public sealed class HighScoreEntry
{
    public required int Score { get; init; }

    public required int Wave { get; init; }

    public required int Kills { get; init; }

    /// <summary>Seconds survived.</summary>
    public required int Seconds { get; init; }

    /// <summary>1 or 2.</summary>
    public required int Players { get; init; }

    /// <summary>Day of the run, as yyyy-MM-dd.</summary>
    public required string Date { get; init; }

    /// <summary>Three capital letters.</summary>
    public required string Initials { get; init; }
}
