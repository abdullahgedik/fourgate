using System.Collections.Generic;

namespace Fourgate.Core.Data;

/// <summary>Root object of <c>enemies.json</c>.</summary>
public sealed class EnemiesFile
{
    public required List<EnemyConfig> Enemies { get; init; }
}
