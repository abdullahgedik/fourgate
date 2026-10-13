using Microsoft.Xna.Framework;

namespace Fourgate.Entities;

/// <summary>A sealed gate in the middle of one arena wall; enemies enter through it.</summary>
/// <param name="Side">Wall the door sits in.</param>
/// <param name="TopLeft">Top-left of the gate rectangle inside the wall strip.</param>
/// <param name="Size">Size of the gate rectangle.</param>
/// <param name="Inward">Unit vector pointing from the door into the arena.</param>
public readonly record struct Door(DoorSide Side, Vector2 TopLeft, Vector2 Size, Vector2 Inward)
{
    public Vector2 Center => TopLeft + Size / 2f;
}
