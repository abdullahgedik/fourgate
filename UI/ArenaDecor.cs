using System;
using System.Collections.Generic;
using Fourgate.Core.Rendering;
using Fourgate.Core.Rng;
using Fourgate.Entities;
using Fourgate.Systems;
using Microsoft.Xna.Framework;

namespace Fourgate.UI;

/// <summary>
/// The Hellgate ruins: a ritual ground before four demon gates. Volcanic flagstones with glowing
/// lava cracks, a rune circle, scorched gate thresholds, dried blood and bones are painted once into
/// the floor image; basalt wall caps, carved obelisk pillars with burning braziers and torches on
/// the wall tops are drawn each frame. Purely visual: nothing in it stands in anyone's way, and no
/// lava lies open where it could be mistaken for a hazard.
/// </summary>
public sealed class ArenaDecor
{
    private const float FlagSize = 200f;
    private const float SeamThickness = 4f;
    private const int LavaCracks = 30;
    private const int BloodStains = 10;
    private const int BoneScatters = 22;
    private const float CircleRadius = 260f;
    private const float InnerCircleRadius = 228f;
    private const int CircleSegments = 72;
    private const int Runes = 24;
    private const float ThresholdScorch = 130f;
    private const float CapLift = 0.6f;
    private const float PillarBandHeight = 14f;
    private const float PillarBandMargin = 4f;
    private const float BrazierSize = 70f;
    private const float BrazierFlame = 26f;
    private const float TorchSpacing = 500f;
    private const float TorchDoorClearance = 320f;
    private const float TorchFlame = 11f;
    private const float TorchFloorReach = 110f;
    private const float TorchFloorRadius = 140f;
    private const float FlickerSpeed = 11f;
    private const float BrazierEmbersPerSecond = 14f;
    private const float TorchEmbersPerSecond = 4f;

    private static readonly Color SeamColor = new Color(0, 0, 0) * 0.45f;
    private static readonly Color LavaGlowColor = new Color(255, 80, 20) * 0.16f;
    private static readonly Color LavaCoreColor = new Color(255, 130, 40) * 0.8f;
    private static readonly Color CircleColor = new Color(190, 35, 25) * 0.55f;
    private static readonly Color RuneColor = new Color(240, 100, 40) * 0.6f;
    private static readonly Color ScorchColor = new Color(0, 0, 0) * 0.45f;
    private static readonly Color DriedBloodColor = new Color(70, 12, 10) * 0.45f;
    private static readonly Color BoneColor = new Color(205, 195, 175) * 0.65f;
    private static readonly Color CapColor = new(46, 36, 38);
    private static readonly Color IronColor = new(38, 34, 36);
    private static readonly Color BrazierColor = new(30, 26, 28);
    private static readonly Color FlameBase = new(210, 50, 15);
    private static readonly Color FlameMiddle = new(255, 130, 40);
    private static readonly Color FlameTip = new(255, 230, 140);
    private static readonly Color FireLightColor = new(255, 110, 40);

    private readonly Arena _arena;
    private readonly float _wallHeight;
    private readonly float _pillarHeight;
    private readonly List<(Vector2 Position, Vector2 Inward)> _torches = new();
    private float _time;

    public ArenaDecor(Arena arena, float wallHeight, float pillarHeight)
    {
        _arena = arena;
        _wallHeight = wallHeight;
        _pillarHeight = pillarHeight;
        PlaceTorches();
    }

    /// <summary>Paints the floor's look into the floor image; call once, before anything else is spilled on it.</summary>
    public void Paint(FloorStains floor, GameRandom random)
    {
        PaintFlagstones(floor, random);
        PaintLavaCracks(floor, random);
        PaintRitualCircle(floor);
        PaintGateThresholds(floor);
        PaintBloodAndBones(floor, random);
    }

    /// <summary>Flickers the fires and lets them throw embers.</summary>
    public void Update(float deltaSeconds, ParticleSystem particles)
    {
        _time += deltaSeconds;
        var pillars = _arena.Pillars;
        for (var i = 0; i < pillars.Count; i++)
        {
            particles.Embers(pillars[i].Center, _pillarHeight + BrazierFlame, BrazierEmbersPerSecond, 3f, deltaSeconds);
        }

        for (var i = 0; i < _torches.Count; i++)
        {
            particles.Embers(_torches[i].Position, _wallHeight + TorchFlame, TorchEmbersPerSecond, 2f, deltaSeconds);
        }
    }

    /// <summary>Wall caps, obelisk pillars with their braziers, and the wall-top torches.</summary>
    public void Draw(IRenderer renderer)
    {
        var left = _arena.Left;
        var top = _arena.Top;
        var size = _arena.Size;
        var wall = _arena.WallThickness;

        var capHeight = _wallHeight + CapLift;
        renderer.FillRectRaised(new Vector2(left - wall, top - wall), new Vector2(size + wall * 2f, wall), capHeight, CapColor);
        renderer.FillRectRaised(new Vector2(left - wall, top + size), new Vector2(size + wall * 2f, wall), capHeight, CapColor);
        renderer.FillRectRaised(new Vector2(left - wall, top), new Vector2(wall, size), capHeight, CapColor);
        renderer.FillRectRaised(new Vector2(left + size, top), new Vector2(wall, size), capHeight, CapColor);

        // Obelisks: an iron band round the foot, a dark cap, and a brazier burning on top.
        var pillars = _arena.Pillars;
        for (var i = 0; i < pillars.Count; i++)
        {
            var corner = pillars[i].Center - pillars[i].HalfSize;
            var extent = pillars[i].HalfSize * 2f;
            var margin = new Vector2(PillarBandMargin);
            renderer.FillBox(corner - margin, extent + margin * 2f, PillarBandHeight, IronColor, castShadow: false);
            renderer.FillRectRaised(corner, extent, _pillarHeight + CapLift, CapColor);
            var bowl = new Vector2(BrazierSize);
            renderer.FillRectRaised(pillars[i].Center - bowl / 2f, bowl, _pillarHeight + 1.2f, BrazierColor);
            DrawFlame(renderer, pillars[i].Center, _pillarHeight + 2f, BrazierFlame, i);
            renderer.FillCircle(pillars[i].Center, pillars[i].HalfSize.X * 1.9f, FireLightColor * 0.05f);
        }

        // Torches on the wall tops, each lighting the floor below it.
        for (var i = 0; i < _torches.Count; i++)
        {
            var (position, inward) = _torches[i];
            var bowl = new Vector2(14f);
            renderer.FillRectRaised(position - bowl / 2f, bowl, _wallHeight + 2f, BrazierColor);
            DrawFlame(renderer, position, _wallHeight + 3f, TorchFlame, i + 7);
            renderer.FillCircle(position + inward * TorchFloorReach, TorchFloorRadius, FireLightColor * 0.07f);
        }
    }

    // A flickering flame: glowing sprites stacked up, wide and red at the foot, small and yellow at the tip.
    private void DrawFlame(IRenderer renderer, Vector2 position, float height, float size, int seed)
    {
        var flicker = 0.8f + 0.2f * MathF.Sin(_time * FlickerSpeed + seed * 1.7f);
        var tall = size * flicker;
        renderer.FillCircleRaised(position, height + tall * 0.5f, tall * 1.5f, FlameBase * 0.45f);
        renderer.FillCircleRaised(position, height + tall * 0.9f, tall * 0.9f, FlameMiddle * 0.7f);
        renderer.FillCircleRaised(position, height + tall * 1.5f, tall * 0.5f, FlameTip * 0.9f);
    }

    // Volcanic flagstones in slightly different dark tones, with deep seams.
    private void PaintFlagstones(FloorStains floor, GameRandom random)
    {
        var count = (int)MathF.Ceiling(_arena.Size / FlagSize);
        for (var x = 0; x < count; x++)
        {
            for (var y = 0; y < count; y++)
            {
                var center = new Vector2(_arena.Left + (x + 0.5f) * FlagSize, _arena.Top + (y + 0.5f) * FlagSize);
                var tone = random.Range(-1f, 1f);
                var color = tone >= 0f ? new Color(255, 200, 190) * (0.035f * tone) : Color.Black * (0.15f * -tone);
                floor.AddRect(center, new Vector2(FlagSize - SeamThickness), 0f, color);
            }
        }

        for (var i = 1; i < count; i++)
        {
            var offset = i * FlagSize;
            floor.AddLine(
                new Vector2(_arena.Left + offset, _arena.Top), new Vector2(_arena.Left + offset, _arena.Bottom), SeamThickness, SeamColor);
            floor.AddLine(
                new Vector2(_arena.Left, _arena.Top + offset), new Vector2(_arena.Right, _arena.Top + offset), SeamThickness, SeamColor);
        }
    }

    // Cracks glowing with the heat below: a wide faint glow under a thin bright core.
    private void PaintLavaCracks(FloorStains floor, GameRandom random)
    {
        for (var i = 0; i < LavaCracks; i++)
        {
            var at = RandomFloorPoint(random);
            var heading = random.Range(0f, MathHelper.TwoPi);
            var segments = 3 + random.NextIndex(5);
            for (var s = 0; s < segments; s++)
            {
                heading += random.Range(-0.8f, 0.8f);
                var next = at + new Vector2(MathF.Cos(heading), MathF.Sin(heading)) * random.Range(20f, 60f);
                floor.AddLine(at, next, random.Range(7f, 10f), LavaGlowColor);
                floor.AddLine(at, next, random.Range(1.6f, 2.6f), LavaCoreColor);
                at = next;
            }
        }
    }

    // A rune circle in the middle of the ground, with four lines reaching toward the gates.
    private void PaintRitualCircle(FloorStains floor)
    {
        var middle = _arena.Center;
        Ring(floor, middle, CircleRadius, 5f, CircleColor);
        Ring(floor, middle, InnerCircleRadius, 3f, CircleColor);

        for (var i = 0; i < Runes; i++)
        {
            var angle = MathHelper.TwoPi * i / Runes;
            var along = new Vector2(MathF.Cos(angle), MathF.Sin(angle));
            var spot = middle + along * ((CircleRadius + InnerCircleRadius) / 2f);
            floor.AddRect(spot, new Vector2(i % 3 == 0 ? 14f : 9f, 4f), angle + MathHelper.PiOver2, RuneColor);
            floor.AddRect(spot, new Vector2(3f, 10f), angle + MathHelper.PiOver2, RuneColor);
        }

        var doors = _arena.Doors;
        for (var i = 0; i < doors.Count; i++)
        {
            var toward = Vector2.Normalize(doors[i].Center - middle);
            floor.AddLine(middle + toward * CircleRadius, middle + toward * (CircleRadius + 420f), 3f, CircleColor * 0.7f);
        }
    }

    // Scorched ground before each gate, with glowing runes across the threshold.
    private void PaintGateThresholds(FloorStains floor)
    {
        var doors = _arena.Doors;
        for (var i = 0; i < doors.Count; i++)
        {
            ArenaView.DoorInnerEdge(doors[i], out var from, out var to);
            var inward = doors[i].Inward;
            var middle = (from + to) / 2f;
            floor.Add(middle + inward * (ThresholdScorch * 0.4f), ThresholdScorch, ScorchColor);
            var along = to - from;
            var rotation = MathF.Atan2(along.Y, along.X);
            for (var r = 1; r <= 5; r++)
            {
                var spot = Vector2.Lerp(from, to, r / 6f) + inward * 22f;
                floor.AddRect(spot, new Vector2(12f, 4f), rotation, RuneColor);
                floor.AddRect(spot, new Vector2(4f, 12f), rotation, RuneColor);
            }
        }
    }

    // Old dried blood, and scattered bones of those who came before.
    private void PaintBloodAndBones(FloorStains floor, GameRandom random)
    {
        for (var i = 0; i < BloodStains; i++)
        {
            var at = RandomFloorPoint(random);
            var radius = random.Range(25f, 60f);
            floor.Add(at, radius, DriedBloodColor);
            floor.Add(at + new Vector2(random.Range(-0.6f, 0.6f), random.Range(-0.6f, 0.6f)) * radius, radius * 0.6f, DriedBloodColor);
        }

        for (var i = 0; i < BoneScatters; i++)
        {
            var at = RandomFloorPoint(random);
            var bones = 2 + random.NextIndex(3);
            for (var b = 0; b < bones; b++)
            {
                var spot = at + new Vector2(random.Range(-14f, 14f), random.Range(-14f, 14f));
                floor.AddRect(spot, new Vector2(random.Range(8f, 14f), 2.6f), random.Range(0f, MathHelper.Pi), BoneColor);
            }
        }
    }

    private static void Ring(FloorStains floor, Vector2 center, float radius, float thickness, Color color)
    {
        var previous = center + new Vector2(radius, 0f);
        for (var i = 1; i <= CircleSegments; i++)
        {
            var angle = MathHelper.TwoPi * i / CircleSegments;
            var point = center + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * radius;
            floor.AddLine(previous, point, thickness, color);
            previous = point;
        }
    }

    private Vector2 RandomFloorPoint(GameRandom random)
    {
        return new Vector2(
            random.Range(_arena.Left + 80f, _arena.Right - 80f), random.Range(_arena.Top + 80f, _arena.Bottom - 80f));
    }

    // Torches along the middle of each wall top, spaced out and kept clear of the gates.
    private void PlaceTorches()
    {
        var half = _arena.WallThickness / 2f;
        AddTorchRow(new Vector2(_arena.Left, _arena.Top - half), Vector2.UnitX, Vector2.UnitY);
        AddTorchRow(new Vector2(_arena.Left, _arena.Bottom + half), Vector2.UnitX, -Vector2.UnitY);
        AddTorchRow(new Vector2(_arena.Left - half, _arena.Top), Vector2.UnitY, Vector2.UnitX);
        AddTorchRow(new Vector2(_arena.Right + half, _arena.Top), Vector2.UnitY, -Vector2.UnitX);
    }

    private void AddTorchRow(Vector2 start, Vector2 along, Vector2 inward)
    {
        for (var t = TorchSpacing / 2f; t < _arena.Size; t += TorchSpacing)
        {
            var position = start + along * t;
            if (!NearAGate(position))
            {
                _torches.Add((position, inward));
            }
        }
    }

    private bool NearAGate(Vector2 position)
    {
        var doors = _arena.Doors;
        for (var i = 0; i < doors.Count; i++)
        {
            if (Vector2.Distance(doors[i].Center, position) < TorchDoorClearance)
            {
                return true;
            }
        }

        return false;
    }
}
