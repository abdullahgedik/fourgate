using System;
using Microsoft.Xna.Framework;

namespace Fourgate.Core;

/// <summary>Circle and ray tests against axis-aligned boxes (pillars, barrels).</summary>
public static class Collision2D
{
    /// <summary>
    /// Moves a circle out of a box: away from the box's nearest point, or, with its center
    /// inside, out through the nearest side.
    /// </summary>
    public static Vector2 PushOutOfBox(Vector2 position, float radius, Vector2 center, Vector2 halfSize)
    {
        var offset = position - center;
        var nearest = new Vector2(
            Math.Clamp(offset.X, -halfSize.X, halfSize.X),
            Math.Clamp(offset.Y, -halfSize.Y, halfSize.Y));
        var away = offset - nearest;
        var distance = away.Length();

        if (distance > 0.0001f)
        {
            return distance >= radius ? position : position + away / distance * (radius - distance);
        }

        var pushX = halfSize.X - Math.Abs(offset.X) + radius;
        var pushY = halfSize.Y - Math.Abs(offset.Y) + radius;
        return pushX < pushY
            ? position + new Vector2(offset.X >= 0f ? pushX : -pushX, 0f)
            : position + new Vector2(0f, offset.Y >= 0f ? pushY : -pushY);
    }

    /// <summary>True when a point lies inside a box.</summary>
    public static bool Contains(Vector2 point, Vector2 center, Vector2 halfSize)
    {
        return Math.Abs(point.X - center.X) < halfSize.X && Math.Abs(point.Y - center.Y) < halfSize.Y;
    }

    /// <summary>
    /// Where a ray from outside first enters a box: the distance along the unit direction and
    /// the normal of the face it enters through. False when it misses or starts inside.
    /// </summary>
    public static bool RayBox(
        Vector2 origin, Vector2 direction, Vector2 center, Vector2 halfSize, out float distance, out Vector2 normal)
    {
        distance = 0f;
        normal = Vector2.Zero;
        if (Contains(origin, center, halfSize))
        {
            return false;
        }

        var min = center - halfSize;
        var max = center + halfSize;
        var enter = float.NegativeInfinity;
        var exit = float.PositiveInfinity;
        var enterNormal = Vector2.Zero;

        if (!Slab(origin.X, direction.X, min.X, max.X, ref enter, ref exit, ref enterNormal, Vector2.UnitX)
            || !Slab(origin.Y, direction.Y, min.Y, max.Y, ref enter, ref exit, ref enterNormal, Vector2.UnitY))
        {
            return false;
        }

        if (enter < 0f || enter > exit)
        {
            return false;
        }

        distance = enter;
        normal = enterNormal;
        return true;
    }

    /// <summary>True when the segment between two points passes through the inside of a box.</summary>
    public static bool SegmentCrossesBox(Vector2 from, Vector2 to, Vector2 center, Vector2 halfSize)
    {
        if (Contains(from, center, halfSize) || Contains(to, center, halfSize))
        {
            return true;
        }

        var delta = to - from;
        var length = delta.Length();
        if (length < 0.0001f)
        {
            return false;
        }

        return RayBox(from, delta / length, center, halfSize, out var distance, out _) && distance <= length;
    }

    // One axis of the slab method; tracks the latest entry and earliest exit along the ray.
    private static bool Slab(
        float origin,
        float direction,
        float min,
        float max,
        ref float enter,
        ref float exit,
        ref Vector2 enterNormal,
        Vector2 axis)
    {
        if (MathF.Abs(direction) < 0.000001f)
        {
            return origin > min && origin < max;
        }

        var near = (min - origin) / direction;
        var far = (max - origin) / direction;
        var nearNormal = -axis;
        if (near > far)
        {
            (near, far) = (far, near);
            nearNormal = axis;
        }

        if (near > enter)
        {
            enter = near;
            enterNormal = nearNormal;
        }

        exit = Math.Min(exit, far);
        return true;
    }
}
