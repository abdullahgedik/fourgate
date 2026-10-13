using System.Collections.Generic;
using Fourgate.Core.Audio;
using Fourgate.Core.Data;
using Fourgate.Core.Input;
using Fourgate.Core.Rendering;
using Fourgate.Entities;
using Microsoft.Xna.Framework;

namespace Fourgate.Scenes;

/// <summary>The arena and everything that happens in it.</summary>
public sealed class GameplayScene
{
    private const float GridSpacing = 100f;
    private const float GridLineThickness = 2f;
    private const float FacingLineLength = 14f;
    private const float FacingLineThickness = 6f;
    private const float CrosshairArm = 14f;
    private const float CrosshairThickness = 2f;

    private static readonly Color WallColor = new(70, 74, 92);
    private static readonly Color FloorColor = new(34, 36, 46);
    private static readonly Color GridColor = new(42, 45, 57);
    private static readonly Color GateColor = new(150, 60, 50);
    private static readonly Color FacingColor = new(225, 230, 240);
    private static readonly Color CrosshairColor = new(240, 240, 240);

    private readonly Arena _arena;

    // _cameras[i] is the view of _players[i].
    private readonly List<Player> _players = new();
    private readonly List<Camera2D> _cameras = new();

    public GameplayScene(GameConfig config, IPlayerInput playerOneInput, IAudioManager audio)
    {
        _arena = new Arena(config.Arena);

        var fullScreen = new Rectangle(0, 0, config.VirtualWidth, config.VirtualHeight);
        AddPlayer(new Player(0, playerOneInput, config.Player, audio, _arena.Center), fullScreen, config.Camera);
    }

    public void Update(float deltaSeconds)
    {
        for (var i = 0; i < _players.Count; i++)
        {
            var player = _players[i];
            var camera = _cameras[i];

            player.Input.Update(player.Position, camera);
            player.Update(deltaSeconds, _arena);
            camera.Follow(player.Position, player.Input.AimScreen, deltaSeconds);
        }
    }

    public void Draw(IRenderer renderer)
    {
        for (var i = 0; i < _cameras.Count; i++)
        {
            renderer.BeginWorld(_cameras[i]);
            DrawArena(renderer);
            DrawPlayers(renderer);
            renderer.End();
        }

        renderer.BeginScreen();
        for (var i = 0; i < _players.Count; i++)
        {
            var input = _players[i].Input;
            if (input.HasPointer)
            {
                DrawCrosshair(renderer, input.AimScreen);
            }
        }

        renderer.End();
    }

    private void AddPlayer(Player player, Rectangle viewRect, CameraConfig cameraConfig)
    {
        var camera = new Camera2D(viewRect, cameraConfig);
        var wall = new Vector2(_arena.WallThickness);
        camera.SetBounds(
            new Vector2(_arena.Left, _arena.Top) - wall,
            new Vector2(_arena.Right, _arena.Bottom) + wall);
        camera.SnapTo(player.Position, camera.ViewCenter);

        _players.Add(player);
        _cameras.Add(camera);
    }

    private void DrawPlayers(IRenderer renderer)
    {
        for (var i = 0; i < _players.Count; i++)
        {
            var player = _players[i];
            renderer.DrawVisual(player.Visual, player.Position, 0f);

            var muzzle = player.Position + player.AimDirection * player.Radius;
            renderer.DrawLine(
                muzzle, muzzle + player.AimDirection * FacingLineLength, FacingLineThickness, FacingColor);
        }
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

    private static void DrawCrosshair(IRenderer renderer, Vector2 position)
    {
        renderer.DrawLine(
            position - new Vector2(CrosshairArm, 0f), position + new Vector2(CrosshairArm, 0f),
            CrosshairThickness, CrosshairColor);
        renderer.DrawLine(
            position - new Vector2(0f, CrosshairArm), position + new Vector2(0f, CrosshairArm),
            CrosshairThickness, CrosshairColor);
    }
}
