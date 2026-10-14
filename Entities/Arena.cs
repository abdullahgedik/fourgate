using System;
using System.Collections.Generic;
using Fourgate.Core;
using Fourgate.Core.Data;
using Microsoft.Xna.Framework;

namespace Fourgate.Entities;

/// <summary>
/// Square arena geometry in virtual coordinates: walkable floor, surrounding walls, four doors
/// and the solid pillars inside.
/// </summary>
public sealed class Arena
{
    // Walls and pillars meeting a ray at nearly the same distance count as a corner.
    private const float CornerTolerance = 0.01f;

    // How far past a pillar's corner an enemy aims when walking around it.
    private const float CornerClearance = 6f;

    // Rays stop this short of a pillar so the next ray starts outside it.
    private const float PillarSkin = 0.01f;

    private readonly Door[] _doors;
    private readonly Pillar[] _pillars;

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

        _pillars = new Pillar[config.Pillars.Count];
        for (var i = 0; i < _pillars.Length; i++)
        {
            var pillar = config.Pillars[i];
            _pillars[i] = new Pillar(
                new Vector2(Left + pillar.CenterX, Top + pillar.CenterY),
                new Vector2(pillar.Width / 2f, pillar.Height / 2f));
        }
    }

    public float Left { get; }

    public float Top { get; }

    public float Size { get; }

    public float WallThickness { get; }

    public float Right => Left + Size;

    public float Bottom => Top + Size;

    public Vector2 Center => new(Left + Size / 2f, Top + Size / 2f);

    public IReadOnlyList<Door> Doors => _doors;

    /// <summary>The solid blocks inside the arena.</summary>
    public IReadOnlyList<Pillar> Pillars => _pillars;

    /// <summary>Solid things placed on the floor (barrels); null when there are none.</summary>
    public IObstacleSource? Obstacles { get; set; }

    /// <summary>Floor position just inside a door for something of the given radius.</summary>
    /// <param name="door">The door to enter through.</param>
    /// <param name="radius">Radius of the thing entering.</param>
    /// <param name="lateral">Position across the door opening, from -1 (one edge) to 1 (the other).</param>
    public Vector2 GetDoorSpawnPoint(Door door, float radius, float lateral)
    {
        var across = new Vector2(-door.Inward.Y, door.Inward.X);
        var halfOpening = Math.Max(0f, Math.Max(door.Size.X, door.Size.Y) / 2f - radius);

        return door.Center
            + door.Inward * (WallThickness / 2f + radius)
            + across * (Math.Clamp(lateral, -1f, 1f) * halfOpening);
    }

    /// <summary>
    /// Distance from a point on the floor to the wall along a unit direction.
    /// Returns 0 when the point is already at or beyond the wall it faces.
    /// </summary>
    public float DistanceToWall(Vector2 origin, Vector2 direction)
    {
        return WallRaycast(origin, direction, out _);
    }

    /// <summary>
    /// Distance along a unit direction to the first solid surface (a wall or a pillar) and the
    /// normal of that surface; in a corner the normal points back along both sides.
    /// </summary>
    public float Raycast(Vector2 origin, Vector2 direction, out Vector2 normal)
    {
        var distance = WallRaycast(origin, direction, out normal);
        for (var i = 0; i < _pillars.Length; i++)
        {
            var pillar = _pillars[i];
            if (Collision2D.RayBox(origin, direction, pillar.Center, pillar.HalfSize, out var hit, out var hitNormal)
                && hit < distance)
            {
                distance = Math.Max(0f, hit - PillarSkin);
                normal = hitNormal;
            }
        }

        return distance;
    }

    /// <summary>True when a point is outside the floor or inside a pillar.</summary>
    public bool IsSolid(Vector2 point)
    {
        if (point.X <= Left || point.X >= Right || point.Y <= Top || point.Y >= Bottom)
        {
            return true;
        }

        for (var i = 0; i < _pillars.Length; i++)
        {
            if (Collision2D.Contains(point, _pillars[i].Center, _pillars[i].HalfSize))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>True when a box (e.g. a barrel's cell) overlaps a pillar.</summary>
    public bool OverlapsPillar(Vector2 center, Vector2 halfSize)
    {
        for (var i = 0; i < _pillars.Length; i++)
        {
            var pillar = _pillars[i];
            if (Math.Abs(center.X - pillar.Center.X) < halfSize.X + pillar.HalfSize.X
                && Math.Abs(center.Y - pillar.Center.Y) < halfSize.Y + pillar.HalfSize.Y)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Where a moving circle ends up: inside the walls and outside every pillar and obstacle.</summary>
    public Vector2 ResolveMovement(Vector2 position, float radius)
    {
        position = PlaceOnFloor(position, radius);
        if (Obstacles is not null)
        {
            position = Obstacles.PushOut(position, radius);
        }

        return ClampInside(position, radius);
    }

    /// <summary>Puts a circle on open floor: inside the walls and off every pillar (barrels are ignored).</summary>
    public Vector2 PlaceOnFloor(Vector2 position, float radius)
    {
        position = ClampInside(position, radius);
        for (var i = 0; i < _pillars.Length; i++)
        {
            position = Collision2D.PushOutOfBox(position, radius, _pillars[i].Center, _pillars[i].HalfSize);
        }

        return ClampInside(position, radius);
    }

    /// <summary>Pushes a circle back onto the floor; walls and sealed doors are solid.</summary>
    public Vector2 ClampInside(Vector2 position, float radius)
    {
        return new Vector2(
            Math.Clamp(position.X, Left + radius, Right - radius),
            Math.Clamp(position.Y, Top + radius, Bottom - radius));
    }

    /// <summary>
    /// The point to walk toward on the way to <paramref name="to"/>: the target itself when the
    /// way is clear, otherwise the best corner of the first pillar in the way. Pillars are far
    /// apart, so going around one corner at a time is enough.
    /// </summary>
    public Vector2 NextWaypoint(Vector2 from, Vector2 to, float radius)
    {
        Pillar? blocking = null;
        var blockingGrowth = 0f;
        var nearest = float.PositiveInfinity;
        for (var i = 0; i < _pillars.Length; i++)
        {
            var pillar = _pillars[i];

            // Grown by the walker's size (slightly less, so one resting against a face is not
            // "inside"); a target hugging the pillar is tested against the bare block instead,
            // so a big walker can still reach it by sliding along the face.
            var growth = radius - 1f;
            if (Collision2D.Contains(to, pillar.Center, pillar.HalfSize + new Vector2(growth)))
            {
                growth = 0f;
            }

            if (!Collision2D.SegmentCrossesBox(from, to, pillar.Center, pillar.HalfSize + new Vector2(growth)))
            {
                continue;
            }

            var distance = Vector2.DistanceSquared(from, pillar.Center);
            if (distance < nearest)
            {
                nearest = distance;
                blocking = pillar;
                blockingGrowth = growth;
            }
        }

        if (blocking is null)
        {
            return to;
        }

        return BestCorner(from, to, blocking.Value, radius, blockingGrowth);
    }

    // Of the corners the walker can reach in a straight line, the one with the shortest way on
    // around the pillar. Counting the whole way (not just to the target) keeps the choice steady
    // as the walker moves, so it does not flip between two corners.
    private static Vector2 BestCorner(Vector2 from, Vector2 to, Pillar pillar, float radius, float growth)
    {
        var body = pillar.HalfSize + new Vector2(growth);
        var reach = pillar.HalfSize + new Vector2(radius + CornerClearance);

        var best = to;
        var bestLength = float.PositiveInfinity;
        for (var corner = 0; corner < 4; corner++)
        {
            var point = Corner(pillar, reach, corner);

            // A corner already reached is not the way on.
            if (Vector2.DistanceSquared(from, point) < 1f
                || Collision2D.SegmentCrossesBox(from, point, pillar.Center, body))
            {
                continue;
            }

            var length = Vector2.Distance(from, point) + WayOnFromCorner(point, corner, to, pillar, body, reach);
            if (length < bestLength)
            {
                bestLength = length;
                best = point;
            }
        }

        return best;
    }

    // Straight to the target when the corner sees it, otherwise via the better neighbouring corner.
    private static float WayOnFromCorner(
        Vector2 point, int corner, Vector2 to, Pillar pillar, Vector2 body, Vector2 reach)
    {
        if (!Collision2D.SegmentCrossesBox(point, to, pillar.Center, body))
        {
            return Vector2.Distance(point, to);
        }

        var alongX = Corner(pillar, reach, corner ^ 1);
        var alongY = Corner(pillar, reach, corner ^ 2);
        return Math.Min(
            Vector2.Distance(point, alongX) + Vector2.Distance(alongX, to),
            Vector2.Distance(point, alongY) + Vector2.Distance(alongY, to));
    }

    // Corners 0-3: top-left, top-right, bottom-left, bottom-right.
    private static Vector2 Corner(Pillar pillar, Vector2 reach, int corner)
    {
        var sign = new Vector2(corner % 2 == 0 ? -1f : 1f, corner < 2 ? -1f : 1f);
        return pillar.Center + sign * reach;
    }

    private float WallRaycast(Vector2 origin, Vector2 direction, out Vector2 normal)
    {
        var alongX = float.PositiveInfinity;
        var alongY = float.PositiveInfinity;

        if (direction.X > 0f)
        {
            alongX = (Right - origin.X) / direction.X;
        }
        else if (direction.X < 0f)
        {
            alongX = (Left - origin.X) / direction.X;
        }

        if (direction.Y > 0f)
        {
            alongY = (Bottom - origin.Y) / direction.Y;
        }
        else if (direction.Y < 0f)
        {
            alongY = (Top - origin.Y) / direction.Y;
        }

        var normalX = new Vector2(-MathF.Sign(direction.X), 0f);
        var normalY = new Vector2(0f, -MathF.Sign(direction.Y));
        if (MathF.Abs(alongX - alongY) <= CornerTolerance)
        {
            normal = Vector2.Normalize(normalX + normalY);
        }
        else
        {
            normal = alongX < alongY ? normalX : normalY;
        }

        return Math.Max(0f, Math.Min(alongX, alongY));
    }
}
