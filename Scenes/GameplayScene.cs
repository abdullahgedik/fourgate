using System;
using System.Collections.Generic;
using Fourgate.Core.Audio;
using Fourgate.Core.Data;
using Fourgate.Core.Input;
using Fourgate.Core.Rendering;
using Fourgate.Core.Rng;
using Fourgate.Entities;
using Fourgate.Systems;
using Fourgate.Weapons;
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
    private const float ReloadBarExtraWidth = 12f;
    private const float ReloadBarHeight = 6f;
    private const float ReloadBarGap = 10f;

    private static readonly Color WallColor = new(70, 74, 92);
    private static readonly Color FloorColor = new(34, 36, 46);
    private static readonly Color GridColor = new(42, 45, 57);
    private static readonly Color GateColor = new(150, 60, 50);
    private static readonly Color FacingColor = new(225, 230, 240);
    private static readonly Color CrosshairColor = new(240, 240, 240);
    private static readonly Color ReloadBarBackColor = new(12, 13, 18);
    private static readonly Color ReloadBarFillColor = new(255, 225, 120);

    private readonly GameData _data;
    private readonly GameRandom _random;
    private readonly Arena _arena;
    private readonly ShotSystem _shots;
    private readonly EnemySystem _enemies;
    private readonly List<PracticeTarget> _practiceTargets = new();

    // _cameras[i] is the view of _players[i].
    private readonly List<Player> _players = new();
    private readonly List<Camera2D> _cameras = new();

    public GameplayScene(GameData data, IPlayerInput playerOneInput, IAudioManager audio)
    {
        _data = data;
        var config = data.Game;
        _random = new GameRandom(config.Debug.Seed ?? Environment.TickCount);
        _arena = new Arena(config.Arena);
        _enemies = new EnemySystem(audio);
        _shots = new ShotSystem(_arena);
        _shots.AddSource(_enemies);

        var player = new Player(0, playerOneInput, config.Player, audio, _arena.Center);
        player.GiveWeapon(new Weapon(data.GetWeapon(config.Player.StartingWeapon), config, audio));

        var fullScreen = new Rectangle(0, 0, config.VirtualWidth, config.VirtualHeight);
        AddPlayer(player, fullScreen, config.Camera);

        if (config.Debug.PracticeTargets)
        {
            AddPracticeTargets(config.Player.Radius);
        }
    }

    public void Update(float deltaSeconds)
    {
        for (var i = 0; i < _practiceTargets.Count; i++)
        {
            _practiceTargets[i].Update(deltaSeconds);
        }

        _enemies.Update(deltaSeconds, _players, _arena);

        for (var i = 0; i < _players.Count; i++)
        {
            var player = _players[i];
            var camera = _cameras[i];

            player.Input.Update(player.Position, camera);
            player.Update(deltaSeconds, _arena, _shots);
            camera.Follow(player.Position, player.Input.AimScreen, deltaSeconds);
        }

        _shots.Update(deltaSeconds);
    }

    /// <summary>Debug helper: spawns enemies of the given type at random doors.</summary>
    public void DebugSpawnEnemies(string enemyId, int count)
    {
        var config = _data.GetEnemy(enemyId);
        for (var i = 0; i < count; i++)
        {
            var door = _arena.Doors[_random.NextIndex(_arena.Doors.Count)];
            _enemies.Spawn(config, _arena.GetDoorSpawnPoint(door, config.Radius, _random.Range(-1f, 1f)));
        }
    }

    public void Draw(IRenderer renderer)
    {
        for (var i = 0; i < _cameras.Count; i++)
        {
            renderer.BeginWorld(_cameras[i]);
            DrawArena(renderer);
            for (var t = 0; t < _practiceTargets.Count; t++)
            {
                renderer.DrawVisual(_practiceTargets[t].Visual, _practiceTargets[t].Position, 0f);
            }

            _enemies.Draw(renderer);
            _shots.Draw(renderer);
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

    // Debug dummies near the spawn point: one still, one patrolling left and right.
    private void AddPracticeTargets(float radius)
    {
        var still = new PracticeTarget(_arena.Center + new Vector2(350f, -200f), radius);
        var moving = new PracticeTarget(
            _arena.Center + new Vector2(100f, 250f), _arena.Center + new Vector2(600f, 250f), 180f, radius);

        _practiceTargets.Add(still);
        _practiceTargets.Add(moving);
        _shots.AddTarget(still);
        _shots.AddTarget(moving);
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

            var weapon = player.CurrentWeapon;
            if (weapon is { IsReloading: true })
            {
                DrawReloadBar(renderer, player, weapon.ReloadProgress);
            }
        }
    }

    private static void DrawReloadBar(IRenderer renderer, Player player, float progress)
    {
        var size = new Vector2(player.Radius * 2f + ReloadBarExtraWidth, ReloadBarHeight);
        var topLeft = player.Position + new Vector2(-size.X / 2f, player.Radius + ReloadBarGap);

        renderer.FillRect(topLeft, size, ReloadBarBackColor);
        renderer.FillRect(topLeft, new Vector2(size.X * progress, size.Y), ReloadBarFillColor);
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
