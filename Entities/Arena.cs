using System;
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

    /// <summary>
    /// Distance from a point on the floor to the wall along a unit direction.
    /// Returns 0 when the point is already at or beyond the wall it faces.
    /// </summary>
    public float DistanceToWall(Vector2 origin, Vector2 direction)
    {
        var distance = float.PositiveInfinity;

        if (direction.X > 0f)
        {
            distance = Math.Min(distance, (Right - origin.X) / direction.X);
        }
        else if (direction.X < 0f)
        {
            distance = Math.Min(distance, (Left - origin.X) / direction.X);
        }

        if (direction.Y > 0f)
        {
            distance = Math.Min(distance, (Bottom - origin.Y) / direction.Y);
        }
        else if (direction.Y < 0f)
        {
            distance = Math.Min(distance, (Top - origin.Y) / direction.Y);
        }

        return Math.Max(0f, distance);
    }

    /// <summary>Pushes a circle back onto the floor; walls and sealed doors are solid.</summary>
    public Vector2 ClampInside(Vector2 position, float radius)
    {
        return new Vector2(
            Math.Clamp(position.X, Left + radius, Right - radius),
            Math.Clamp(position.Y, Top + radius, Bottom - radius));
    }
}
