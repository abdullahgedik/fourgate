using System;
using Fourgate.Core.Data;
using Fourgate.Core.Rendering;
using Fourgate.Entities;
using Fourgate.Systems;
using Microsoft.Xna.Framework;

namespace Fourgate.UI;

/// <summary>
/// Draws the arena itself: the walls, the floor with its grid and its stains, the pillars and the
/// gates, and the warning a door gives before enemies come through it (a throbbing gate, a curtain
/// of light, lamps on the posts, light spilled on the floor and drifting sparks).
/// </summary>
public sealed class ArenaView
{
    /// <summary>How tall the outer walls stand.</summary>
    public const float WallHeight = 90f;

    /// <summary>How tall the pillars stand.</summary>
    public const float PillarHeight = 120f;

    // Floor grid spacing when no weapon snaps to a grid.
    private const float GridSpacing = 100f;
    private const float GridLineThickness = 2f;
    private const float GateInset = 2f;

    // The door warning: pulses that quicken as the enemies near, a curtain of light in the
    // opening, lamps on the gate posts and light spilled onto the floor.
    private const float DoorPulseSlow = 4f;
    private const float DoorPulseFast = 14f;
    private const float DoorCurtainOpacity = 0.35f;
    private const float DoorLampRadius = 16f;
    private const float DoorFloorLightDepth = 260f;
    private const float DoorFloorLightOpacity = 0.3f;
    private const int DoorFloorLightBands = 5;

    // Once its enemies are out, a door's warning fades over this long instead of vanishing.
    private const float DoorFadeSeconds = 0.4f;

    /// <summary>The gold a warning door glows (also used by the off-screen door pointers).</summary>
    public static readonly Color GateGlowColor = new(255, 190, 70);

    private static readonly Color WallColor = new(70, 74, 92);
    private static readonly Color FloorColor = new(34, 36, 46);
    private static readonly Color GridColor = new(42, 45, 57);
    private static readonly Color GateColor = new(150, 60, 50);

    private readonly Arena _arena;
    private readonly ParticleSystem _particles;
    private readonly float _floorCellSize;
    private readonly float[] _doorGlow;
    private float _pulseTime;

    public ArenaView(Arena arena, GameData data, ParticleSystem particles)
    {
        _arena = arena;
        _particles = particles;
        _floorCellSize = FloorCellSize(data);
        _doorGlow = new float[arena.Doors.Count];
    }

    /// <summary>
    /// A door's warning follows the spawner while it rises and fades out quickly once its enemies
    /// are out; sparks drift out of it while it glows.
    /// </summary>
    public void Update(float deltaSeconds, WaveSystem? waves)
    {
        _pulseTime += deltaSeconds;
        var doors = _arena.Doors;
        for (var i = 0; i < doors.Count; i++)
        {
            var target = waves?.Spawner.GetDoorGlow(i) ?? 0f;
            _doorGlow[i] = target >= _doorGlow[i] ? target : Math.Max(target, _doorGlow[i] - deltaSeconds / DoorFadeSeconds);
            if (_doorGlow[i] <= 0f)
            {
                continue;
            }

            DoorInnerEdge(doors[i], out var from, out var to);
            _particles.DoorSparks(from, to, doors[i].Inward, WallHeight, _doorGlow[i], GateGlowColor, GateColor, deltaSeconds);
        }
    }

    public void Draw(IRenderer renderer)
    {
        var wall = new Vector2(_arena.WallThickness);
        var floorTopLeft = new Vector2(_arena.Left, _arena.Top);
        var floorSize = new Vector2(_arena.Size);

        var outer = floorSize + wall * 2f;
        renderer.FillBox(floorTopLeft - wall, new Vector2(outer.X, wall.Y), WallHeight, WallColor);
        renderer.FillBox(floorTopLeft + new Vector2(-wall.X, floorSize.Y), new Vector2(outer.X, wall.Y), WallHeight, WallColor);
        renderer.FillBox(floorTopLeft - new Vector2(wall.X, 0f), new Vector2(wall.X, floorSize.Y), WallHeight, WallColor);
        renderer.FillBox(floorTopLeft + new Vector2(floorSize.X, 0f), new Vector2(wall.X, floorSize.Y), WallHeight, WallColor);
        renderer.FillRect(floorTopLeft, floorSize, FloorColor);

        // Floor grid: gives the eye a reference for camera and player movement.
        // (Blood stains go on after it, so they darken the grid lines too.)
        for (var offset = _floorCellSize; offset < _arena.Size; offset += _floorCellSize)
        {
            renderer.FillRect(
                new Vector2(_arena.Left + offset - GridLineThickness / 2f, _arena.Top),
                new Vector2(GridLineThickness, _arena.Size),
                GridColor);
            renderer.FillRect(
                new Vector2(_arena.Left, _arena.Top + offset - GridLineThickness / 2f),
                new Vector2(_arena.Size, GridLineThickness),
                GridColor);
        }

        renderer.DrawFloorStains(floorTopLeft, floorSize);

        var pillars = _arena.Pillars;
        for (var i = 0; i < pillars.Count; i++)
        {
            renderer.FillBox(pillars[i].Center - pillars[i].HalfSize, pillars[i].HalfSize * 2f, PillarHeight, WallColor);
        }

        var doors = _arena.Doors;
        for (var i = 0; i < doors.Count; i++)
        {
            // A gate block slightly proud of the wall, so it shows in front of it; set into the
            // wall, it casts no shadow of its own. It throbs while it warns.
            var glow = _doorGlow[i];
            var shown = glow > 0f ? glow * DoorPulse(glow) : 0f;
            renderer.FillBox(
                doors[i].TopLeft - new Vector2(GateInset), doors[i].Size + new Vector2(GateInset * 2f), WallHeight + GateInset,
                Color.Lerp(GateColor, GateGlowColor, shown), castShadow: false);
            if (glow > 0f)
            {
                DrawDoorWarning(renderer, doors[i], glow);
            }
        }
    }

    // 0.55–1: a throb that quickens as the door's enemies get closer.
    private float DoorPulse(float glow)
    {
        var speed = MathHelper.Lerp(DoorPulseSlow, DoorPulseFast, glow);
        return 0.775f + 0.225f * MathF.Sin(_pulseTime * speed);
    }

    // Light spilled onto the floor, a curtain of light standing in the opening and lamps on both
    // gate posts, all throbbing with the gate.
    private void DrawDoorWarning(IRenderer renderer, Door door, float glow)
    {
        var strength = glow * DoorPulse(glow);
        DoorInnerEdge(door, out var from, out var to);
        DrawDoorFloorLight(renderer, door, strength);
        renderer.DrawGlowWall(from, to, WallHeight * (0.4f + 0.6f * glow), GateGlowColor * (DoorCurtainOpacity * strength));
        var lampHeight = WallHeight + GateInset + DoorLampRadius * 0.5f;
        var lampRadius = DoorLampRadius * (0.6f + 0.6f * strength);
        renderer.FillCircleRaised(from, lampHeight, lampRadius * 2f, GateGlowColor * (0.25f * strength));
        renderer.FillCircleRaised(to, lampHeight, lampRadius * 2f, GateGlowColor * (0.25f * strength));
        renderer.FillCircleRaised(from, lampHeight, lampRadius, GateGlowColor * strength);
        renderer.FillCircleRaised(to, lampHeight, lampRadius, GateGlowColor * strength);
    }

    // Side by side bands, each dimmer than the last, so the light fades without visible layers.
    private static void DrawDoorFloorLight(IRenderer renderer, Door door, float strength)
    {
        var band = DoorFloorLightDepth / DoorFloorLightBands;
        for (var i = 0; i < DoorFloorLightBands; i++)
        {
            var near = 1f - (float)i / DoorFloorLightBands;
            var bandTopLeft = FloorStripTopLeft(door, band) + door.Inward * (band * i);
            renderer.FillRect(
                bandTopLeft, FloorStripSize(door, band), GateGlowColor * (DoorFloorLightOpacity * strength * near * near));
        }
    }

    // The two ends of a door's edge facing the arena.
    private static void DoorInnerEdge(Door door, out Vector2 from, out Vector2 to)
    {
        var half = door.Size / 2f;
        var depth = MathF.Abs(door.Inward.X) * half.X + MathF.Abs(door.Inward.Y) * half.Y;
        var width = MathF.Abs(door.Inward.Y) * half.X + MathF.Abs(door.Inward.X) * half.Y;
        var edge = door.Center + door.Inward * (depth + GateInset);
        var across = new Vector2(-door.Inward.Y, door.Inward.X) * width;
        from = edge - across;
        to = edge + across;
    }

    // A strip of floor in front of a door, reaching this deep into the arena.
    private static Vector2 FloorStripSize(Door door, float depth)
    {
        return door.Inward.X == 0f ? new Vector2(door.Size.X, depth) : new Vector2(depth, door.Size.Y);
    }

    private static Vector2 FloorStripTopLeft(Door door, float depth)
    {
        if (door.Inward.X == 0f)
        {
            return door.Inward.Y > 0f
                ? door.TopLeft + new Vector2(0f, door.Size.Y)
                : door.TopLeft - new Vector2(0f, depth);
        }

        return door.Inward.X > 0f
            ? door.TopLeft + new Vector2(door.Size.X, 0f)
            : door.TopLeft - new Vector2(depth, 0f);
    }

    // The floor grid shows the cells barrels snap to, so a player can see where one will go.
    private static float FloorCellSize(GameData data)
    {
        var weapons = data.Weapons;
        for (var i = 0; i < weapons.Count; i++)
        {
            var placed = weapons[i].Placed;
            if (placed is not null && placed.Solid && placed.GridSize > 0f)
            {
                return placed.GridSize;
            }
        }

        return GridSpacing;
    }
}
