using System;
using System.Collections.Generic;
using Fourgate.Core.Rendering;
using Fourgate.Core.Rng;
using Fourgate.Entities;
using Microsoft.Xna.Framework;

namespace Fourgate.UI;

/// <summary>
/// Dressing that makes the arena look like a place (a neutral concrete yard): concrete slabs,
/// seams, cracks, oil stains, hazard paint at the doors and drain grates painted once into the
/// floor image; trim on the walls and pillars and lamps on the wall tops drawn each frame. Purely
/// visual, and nothing in it stands in anyone's way.
/// </summary>
public sealed class ArenaDecor
{
    private const float SlabSize = 300f;
    private const float SeamThickness = 3f;
    private const int Cracks = 26;
    private const int OilStains = 12;
    private const float HazardDepth = 16f;
    private const float HazardInset = 14f;
    private const float HazardDash = 36f;
    private const float HazardGap = 24f;
    private const float BaseboardHeight = 8f;
    private const float BaseboardDepth = 3f;
    private const float CapLift = 0.6f;
    private const float PillarBandHeight = 10f;
    private const float PillarBandMargin = 3f;
    private const float LampSpacing = 500f;
    private const float LampDoorClearance = 320f;
    private const float LampFloorReach = 110f;
    private const float LampFloorRadius = 130f;

    private static readonly Color SeamColor = new Color(0, 0, 0) * 0.35f;
    private static readonly Color CrackColor = new Color(0, 0, 0) * 0.4f;
    private static readonly Color OilColor = new Color(10, 8, 6) * 0.35f;
    private static readonly Color HazardColor = new Color(225, 185, 45) * 0.55f;
    private static readonly Color DrainColor = new Color(0, 0, 0) * 0.5f;
    private static readonly Color DrainBarColor = new Color(255, 255, 255) * 0.07f;
    private static readonly Color BaseboardColor = new(48, 51, 63);
    private static readonly Color CapColor = new(92, 97, 115);
    private static readonly Color PillarBandColor = new(200, 165, 45);
    private static readonly Color LampHousingColor = new(45, 45, 50);
    private static readonly Color LampColor = new(255, 214, 150);

    private readonly Arena _arena;
    private readonly float _wallHeight;
    private readonly float _pillarHeight;
    private readonly List<(Vector2 Position, Vector2 Inward)> _lamps = new();

    public ArenaDecor(Arena arena, float wallHeight, float pillarHeight)
    {
        _arena = arena;
        _wallHeight = wallHeight;
        _pillarHeight = pillarHeight;
        PlaceLamps();
    }

    /// <summary>Paints the floor's look into the floor image; call once, before anything else is spilled on it.</summary>
    public void Paint(FloorStains floor, GameRandom random)
    {
        PaintSlabs(floor, random);
        PaintCracks(floor, random);
        PaintOil(floor, random);
        PaintHazardDashes(floor);
        PaintDrains(floor);
    }

    /// <summary>Wall and pillar trim and the wall-top lamps.</summary>
    public void Draw(IRenderer renderer)
    {
        var left = _arena.Left;
        var top = _arena.Top;
        var size = _arena.Size;
        var wall = _arena.WallThickness;

        // A dark baseboard along the foot of each wall, and a lighter cap along the top.
        renderer.FillBox(new Vector2(left, top), new Vector2(size, BaseboardDepth), BaseboardHeight, BaseboardColor, castShadow: false);
        renderer.FillBox(new Vector2(left, top + size - BaseboardDepth), new Vector2(size, BaseboardDepth), BaseboardHeight, BaseboardColor, castShadow: false);
        renderer.FillBox(new Vector2(left, top), new Vector2(BaseboardDepth, size), BaseboardHeight, BaseboardColor, castShadow: false);
        renderer.FillBox(new Vector2(left + size - BaseboardDepth, top), new Vector2(BaseboardDepth, size), BaseboardHeight, BaseboardColor, castShadow: false);
        var capHeight = _wallHeight + CapLift;
        renderer.FillRectRaised(new Vector2(left - wall, top - wall), new Vector2(size + wall * 2f, wall), capHeight, CapColor);
        renderer.FillRectRaised(new Vector2(left - wall, top + size), new Vector2(size + wall * 2f, wall), capHeight, CapColor);
        renderer.FillRectRaised(new Vector2(left - wall, top), new Vector2(wall, size), capHeight, CapColor);
        renderer.FillRectRaised(new Vector2(left + size, top), new Vector2(wall, size), capHeight, CapColor);

        // Pillars: a lighter cap and a yellow hazard band round the foot.
        var pillars = _arena.Pillars;
        for (var i = 0; i < pillars.Count; i++)
        {
            var corner = pillars[i].Center - pillars[i].HalfSize;
            var extent = pillars[i].HalfSize * 2f;
            var margin = new Vector2(PillarBandMargin);
            renderer.FillBox(corner - margin, extent + margin * 2f, PillarBandHeight, PillarBandColor, castShadow: false);
            renderer.FillRectRaised(corner, extent, _pillarHeight + CapLift, CapColor);
        }

        // Lamps on the wall tops, each with a warm glow and a pool of light on the floor below.
        for (var i = 0; i < _lamps.Count; i++)
        {
            var (position, inward) = _lamps[i];
            var housing = new Vector2(LampHousingSize);
            renderer.FillRectRaised(position - housing / 2f, housing, _wallHeight + 2f, LampHousingColor);
            var bulb = position + inward * 6f;
            renderer.FillCircleRaised(bulb, _wallHeight + 6f, 16f, LampColor * 0.4f);
            renderer.FillCircleRaised(bulb, _wallHeight + 6f, 6f, LampColor);
            renderer.FillCircle(position + inward * LampFloorReach, LampFloorRadius, LampColor * 0.06f);
        }
    }

    private const float LampHousingSize = 14f;

    // Concrete slabs in slightly different tones, with dark seams between them.
    private void PaintSlabs(FloorStains floor, GameRandom random)
    {
        var count = (int)MathF.Ceiling(_arena.Size / SlabSize);
        for (var x = 0; x < count; x++)
        {
            for (var y = 0; y < count; y++)
            {
                var center = new Vector2(_arena.Left + (x + 0.5f) * SlabSize, _arena.Top + (y + 0.5f) * SlabSize);
                var tone = random.Range(-1f, 1f);
                var color = tone >= 0f ? Color.White * (0.04f * tone) : Color.Black * (0.12f * -tone);
                floor.AddRect(center, new Vector2(SlabSize - SeamThickness), 0f, color);
            }
        }

        for (var i = 1; i < count; i++)
        {
            var offset = i * SlabSize;
            floor.AddLine(
                new Vector2(_arena.Left + offset, _arena.Top), new Vector2(_arena.Left + offset, _arena.Bottom), SeamThickness, SeamColor);
            floor.AddLine(
                new Vector2(_arena.Left, _arena.Top + offset), new Vector2(_arena.Right, _arena.Top + offset), SeamThickness, SeamColor);
        }
    }

    // Thin dark cracks wandering a short way across the concrete.
    private void PaintCracks(FloorStains floor, GameRandom random)
    {
        for (var i = 0; i < Cracks; i++)
        {
            var at = RandomFloorPoint(random);
            var heading = random.Range(0f, MathHelper.TwoPi);
            var segments = 3 + random.NextIndex(4);
            for (var s = 0; s < segments; s++)
            {
                heading += random.Range(-0.7f, 0.7f);
                var next = at + new Vector2(MathF.Cos(heading), MathF.Sin(heading)) * random.Range(18f, 55f);
                floor.AddLine(at, next, random.Range(1.4f, 2.6f), CrackColor);
                at = next;
            }
        }
    }

    // Dark oil stains, each two overlapping blots so they are not perfectly round.
    private void PaintOil(FloorStains floor, GameRandom random)
    {
        for (var i = 0; i < OilStains; i++)
        {
            var at = RandomFloorPoint(random);
            var radius = random.Range(30f, 75f);
            floor.Add(at, radius, OilColor);
            floor.Add(at + new Vector2(random.Range(-0.6f, 0.6f), random.Range(-0.6f, 0.6f)) * radius, radius * 0.7f, OilColor);
        }
    }

    // A row of yellow dashes painted just inside each door.
    private void PaintHazardDashes(FloorStains floor)
    {
        var doors = _arena.Doors;
        for (var i = 0; i < doors.Count; i++)
        {
            ArenaView.DoorInnerEdge(doors[i], out var from, out var to);
            var inward = doors[i].Inward;
            var along = to - from;
            var length = along.Length();
            var direction = along / length;
            var rotation = MathF.Atan2(direction.Y, direction.X);
            for (var t = 0f; t + HazardDash <= length; t += HazardDash + HazardGap)
            {
                var center = from + direction * (t + HazardDash / 2f) + inward * (HazardInset + HazardDepth / 2f);
                floor.AddRect(center, new Vector2(HazardDash, HazardDepth), rotation, HazardColor);
            }
        }
    }

    // Drain grates around the middle of the yard.
    private void PaintDrains(FloorStains floor)
    {
        var middle = _arena.Center;
        var spots = new[] { new Vector2(-260f, -260f), new Vector2(260f, -260f), new Vector2(-260f, 260f), new Vector2(260f, 260f) };
        for (var i = 0; i < spots.Length; i++)
        {
            var center = middle + spots[i];
            floor.AddRect(center, new Vector2(70f, 44f), 0f, DrainColor);
            for (var bar = -2; bar <= 2; bar++)
            {
                floor.AddRect(center + new Vector2(bar * 12f, 0f), new Vector2(3f, 38f), 0f, DrainBarColor);
            }
        }
    }

    private Vector2 RandomFloorPoint(GameRandom random)
    {
        return new Vector2(
            random.Range(_arena.Left + 80f, _arena.Right - 80f), random.Range(_arena.Top + 80f, _arena.Bottom - 80f));
    }

    // Lamps along the middle of each wall top, spaced out and kept clear of the doors.
    private void PlaceLamps()
    {
        var half = _arena.WallThickness / 2f;
        AddLampRow(new Vector2(_arena.Left, _arena.Top - half), Vector2.UnitX, Vector2.UnitY);
        AddLampRow(new Vector2(_arena.Left, _arena.Bottom + half), Vector2.UnitX, -Vector2.UnitY);
        AddLampRow(new Vector2(_arena.Left - half, _arena.Top), Vector2.UnitY, Vector2.UnitX);
        AddLampRow(new Vector2(_arena.Right + half, _arena.Top), Vector2.UnitY, -Vector2.UnitX);
    }

    private void AddLampRow(Vector2 start, Vector2 along, Vector2 inward)
    {
        for (var t = LampSpacing / 2f; t < _arena.Size; t += LampSpacing)
        {
            var position = start + along * t;
            if (!NearADoor(position))
            {
                _lamps.Add((position, inward));
            }
        }
    }

    private bool NearADoor(Vector2 position)
    {
        var doors = _arena.Doors;
        for (var i = 0; i < doors.Count; i++)
        {
            if (Vector2.Distance(doors[i].Center, position) < LampDoorClearance)
            {
                return true;
            }
        }

        return false;
    }
}
