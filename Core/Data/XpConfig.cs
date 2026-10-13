using System;

namespace Fourgate.Core.Data;

/// <summary>Level curve and XP sharing rules.</summary>
public sealed class XpConfig
{
    /// <summary>XP needed to go from level 1 to level 2.</summary>
    public required float Base { get; init; }

    /// <summary>How steeply the requirement grows with level.</summary>
    public required float Exponent { get; init; }

    /// <summary>When true every kill gives its full XP to all players, dead ones included; otherwise only to the killer.</summary>
    public required bool SharedFullToAllPlayers { get; init; }

    /// <summary>XP needed to go from <paramref name="level"/> to the next: <c>round(base · level^exponent)</c>.</summary>
    public int XpToNext(int level)
    {
        var needed = (int)MathF.Round(Base * MathF.Pow(Math.Max(1, level), Exponent));
        return Math.Max(1, needed);
    }
}
