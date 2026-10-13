using System.Collections.Generic;

namespace Fourgate.Core.Data;

/// <summary>Root object of <c>weapons.json</c>.</summary>
public sealed class WeaponsFile
{
    public required List<WeaponConfig> Weapons { get; init; }
}
