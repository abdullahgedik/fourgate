using System;
using System.Collections.Generic;
using Fourgate.Core.Rendering;
using Fourgate.Core.Rng;
using Fourgate.Entities;
using Fourgate.Systems;
using Microsoft.Xna.Framework;

namespace Fourgate.UI;

/// <summary>
/// The Hellgate ruins: a ritual ground before four demon gates. Sharp detail is real geometry
/// drawn each frame, so it stays crisp at any zoom: flagstones laid on the floor grid (the grid
/// lines are their joints), faintly glowing cracks, a rune circle, runes across the gate
/// thresholds, scattered bones, walls and obelisks built of stone blocks, battlements, braziers
/// and torches (embers rise from the fires only). Only soft things (old blood, scorched thresholds) are painted into the floor
/// image. The fires' light on the floor, the cracks' heat and the runes' glow are kept low so
/// the ground stays readable. Purely visual: nothing in it stands in anyone's way.
/// </summary>
public sealed class ArenaDecor
{
    private const float SeamThickness = 2f;
    private const float TonedTileShare = 0.45f;
    private const int LavaCracks = 18;
    private const int BloodStains = 10;
    private const int BoneScatters = 22;
    private const float CircleRadius = 260f;
    private const float InnerCircleRadius = 228f;
    private const int CircleSegments = 96;
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
    private const float FlickerSpeed = 11f;
    private const float BrazierEmbersPerSecond = 8f;
    private const float TorchEmbersPerSecond = 2f;

    // Light the fires throw on the floor, and how much it flickers.
    private const float BrazierLightRadius = 400f;
    private const float BrazierLightStrength = 0.11f;
    private const float TorchLightRadius = 190f;
    private const float TorchLightStrength = 0.09f;
    private const float TorchLightReach = 80f;
    private const float LightFlicker = 0.2f;

    // Heat breathing through the cracks, and the rune circle's slow pulse.
    private const float CrackGlowRadius = 24f;
    private const float CrackGlowStrength = 0.12f;
    private const float CrackPulseSpeed = 1.1f;
    private const float CircleGlowRadius = 380f;
    private const float CircleGlowStrength = 0.06f;
    private const float CirclePulseSpeed = 0.6f;
    private const float RuneGlowRadius = 16f;
    private const float RuneWaveSpeed = 1.2f;

    // Stone blocks: length along the wall, the joints between them, rows up the wall.
    private const float BlockLength = 120f;
    private const float BlockJoint = 3f;
    private const int WallCourses = 3;
    private const int PillarCourses = 4;
    private const float CourseJoint = 2.5f;
    private const float BlockProud = 1f;
    private const float BlockToneSpread = 0.1f;

    // Battlements along the wall tops, kept off the gates.
    private const float MerlonSpacing = 64f;
    private const float MerlonLength = 30f;
    private const float MerlonHeight = 16f;
    private const float MerlonGateClearance = 30f;

    private static readonly Color SeamColor = new Color(0, 0, 0) * 0.4f;
    private static readonly Color LightTileColor = new Color(255, 205, 195) * 0.035f;
    private static readonly Color DarkTileColor = new Color(0, 0, 0) * 0.16f;
    private static readonly Color CrackShadowColor = new Color(10, 0, 0) * 0.55f;
    private static readonly Color CrackCoreColor = new(150, 45, 18);
    private static readonly Color CircleColor = new Color(150, 30, 22) * 0.75f;
    private static readonly Color RuneColor = new(175, 60, 30);
    private static readonly Color ScorchColor = new Color(0, 0, 0) * 0.45f;
    private static readonly Color DriedBloodColor = new Color(70, 12, 10) * 0.45f;
    private static readonly Color BoneColor = new(176, 166, 148);
    private static readonly Color CapColor = new(46, 36, 38);
    private static readonly Color IronColor = new(38, 34, 36);
    private static readonly Color BrazierColor = new(30, 26, 28);
    private static readonly Color StoneColor = new(70, 57, 58);
    private static readonly Color FlameBase = new(210, 50, 15);
    private static readonly Color FlameMiddle = new(255, 130, 40);
    private static readonly Color FlameTip = new(255, 230, 140);
    private static readonly Color FireLightColor = new(255, 110, 40);
    private static readonly Color CrackHeatColor = new(255, 90, 25);
    private static readonly Color CircleHeatColor = new(220, 30, 20);
    private static readonly Color MerlonColor = new(54, 43, 45);

    private readonly Arena _arena;
    private readonly float _wallHeight;
    private readonly float _pillarHeight;
    private readonly float _cellSize;
    private readonly List<(Vector2 Position, Vector2 Inward)> _torches = new();
    private readonly List<(Vector2 Position, float Phase)> _crackGlows = new();
    private readonly List<(Vector2 From, Vector2 To)> _merlons = new();
    private readonly List<(Vector2 TopLeft, Color Color)> _tiles = new();
    private readonly List<Stroke> _strokes = new();
    private readonly List<Block> _blocks = new();
    private float _time;

    /// <param name="cellSize">The floor grid's spacing; flagstones are laid on it.</param>
    public ArenaDecor(Arena arena, float wallHeight, float pillarHeight, float cellSize)
    {
        _arena = arena;
        _wallHeight = wallHeight;
        _pillarHeight = pillarHeight;
        _cellSize = cellSize;
        PlaceTorches();
        PlaceMerlons();
    }

    /// <summary>
    /// Lays out the ground and the stonework (varied from run to run) and paints the soft marks
    /// into the floor image; call once, before anything else is spilled on it.
    /// </summary>
    public void Paint(FloorStains floor, GameRandom random)
    {
        LayFlagstones(random);
        LayCracks(random);
        LayRitualCircle();
        LayGateThresholds(floor);
        LayBloodAndBones(floor, random);
        BuildStonework(random);
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

    /// <summary>
    /// The ground's sharp detail: flagstone tones, the grid joints, cracks, the rune circle,
    /// threshold runes and bones. Drawn on the bare floor, under the stains.
    /// </summary>
    public void DrawFloorDetail(IRenderer renderer)
    {
        var tile = new Vector2(_cellSize - SeamThickness);
        for (var i = 0; i < _tiles.Count; i++)
        {
            renderer.FillRect(_tiles[i].TopLeft, tile, _tiles[i].Color);
        }

        for (var offset = _cellSize; offset < _arena.Size; offset += _cellSize)
        {
            renderer.FillRect(
                new Vector2(_arena.Left + offset - SeamThickness / 2f, _arena.Top), new Vector2(SeamThickness, _arena.Size),
                SeamColor);
            renderer.FillRect(
                new Vector2(_arena.Left, _arena.Top + offset - SeamThickness / 2f), new Vector2(_arena.Size, SeamThickness),
                SeamColor);
        }

        for (var i = 0; i < _strokes.Count; i++)
        {
            var stroke = _strokes[i];
            renderer.DrawLine(stroke.From, stroke.To, stroke.Thickness, stroke.Color);
        }
    }

    /// <summary>The walls' and obelisks' stone blocks, set a hair proud of the mortar behind them.</summary>
    public void DrawStonework(IRenderer renderer)
    {
        for (var i = 0; i < _blocks.Count; i++)
        {
            var block = _blocks[i];
            renderer.DrawBar(block.From, block.To, block.Thickness, block.Bottom, block.Top, block.Color);
        }
    }

    /// <summary>
    /// Light on the floor: the fires' flickering pools, faint heat in the cracks and the rune
    /// circle's slow glow. Drawn after the stains, under everything standing on the floor.
    /// </summary>
    public void DrawFloorLight(IRenderer renderer)
    {
        var circlePulse = 0.75f + 0.25f * MathF.Sin(_time * CirclePulseSpeed);
        renderer.FillGlow(_arena.Center, CircleGlowRadius, Light.Of(CircleHeatColor, CircleGlowStrength * circlePulse));

        // A wave of light running slowly round the runes.
        var middle = _arena.Center;
        for (var i = 0; i < Runes; i++)
        {
            var angle = MathHelper.TwoPi * i / Runes;
            var spot = middle + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * ((CircleRadius + InnerCircleRadius) / 2f);
            var wave = 0.5f + 0.5f * MathF.Sin(_time * RuneWaveSpeed - angle * 2f);
            renderer.FillGlow(spot, RuneGlowRadius, Light.Of(RuneColor, 0.3f * wave * wave));
        }

        for (var i = 0; i < _crackGlows.Count; i++)
        {
            var (position, phase) = _crackGlows[i];
            var pulse = 0.5f + 0.5f * MathF.Sin(_time * CrackPulseSpeed + phase);
            renderer.FillGlow(position, CrackGlowRadius, Light.Of(CrackHeatColor, CrackGlowStrength * pulse));
        }

        var pillars = _arena.Pillars;
        for (var i = 0; i < pillars.Count; i++)
        {
            renderer.FillGlow(
                pillars[i].Center, BrazierLightRadius, Light.Of(FireLightColor, BrazierLightStrength * Flicker(i)));
        }

        for (var i = 0; i < _torches.Count; i++)
        {
            var (position, inward) = _torches[i];
            renderer.FillGlow(
                position + inward * TorchLightReach, TorchLightRadius,
                Light.Of(FireLightColor, TorchLightStrength * Flicker(i + 7)));
        }
    }

    /// <summary>Wall caps and battlements, obelisk tops with their braziers, and the wall-top torches.</summary>
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
        for (var i = 0; i < _merlons.Count; i++)
        {
            renderer.DrawBar(_merlons[i].From, _merlons[i].To, wall, _wallHeight, _wallHeight + MerlonHeight, MerlonColor);
        }

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
        }

        for (var i = 0; i < _torches.Count; i++)
        {
            var position = _torches[i].Position;
            var bowl = new Vector2(14f);
            renderer.FillRectRaised(position - bowl / 2f, bowl, _wallHeight + 2f, BrazierColor);
            DrawFlame(renderer, position, _wallHeight + 3f, TorchFlame, i + 7);
        }
    }

    // A flickering flame of soft added light: a red glow at the foot, a swaying orange tongue
    // and a small hot core.
    private void DrawFlame(IRenderer renderer, Vector2 position, float height, float size, int seed)
    {
        var flicker = Flicker(seed);
        var tall = size * (0.85f + 0.3f * flicker);
        var sway = new Vector2(MathF.Sin(_time * 3.1f + seed), MathF.Cos(_time * 2.3f + seed * 0.7f)) * (size * 0.15f);
        renderer.FillGlowRaised(position, height + tall * 0.4f, tall * 1.9f, Light.Of(FlameBase, 0.4f));
        renderer.FillGlowRaised(position + sway, height + tall * 1.0f, tall * 1.3f, Light.Of(FlameMiddle, 0.55f * flicker));
        renderer.FillGlowRaised(position - sway * 0.6f, height + tall * 1.7f, tall * 0.6f, Light.Of(FlameTip, 0.4f * flicker));
        renderer.FillGlowRaised(position, height + tall * 0.7f, tall * 0.55f, Light.Of(FlameTip, 0.7f));
    }

    // Between 1 - LightFlicker and 1, uneven like a real fire: two waves at odd speeds per fire.
    private float Flicker(int seed)
    {
        var a = MathF.Sin(_time * FlickerSpeed + seed * 1.7f);
        var b = MathF.Sin(_time * FlickerSpeed * 2.37f + seed * 0.9f);
        return 1f - LightFlicker + LightFlicker * (0.5f + 0.3f * a + 0.2f * b);
    }

    // Flagstones on the grid cells: some a shade lighter, some darker, most plain.
    private void LayFlagstones(GameRandom random)
    {
        var count = (int)MathF.Ceiling(_arena.Size / _cellSize);
        var half = SeamThickness / 2f;
        for (var x = 0; x < count; x++)
        {
            for (var y = 0; y < count; y++)
            {
                if (random.Range(0f, 1f) > TonedTileShare)
                {
                    continue;
                }

                var tone = random.Range(-1f, 1f);
                var color = tone >= 0f ? LightTileColor * tone : DarkTileColor * -tone;
                _tiles.Add((new Vector2(_arena.Left + x * _cellSize + half, _arena.Top + y * _cellSize + half), color));
            }
        }
    }

    // Thin cracks, darkened along their sides, with a dull ember line in them; every other bend
    // breathes a little heat.
    private void LayCracks(GameRandom random)
    {
        for (var i = 0; i < LavaCracks; i++)
        {
            var at = RandomFloorPoint(random);
            var heading = random.Range(0f, MathHelper.TwoPi);
            var segments = 3 + random.NextIndex(5);
            for (var s = 0; s < segments; s++)
            {
                heading += random.Range(-0.8f, 0.8f);
                var next = at + new Vector2(MathF.Cos(heading), MathF.Sin(heading)) * random.Range(20f, 55f);
                _strokes.Add(new Stroke(at, next, random.Range(4f, 6f), CrackShadowColor));
                _strokes.Add(new Stroke(at, next, random.Range(1.2f, 1.8f), CrackCoreColor));
                if (s % 2 == 0)
                {
                    _crackGlows.Add(((at + next) / 2f, i * 0.9f + s * 0.45f));
                }

                at = next;
            }
        }
    }

    // A rune circle in the middle of the ground, with four lines reaching toward the gates.
    private void LayRitualCircle()
    {
        var middle = _arena.Center;
        Ring(middle, CircleRadius, 4f, CircleColor);
        Ring(middle, InnerCircleRadius, 2.5f, CircleColor);

        for (var i = 0; i < Runes; i++)
        {
            var angle = MathHelper.TwoPi * i / Runes;
            var along = new Vector2(MathF.Cos(angle), MathF.Sin(angle));
            var side = new Vector2(-along.Y, along.X);
            var spot = middle + along * ((CircleRadius + InnerCircleRadius) / 2f);
            var width = i % 3 == 0 ? 7f : 4.5f;
            _strokes.Add(new Stroke(spot - side * width, spot + side * width, 3f, RuneColor));
            _strokes.Add(new Stroke(spot - along * 5f, spot + along * 5f, 2.5f, RuneColor));
        }

        var doors = _arena.Doors;
        for (var i = 0; i < doors.Count; i++)
        {
            var toward = Vector2.Normalize(doors[i].Center - middle);
            _strokes.Add(new Stroke(middle + toward * CircleRadius, middle + toward * (CircleRadius + 420f), 2.5f, CircleColor * 0.7f));
        }
    }

    // Scorched ground before each gate (soft, painted) with runes across the threshold (sharp).
    private void LayGateThresholds(FloorStains floor)
    {
        var doors = _arena.Doors;
        for (var i = 0; i < doors.Count; i++)
        {
            ArenaView.DoorInnerEdge(doors[i], out var from, out var to);
            var inward = doors[i].Inward;
            var middle = (from + to) / 2f;
            floor.Add(middle + inward * (ThresholdScorch * 0.4f), ThresholdScorch, ScorchColor);
            var along = Vector2.Normalize(to - from);
            for (var r = 1; r <= 5; r++)
            {
                var spot = Vector2.Lerp(from, to, r / 6f) + inward * 22f;
                _strokes.Add(new Stroke(spot - along * 6f, spot + along * 6f, 3f, RuneColor));
                _strokes.Add(new Stroke(spot - inward * 6f, spot + inward * 6f, 3f, RuneColor));
            }
        }
    }

    // Old dried blood (soft, painted) and scattered bones (sharp).
    private void LayBloodAndBones(FloorStains floor, GameRandom random)
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
                var angle = random.Range(0f, MathHelper.Pi);
                var half = new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * random.Range(4f, 7f);
                _strokes.Add(new Stroke(spot - half, spot + half, 2.4f, BoneColor));
            }
        }
    }

    // Walls in rows of blocks (each row shifted half a block, like laid stone) and obelisks in
    // stacked courses, each block a slightly different shade.
    private void BuildStonework(GameRandom random)
    {
        var wall = _arena.WallThickness;
        var half = wall / 2f;
        var length = _arena.Size + wall * 2f;
        AddWallBlocks(new Vector2(_arena.Left - wall, _arena.Top - half), Vector2.UnitX, length, wall, random);
        AddWallBlocks(new Vector2(_arena.Left - wall, _arena.Bottom + half), Vector2.UnitX, length, wall, random);
        AddWallBlocks(new Vector2(_arena.Left - half, _arena.Top), Vector2.UnitY, _arena.Size, wall, random);
        AddWallBlocks(new Vector2(_arena.Right + half, _arena.Top), Vector2.UnitY, _arena.Size, wall, random);

        var pillars = _arena.Pillars;
        var courseHeight = (_pillarHeight - PillarBandHeight) / PillarCourses;
        for (var i = 0; i < pillars.Count; i++)
        {
            var center = pillars[i].Center;
            var halfSize = pillars[i].HalfSize;
            for (var c = 0; c < PillarCourses; c++)
            {
                var bottom = PillarBandHeight + c * courseHeight + CourseJoint / 2f;
                _blocks.Add(new Block(
                    center - new Vector2(halfSize.X + BlockProud, 0f), center + new Vector2(halfSize.X + BlockProud, 0f),
                    halfSize.Y * 2f + BlockProud * 2f, bottom, bottom + courseHeight - CourseJoint, Shade(random)));
            }
        }
    }

    private void AddWallBlocks(Vector2 start, Vector2 along, float length, float wall, GameRandom random)
    {
        var courseHeight = _wallHeight / WallCourses;
        for (var c = 0; c < WallCourses; c++)
        {
            var bottom = c * courseHeight + (c == 0 ? 0f : CourseJoint / 2f);
            var top = (c + 1) * courseHeight - (c == WallCourses - 1 ? 0f : CourseJoint / 2f);
            var shift = c % 2 == 0 ? 0f : -BlockLength / 2f;
            for (var t = shift; t < length; t += BlockLength)
            {
                var from = Math.Max(0f, t) + BlockJoint / 2f;
                var to = Math.Min(length, t + BlockLength) - BlockJoint / 2f;
                if (to - from < BlockJoint)
                {
                    continue;
                }

                _blocks.Add(new Block(
                    start + along * from, start + along * to, wall + BlockProud * 2f, bottom, top, Shade(random)));
            }
        }
    }

    private static Color Shade(GameRandom random)
    {
        var tone = 1f + random.Range(-BlockToneSpread, BlockToneSpread);
        return new Color(Vector3.Clamp(StoneColor.ToVector3() * tone, Vector3.Zero, Vector3.One));
    }

    private void Ring(Vector2 center, float radius, float thickness, Color color)
    {
        var previous = center + new Vector2(radius, 0f);
        for (var i = 1; i <= CircleSegments; i++)
        {
            var angle = MathHelper.TwoPi * i / CircleSegments;
            var point = center + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * radius;
            _strokes.Add(new Stroke(previous, point, thickness, color));
            previous = point;
        }
    }

    private Vector2 RandomFloorPoint(GameRandom random)
    {
        return new Vector2(
            random.Range(_arena.Left + 80f, _arena.Right - 80f), random.Range(_arena.Top + 80f, _arena.Bottom - 80f));
    }

    // Battlements: short raised blocks along every wall top, with gaps between, none over a gate.
    private void PlaceMerlons()
    {
        var half = _arena.WallThickness / 2f;
        AddMerlonRow(new Vector2(_arena.Left - half * 2f, _arena.Top - half), Vector2.UnitX);
        AddMerlonRow(new Vector2(_arena.Left - half * 2f, _arena.Bottom + half), Vector2.UnitX);
        AddMerlonRow(new Vector2(_arena.Left - half, _arena.Top - half * 2f), Vector2.UnitY);
        AddMerlonRow(new Vector2(_arena.Right + half, _arena.Top - half * 2f), Vector2.UnitY);
    }

    private void AddMerlonRow(Vector2 start, Vector2 along)
    {
        var length = _arena.Size + _arena.WallThickness * 2f;
        for (var t = MerlonSpacing / 2f; t < length; t += MerlonSpacing)
        {
            var middle = start + along * t;
            if (!OverAGate(middle))
            {
                _merlons.Add((middle - along * (MerlonLength / 2f), middle + along * (MerlonLength / 2f)));
            }
        }
    }

    private bool OverAGate(Vector2 position)
    {
        var doors = _arena.Doors;
        for (var i = 0; i < doors.Count; i++)
        {
            var min = doors[i].TopLeft - new Vector2(MerlonGateClearance);
            var max = doors[i].TopLeft + doors[i].Size + new Vector2(MerlonGateClearance);
            if (position.X >= min.X && position.X <= max.X && position.Y >= min.Y && position.Y <= max.Y)
            {
                return true;
            }
        }

        return false;
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

    // A straight mark on the floor.
    private readonly record struct Stroke(Vector2 From, Vector2 To, float Thickness, Color Color);

    // One stone block: a bar from one end to the other, this thick, between two heights.
    private readonly record struct Block(Vector2 From, Vector2 To, float Thickness, float Bottom, float Top, Color Color);
}
