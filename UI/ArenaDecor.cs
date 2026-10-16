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
/// the floor image; basalt wall caps with battlements, obelisk pillars with burning braziers,
/// torches on the wall tops, the fires' flickering light on the floor, the cracks' pulsing heat,
/// the rune circle's slow glow and drifting embers are drawn each frame. Purely visual: nothing in it stands in anyone's way, and no
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
    private const float FlickerSpeed = 11f;
    private const float BrazierEmbersPerSecond = 14f;
    private const float TorchEmbersPerSecond = 4f;

    // Light the fires throw on the floor, and how much it flickers.
    private const float BrazierLightRadius = 430f;
    private const float BrazierLightStrength = 0.22f;
    private const float TorchLightRadius = 210f;
    private const float TorchLightStrength = 0.2f;
    private const float TorchLightReach = 80f;
    private const float LightFlicker = 0.25f;

    // Heat breathing through the cracks, and the rune circle's slow pulse.
    private const float CrackGlowRadius = 34f;
    private const float CrackGlowStrength = 0.3f;
    private const float CrackPulseSpeed = 1.3f;
    private const float CircleGlowRadius = 420f;
    private const float CircleGlowStrength = 0.16f;
    private const float CirclePulseSpeed = 0.7f;
    private const float RuneGlowRadius = 22f;
    private const float RuneWaveSpeed = 1.4f;

    // Battlements along the wall tops, kept off the gates.
    private const float MerlonSpacing = 64f;
    private const float MerlonLength = 30f;
    private const float MerlonHeight = 16f;
    private const float MerlonGateClearance = 30f;

    // Embers drifting up from anywhere on the ground.
    private const float GroundEmbersPerSecond = 30f;

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
    private static readonly Color CrackHeatColor = new(255, 90, 25);
    private static readonly Color CircleHeatColor = new(220, 30, 20);
    private static readonly Color MerlonColor = new(54, 43, 45);

    private readonly Arena _arena;
    private readonly float _wallHeight;
    private readonly float _pillarHeight;
    private readonly List<(Vector2 Position, Vector2 Inward)> _torches = new();
    private readonly List<(Vector2 Position, float Phase)> _crackGlows = new();
    private readonly List<(Vector2 From, Vector2 To)> _merlons = new();
    private readonly GameRandom _emberRandom = new(97);
    private float _time;

    public ArenaDecor(Arena arena, float wallHeight, float pillarHeight)
    {
        _arena = arena;
        _wallHeight = wallHeight;
        _pillarHeight = pillarHeight;
        PlaceTorches();
        PlaceMerlons();
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

        // One spot a frame, so the embers come up all over the ground.
        particles.Embers(RandomFloorPoint(_emberRandom), 2f, GroundEmbersPerSecond, 40f, deltaSeconds);
    }

    /// <summary>
    /// Light on the floor: the fires' flickering pools, heat breathing through the cracks and the
    /// rune circle's slow glow. Drawn right after the floor, under everything standing on it.
    /// </summary>
    public void DrawFloorLight(IRenderer renderer)
    {
        var circlePulse = 0.75f + 0.25f * MathF.Sin(_time * CirclePulseSpeed);
        renderer.FillGlow(_arena.Center, CircleGlowRadius, Light.Of(CircleHeatColor, CircleGlowStrength * circlePulse));

        // A wave of light running round the runes.
        var middle = _arena.Center;
        for (var i = 0; i < Runes; i++)
        {
            var angle = MathHelper.TwoPi * i / Runes;
            var spot = middle + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * ((CircleRadius + InnerCircleRadius) / 2f);
            var wave = 0.5f + 0.5f * MathF.Sin(_time * RuneWaveSpeed - angle * 2f);
            renderer.FillGlow(spot, RuneGlowRadius, Light.Of(RuneColor, 0.15f + 0.45f * wave * wave));
        }

        for (var i = 0; i < _crackGlows.Count; i++)
        {
            var (position, phase) = _crackGlows[i];
            var pulse = 0.55f + 0.45f * MathF.Sin(_time * CrackPulseSpeed + phase);
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

        // Torches on the wall tops, each lighting the floor below it.
        for (var i = 0; i < _torches.Count; i++)
        {
            var (position, inward) = _torches[i];
            var bowl = new Vector2(14f);
            renderer.FillRectRaised(position - bowl / 2f, bowl, _wallHeight + 2f, BrazierColor);
            DrawFlame(renderer, position, _wallHeight + 3f, TorchFlame, i + 7);
        }
    }

    // A flickering flame of soft added light: a wide red glow at the foot, swaying orange tongues
    // and a hot yellow-white core, so it reads as fire rather than stacked discs.
    private void DrawFlame(IRenderer renderer, Vector2 position, float height, float size, int seed)
    {
        var flicker = Flicker(seed);
        var tall = size * (0.85f + 0.3f * flicker);
        var sway = new Vector2(MathF.Sin(_time * 3.1f + seed), MathF.Cos(_time * 2.3f + seed * 0.7f)) * (size * 0.18f);
        renderer.FillGlowRaised(position, height + tall * 0.4f, tall * 2.6f, Light.Of(FlameBase, 0.55f));
        renderer.FillGlowRaised(position + sway, height + tall * 1.0f, tall * 1.6f, Light.Of(FlameMiddle, 0.75f * flicker));
        renderer.FillGlowRaised(position - sway * 0.6f, height + tall * 1.6f, tall * 1.0f, Light.Of(FlameMiddle, 0.6f));
        renderer.FillGlowRaised(position + sway * 1.4f, height + tall * 2.2f, tall * 0.55f, Light.Of(FlameTip, 0.55f * flicker));
        renderer.FillGlowRaised(position, height + tall * 0.7f, tall * 0.7f, Light.Of(FlameTip, 0.9f));
    }

    // 0–1, uneven like a real fire: two waves at odd speeds per fire.
    private float Flicker(int seed)
    {
        var a = MathF.Sin(_time * FlickerSpeed + seed * 1.7f);
        var b = MathF.Sin(_time * FlickerSpeed * 2.37f + seed * 0.9f);
        return 1f - LightFlicker + LightFlicker * (0.5f + 0.3f * a + 0.2f * b);
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
                _crackGlows.Add(((at + next) / 2f, i * 0.9f + s * 0.45f));
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
}
