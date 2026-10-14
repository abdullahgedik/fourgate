namespace Fourgate.Core.Data;

/// <summary>Values of a weapon that is placed on the floor and set off by enemies (mine).</summary>
public sealed class PlacedConfig
{
    /// <summary>An enemy this close to the mine sets it off.</summary>
    public required float TriggerRadius { get; init; }

    /// <summary>Seconds after placing before the mine can be set off.</summary>
    public required float ArmSeconds { get; init; }

    /// <summary>
    /// Most of these one player can have down at once; placing another removes the oldest.
    /// 0 (the default) means no limit.
    /// </summary>
    public int MaxPlaced { get; init; }
}
