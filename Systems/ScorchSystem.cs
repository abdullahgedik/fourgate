using System;
using Fourgate.Core.Rendering;
using Fourgate.Entities;
using Microsoft.Xna.Framework;

namespace Fourgate.Systems;

/// <summary>
/// Black soot left by explosions for the rest of the run: a scorched blot on the floor (painted
/// into the floor stains) and soot rising up the faces of nearby walls and pillars. Purely visual.
/// </summary>
public sealed class ScorchSystem
{
    // The newest wall marks kept; older ones give way (each costs a little to draw).
    private const int MaxWallMarks = 256;

    // Floor blot: a wide soft ring of soot and a darker core, as shares of the blast radius.
    private const float FloorSootRadius = 0.9f;
    private const float FloorCoreRadius = 0.45f;
    private const float FloorSootAlpha = 0.6f;
    private const float FloorCoreAlpha = 0.85f;

    // Wall soot: how much of the blast's reach along the face it covers, how high it climbs and how dark it gets.
    private const float WallWidthShare = 1.1f;
    private const float WallMinHeight = 20f;
    private const float WallHeightPerRadius = 1f;
    private const float WallAlpha = 0.9f;
    private const float WallOffset = 1f;

    private static readonly Color Soot = new(18, 16, 14);

    private readonly Arena _arena;
    private readonly FloorStains _floor;
    private readonly float _wallHeight;
    private readonly float _pillarHeight;
    private readonly Mark[] _marks = new Mark[MaxWallMarks];
    private int _next;

    /// <param name="arena">Walls and pillars that can be scorched.</param>
    /// <param name="floor">Where floor scorches are painted.</param>
    /// <param name="wallHeight">How tall the outer walls are drawn.</param>
    /// <param name="pillarHeight">How tall the pillars are drawn.</param>
    public ScorchSystem(Arena arena, FloorStains floor, float wallHeight, float pillarHeight)
    {
        _arena = arena;
        _floor = floor;
        _wallHeight = wallHeight;
        _pillarHeight = pillarHeight;
    }

    /// <summary>Wall and pillar marks currently kept.</summary>
    public int WallMarkCount { get; private set; }

    /// <summary>Scorches the floor under a blast and every wall or pillar face within its reach.</summary>
    public void AddBlast(Vector2 center, float radius)
    {
        _floor.Add(center, radius * FloorSootRadius, Soot * FloorSootAlpha);
        _floor.Add(center, radius * FloorCoreRadius, Soot * FloorCoreAlpha);

        // The inner faces of the four outer walls.
        ScorchFace(center, radius, new Vector2(_arena.Left, _arena.Top), new Vector2(_arena.Right, _arena.Top), Vector2.UnitY, _wallHeight);
        ScorchFace(center, radius, new Vector2(_arena.Left, _arena.Bottom), new Vector2(_arena.Right, _arena.Bottom), -Vector2.UnitY, _wallHeight);
        ScorchFace(center, radius, new Vector2(_arena.Left, _arena.Top), new Vector2(_arena.Left, _arena.Bottom), Vector2.UnitX, _wallHeight);
        ScorchFace(center, radius, new Vector2(_arena.Right, _arena.Top), new Vector2(_arena.Right, _arena.Bottom), -Vector2.UnitX, _wallHeight);

        // The four faces of every pillar.
        var pillars = _arena.Pillars;
        for (var i = 0; i < pillars.Count; i++)
        {
            var min = pillars[i].Center - pillars[i].HalfSize;
            var max = pillars[i].Center + pillars[i].HalfSize;
            ScorchFace(center, radius, new Vector2(min.X, min.Y), new Vector2(max.X, min.Y), -Vector2.UnitY, _pillarHeight);
            ScorchFace(center, radius, new Vector2(min.X, max.Y), new Vector2(max.X, max.Y), Vector2.UnitY, _pillarHeight);
            ScorchFace(center, radius, new Vector2(min.X, min.Y), new Vector2(min.X, max.Y), -Vector2.UnitX, _pillarHeight);
            ScorchFace(center, radius, new Vector2(max.X, min.Y), new Vector2(max.X, max.Y), Vector2.UnitX, _pillarHeight);
        }
    }

    /// <summary>Wall and pillar soot rising up the faces.</summary>
    public void Draw(IRenderer renderer)
    {
        for (var i = 0; i < WallMarkCount; i++)
        {
            ref readonly var mark = ref _marks[i];
            var color = Soot * (WallAlpha * mark.Strength);
            renderer.DrawWallStain(mark.From + mark.Normal * WallOffset, mark.To + mark.Normal * WallOffset, mark.Height, color);
        }
    }

    // A face runs from start to end, with its normal pointing out to the side a blast can be on.
    private void ScorchFace(Vector2 center, float radius, Vector2 start, Vector2 end, Vector2 normal, float faceHeight)
    {
        var distance = Vector2.Dot(center - start, normal);
        if (distance < 0f || distance >= radius)
        {
            return;
        }

        var along = Vector2.Normalize(end - start);
        var faceLength = Vector2.Distance(start, end);
        var at = Vector2.Dot(center - start, along);
        var half = MathF.Sqrt(radius * radius - distance * distance) * WallWidthShare;
        var from = Math.Max(0f, at - half);
        var to = Math.Min(faceLength, at + half);
        if (to <= from)
        {
            return;
        }

        var strength = 1f - distance / radius;
        var height = Math.Min(faceHeight, WallMinHeight + radius * WallHeightPerRadius * strength);
        _marks[_next] = new Mark(start + along * from, start + along * to, normal, height, strength);
        _next = (_next + 1) % MaxWallMarks;
        WallMarkCount = Math.Min(WallMarkCount + 1, MaxWallMarks);
    }

    private readonly record struct Mark(Vector2 From, Vector2 To, Vector2 Normal, float Height, float Strength);
}
