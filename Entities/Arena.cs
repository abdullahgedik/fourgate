using System.Collections.Generic;
using Fourgate.Core.Data;
using Microsoft.Xna.Framework;

namespace Fourgate.Entities;

/// <summary>Square arena geometry in virtual coordinates: walkable floor, surrounding walls and four doors.</summary>
public sealed class Arena
{
    private readonly Door[] _doors;

    public Arena(ArenaConfig config)
    {
        Left = config.X;
        Top = config.Y;
        Size = config.Size;
        WallThickness = config.WallThickness;

        var wall = config.WallThickness;
        var door = config.DoorWidth;
        var center = Center;

        _doors = new[]
        {
            new Door(DoorSide.Top, new Vector2(center.X - door / 2f, Top - wall), new Vector2(door, wall), Vector2.UnitY),
            new Door(DoorSide.Bottom, new Vector2(center.X - door / 2f, Bottom), new Vector2(door, wall), -Vector2.UnitY),
            new Door(DoorSide.Left, new Vector2(Left - wall, center.Y - door / 2f), new Vector2(wall, door), Vector2.UnitX),
            new Door(DoorSide.Right, new Vector2(Right, center.Y - door / 2f), new Vector2(wall, door), -Vector2.UnitX),
        };
    }

    public float Left { get; }

    public float Top { get; }

    public float Size { get; }

    public float WallThickness { get; }

    public float Right => Left + Size;

    public float Bottom => Top + Size;

    public Vector2 Center => new(Left + Size / 2f, Top + Size / 2f);

    public IReadOnlyList<Door> Doors => _doors;
}
