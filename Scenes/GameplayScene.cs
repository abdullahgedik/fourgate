using System;
using Fourgate.Core.Data;
using Fourgate.Core.Rendering;
using Fourgate.Entities;
using Microsoft.Xna.Framework;

namespace Fourgate.Scenes;

/// <summary>The arena and everything that happens in it.</summary>
public sealed class GameplayScene
{
    private const float GridSpacing = 100f;
    private const float GridLineThickness = 2f;

    private static readonly Color WallColor = new(70, 74, 92);
    private static readonly Color FloorColor = new(34, 36, 46);
    private static readonly Color GridColor = new(42, 45, 57);
    private static readonly Color GateColor = new(150, 60, 50);
    private static readonly Color MarkerColor = new(90, 170, 255);

    private readonly PlayerConfig _playerConfig;
    private readonly Arena _arena;

    // Stand-in for the player so the camera has something to follow.
    private Vector2 _marker;

    public GameplayScene(GameConfig config)
    {
        _playerConfig = config.Player;
        _arena = new Arena(config.Arena);
        _marker = _arena.Center;

        Camera = new Camera2D(new Rectangle(0, 0, config.VirtualWidth, config.VirtualHeight), config.Camera);
        var wall = new Vector2(_arena.WallThickness);
        Camera.SetBounds(
            new Vector2(_arena.Left, _arena.Top) - wall,
            new Vector2(_arena.Right, _arena.Bottom) + wall);
        Camera.SnapTo(_marker, Camera.ViewCenter);
    }

    public Camera2D Camera { get; }

    /// <param name="deltaSeconds">Frame time.</param>
    /// <param name="move">Movement direction (unit length or zero).</param>
    /// <param name="aimScreen">Aim point in virtual-screen coordinates.</param>
    public void Update(float deltaSeconds, Vector2 move, Vector2 aimScreen)
    {
        var radius = _playerConfig.Radius;
        _marker += move * _playerConfig.MoveSpeed * deltaSeconds;
        _marker.X = Math.Clamp(_marker.X, _arena.Left + radius, _arena.Right - radius);
        _marker.Y = Math.Clamp(_marker.Y, _arena.Top + radius, _arena.Bottom - radius);

        Camera.Follow(_marker, aimScreen, deltaSeconds);
    }

    /// <summary>Draws the world; the renderer must be in a world-space pass for <see cref="Camera"/>.</summary>
    public void Draw(IRenderer renderer)
    {
        DrawArena(renderer);
        renderer.FillCircle(_marker, _playerConfig.Radius, MarkerColor);
    }

    private void DrawArena(IRenderer renderer)
    {
        var wall = new Vector2(_arena.WallThickness);
        var floorTopLeft = new Vector2(_arena.Left, _arena.Top);
        var floorSize = new Vector2(_arena.Size);

        renderer.FillRect(floorTopLeft - wall, floorSize + wall * 2f, WallColor);
        renderer.FillRect(floorTopLeft, floorSize, FloorColor);

        // Floor grid: gives the eye a reference for camera and player movement.
        for (var offset = GridSpacing; offset < _arena.Size; offset += GridSpacing)
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

        var doors = _arena.Doors;
        for (var i = 0; i < doors.Count; i++)
        {
            renderer.FillRect(doors[i].TopLeft, doors[i].Size, GateColor);
        }
    }
}
