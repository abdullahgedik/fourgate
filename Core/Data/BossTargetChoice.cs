namespace Fourgate.Core.Data;

/// <summary>Whom a boss pattern is aimed at.</summary>
public enum BossTargetChoice
{
    /// <summary>The player the boss is after right now.</summary>
    Current,

    /// <summary>The living player farthest from the boss, so keeping your distance is not safe.</summary>
    Farthest,
}
