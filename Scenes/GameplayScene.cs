using System;
using System.Collections.Generic;
using Fourgate.Core.Audio;
using Fourgate.Core.Data;
using Fourgate.Core.Input;
using Fourgate.Core.Rendering;
using Fourgate.Core.Rng;
using Fourgate.Entities;
using Fourgate.Systems;
using Fourgate.UI;
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
    private const float DoorHaloDepth = 110f;
    private const float DoorHaloOpacity = 0.3f;
    private const float EdgeIndicatorInset = 40f;
    private const float EdgeIndicatorRadius = 18f;

    private static readonly Color WallColor = new(70, 74, 92);
    private static readonly Color FloorColor = new(34, 36, 46);
    private static readonly Color GridColor = new(42, 45, 57);
    private static readonly Color GateColor = new(150, 60, 50);
    private static readonly Color GateGlowColor = new(255, 190, 70);
    private static readonly Color EdgeIndicatorCoreColor = new(40, 20, 10);
    private static readonly Color FacingColor = new(225, 230, 240);
    private static readonly Color CrosshairColor = new(240, 240, 240);
    private static readonly Color ReloadBarBackColor = new(12, 13, 18);
    private static readonly Color ReloadBarFillColor = new(255, 225, 120);

    private readonly GameData _data;
    private readonly GameRandom _random;
    private readonly Arena _arena;
    private readonly ShotSystem _shots;
    private readonly EnemySystem _enemies;
    private readonly WaveSystem? _waves;
    private readonly WaveHud _waveHud = new();
    private readonly GameOverOverlay _gameOverOverlay = new();
    private readonly IAudioManager _audio;
    private float _elapsedSeconds;
    private readonly List<PracticeTarget> _practiceTargets = new();

    // _cameras[i] is the view of _players[i].
    private readonly List<Player> _players = new();
    private readonly List<Camera2D> _cameras = new();
    private readonly List<PlayerHud> _playerHuds = new();

    public GameplayScene(GameData data, IPlayerInput playerOneInput, IAudioManager audio)
    {
        _data = data;
        _audio = audio;
        var config = data.Game;
        _random = new GameRandom(config.Debug.Seed ?? Environment.TickCount);
        _arena = new Arena(config.Arena);
        _enemies = new EnemySystem(audio);
        _shots = new ShotSystem(_arena);
        _shots.AddSource(_enemies);
        _waves = config.Debug.DisableWaves ? null : new WaveSystem(data, _enemies, _arena, _random, audio);

        var player = new Player(0, playerOneInput, config.Player, audio, _arena.Center);
        player.GiveWeapon(new Weapon(data.GetWeapon(config.Player.StartingWeapon), config, audio));

        var fullScreen = new Rectangle(0, 0, config.VirtualWidth, config.VirtualHeight);
        AddPlayer(player, fullScreen, config.Camera);

        if (config.Debug.PracticeTargets)
        {
            AddPracticeTargets(config.Player.Radius);
        }
    }

    /// <summary>True once every player is dead; the simulation is frozen from then on.</summary>
    public bool IsGameOver { get; private set; }

    /// <summary>True when a player asked to start over from the game over screen.</summary>
    public bool RestartRequested { get; private set; }

    public void Update(float deltaSeconds)
    {
        if (IsGameOver)
        {
            UpdateGameOver();
            return;
        }

        _elapsedSeconds += deltaSeconds;

        for (var i = 0; i < _practiceTargets.Count; i++)
        {
            _practiceTargets[i].Update(deltaSeconds);
        }

        _waves?.Update(deltaSeconds);
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

        if (!AnyPlayerAlive())
        {
            IsGameOver = true;
            _gameOverOverlay.SetStats(_waves?.Wave ?? 0, _enemies.KillCount, _elapsedSeconds);
            _audio.PlaySfx(SfxIds.GameOver);
        }
    }

    private void UpdateGameOver()
    {
        for (var i = 0; i < _players.Count; i++)
        {
            var input = _players[i].Input;
            input.Update(_players[i].Position, _cameras[i]);
            if (input.ConfirmPressed)
            {
                RestartRequested = true;
            }
        }
    }

    private bool AnyPlayerAlive()
    {
        for (var i = 0; i < _players.Count; i++)
        {
            if (_players[i].IsAlive)
            {
                return true;
            }
        }

        return false;
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
            DrawDoorIndicators(renderer, _cameras[i]);
            _playerHuds[i].Draw(renderer, _players[i], _cameras[i].ViewRect);

            var input = _players[i].Input;
            if (input.HasPointer && !IsGameOver)
            {
                DrawCrosshair(renderer, input.AimScreen);
            }
        }

        if (_waves is not null)
        {
            _waveHud.Draw(renderer, _waves, _data.Game.VirtualWidth);
        }

        if (IsGameOver)
        {
            _gameOverOverlay.Draw(renderer, _data.Game.VirtualWidth, _data.Game.VirtualHeight);
        }

        renderer.End();
    }

    // Points toward glowing doors that are outside a view, so a spawn warning is never
    // missed just because the door is off screen.
    private void DrawDoorIndicators(IRenderer renderer, Camera2D camera)
    {
        if (_waves is null)
        {
            return;
        }

        var view = camera.ViewRect;
        var doors = _arena.Doors;
        for (var i = 0; i < doors.Count; i++)
        {
            var glow = _waves.Spawner.GetDoorGlow(i);
            if (glow <= 0f)
            {
                continue;
            }

            var screen = camera.WorldToScreen(doors[i].Center);
            if (screen.X >= view.Left && screen.X <= view.Right && screen.Y >= view.Top && screen.Y <= view.Bottom)
            {
                continue;
            }

            var marker = new Vector2(
                Math.Clamp(screen.X, view.Left + EdgeIndicatorInset, view.Right - EdgeIndicatorInset),
                Math.Clamp(screen.Y, view.Top + EdgeIndicatorInset, view.Bottom - EdgeIndicatorInset));
            var radius = EdgeIndicatorRadius * (0.6f + 0.4f * glow);
            renderer.FillCircle(marker, radius, GateGlowColor);
            renderer.FillCircle(marker, radius * 0.45f, EdgeIndicatorCoreColor);
        }
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
        _playerHuds.Add(new PlayerHud());
    }

    private void DrawPlayers(IRenderer renderer)
    {
        for (var i = 0; i < _players.Count; i++)
        {
            var player = _players[i];
            if (!player.IsAlive)
            {
                continue;
            }

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
            var glow = _waves?.Spawner.GetDoorGlow(i) ?? 0f;
            if (glow > 0f)
            {
                DrawDoorHalo(renderer, doors[i], glow);
            }

            renderer.FillRect(doors[i].TopLeft, doors[i].Size, Color.Lerp(GateColor, GateGlowColor, glow));
        }
    }

    // Light spilling from a glowing door onto the floor in front of it.
    private static void DrawDoorHalo(IRenderer renderer, Door door, float glow)
    {
        Vector2 topLeft;
        Vector2 size;
        if (door.Inward.X == 0f)
        {
            size = new Vector2(door.Size.X, DoorHaloDepth);
            topLeft = door.Inward.Y > 0f
                ? door.TopLeft + new Vector2(0f, door.Size.Y)
                : door.TopLeft - new Vector2(0f, DoorHaloDepth);
        }
        else
        {
            size = new Vector2(DoorHaloDepth, door.Size.Y);
            topLeft = door.Inward.X > 0f
                ? door.TopLeft + new Vector2(door.Size.X, 0f)
                : door.TopLeft - new Vector2(DoorHaloDepth, 0f);
        }

        renderer.FillRect(topLeft, size, GateGlowColor * (DoorHaloOpacity * glow));
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
