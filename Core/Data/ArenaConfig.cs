using System.Collections.Generic;

namespace Fourgate.Core.Data;

/// <summary>Arena layout in virtual coordinates. X/Y/Size describe the walkable floor.</summary>
public sealed class ArenaConfig
{
    public required float X { get; init; }

    public required float Y { get; init; }

    public required float Size { get; init; }

    public required float DoorWidth { get; init; }

    public required float WallThickness { get; init; }

    /// <summary>Solid blocks on the floor; none when left out.</summary>
    public List<PillarConfig> Pillars { get; init; } = new();
}
