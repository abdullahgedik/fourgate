using System.Collections.Generic;

namespace Fourgate.Core.Data;

/// <summary>Root object of <c>bosses.json</c>.</summary>
public sealed class BossesFile
{
    public required List<BossConfig> Bosses { get; init; }
}
