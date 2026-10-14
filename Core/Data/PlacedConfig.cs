namespace Fourgate.Core.Data;

/// <summary>Values of a weapon that is placed on the floor and set off by enemies (mine).</summary>
public sealed class PlacedConfig
{
    /// <summary>An enemy this close sets it off; 0 when enemies never do (a barrel).</summary>
    public float TriggerRadius { get; init; }

    /// <summary>Seconds after placing before enemies can set it off.</summary>
    public float ArmSeconds { get; init; }

    /// <summary>True when any hit by a player (bullet, flame, fire, explosion) sets it off (a barrel).</summary>
    public bool Shootable { get; init; }

    /// <summary>True when players and enemies cannot walk through it; it is then placed on a grid.</summary>
    public bool Solid { get; init; }

    /// <summary>Size of a grid cell for solid placed weapons; the object nearly fills one cell.</summary>
    public float GridSize { get; init; }

    /// <summary>
    /// Zombie hits a solid one takes before it goes off; 0 (the default) when zombies do not
    /// attack it. Player damage still sets it off at once.
    /// </summary>
    public int EnemyHitsToDetonate { get; init; }

    /// <summary>How far ahead of the player, along the aim, the cell to fill is looked for.</summary>
    public float PlaceDistance { get; init; }

    /// <summary>True when it sets off every placed explosive in its blast, from level 1.</summary>
    public bool ChainDetonation { get; init; }

    /// <summary>
    /// Most of these one player can have down at once; placing another removes the oldest.
    /// 0 (the default) means no limit.
    /// </summary>
    public int MaxPlaced { get; init; }
}
