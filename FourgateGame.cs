using System;
using System.IO;
using Fourgate.Core.Audio;
using Fourgate.Core.Data;
using Fourgate.Core.Input;
using Fourgate.Core.Rendering;
using Fourgate.Scenes;
using Fourgate.Systems;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Fourgate;

/// <summary>Game entry point: owns the main loop and switches between the start screen and runs.</summary>
public sealed class FourgateGame : Game
{
    private const int SimulationHz = 60;
    private const int DebugSpawnGroupSize = 10;
    private const int DebugXpGrant = 50;

    private static readonly string FontPath =
        Path.Combine(AppContext.BaseDirectory, "Assets", "Fonts", "Rajdhani-SemiBold.ttf");

    private static readonly Color BackgroundColor = new(16, 17, 22);

    private readonly DisplayManager _display;
    private readonly WindowHotkeys _hotkeys = new();
    private readonly DebugHotkeys _debugHotkeys = new();
    private int _debugEnemyIndex;
    private readonly IAudioManager _audio = new NullAudioManager();
    private readonly GameData _data;

    private SpriteBatch? _spriteBatch;
    private VirtualScreen? _screen;
    private ShapeRenderer? _renderer;
    private IScene? _scene;
    private IPlayerInput? _playerOneInput;
    private IPlayerInput[] _singleInputs = Array.Empty<IPlayerInput>();
    private IPlayerInput[] _coopInputs = Array.Empty<IPlayerInput>();
    private int _lastChosenPlayers = 1;
    private RunMode _lastMode;

    // The run in progress, for the debug hotkeys; null on the start screen.
    private GameplayScene? CurrentRun => _scene as GameplayScene;

    public FourgateGame()
    {
        _data = DataLoader.LoadAll(DataLoader.DefaultDirectory);
        _display = new DisplayManager(new GraphicsDeviceManager(this));

        Content.RootDirectory = "Content";

        // The game draws its own crosshair.
        IsMouseVisible = false;
        IsFixedTimeStep = true;
        TargetElapsedTime = TimeSpan.FromSeconds(1.0 / SimulationHz);
        Window.Title = "Fourgate";
        Window.AllowUserResizing = true;
    }

    protected override void LoadContent()
    {
        _spriteBatch = new SpriteBatch(GraphicsDevice);
        _screen = new VirtualScreen(GraphicsDevice, _data.Game.VirtualWidth, _data.Game.VirtualHeight);
        _renderer = new ShapeRenderer(GraphicsDevice, _spriteBatch, File.ReadAllBytes(FontPath));

        // Single-player: keyboard+mouse or the first gamepad, whichever was used last.
        // Co-op: player 1 on keyboard+mouse, player 2 on the gamepad (and, for testing, debug keys).
        var keyboardMouse = new KeyboardMouseInput(_screen);
        var gamepad = new GamepadInput(PlayerIndex.One);
        _playerOneInput = new SwitchingInput(keyboardMouse, gamepad);
        _singleInputs = new[] { _playerOneInput };
        IPlayerInput playerTwo = _data.Game.Debug.KeyboardPlayerTwo
            ? new SwitchingInput(gamepad, new DebugKeyboardInput())
            : gamepad;
        _coopInputs = new[] { keyboardMouse, playerTwo };
        _scene = new ModeSelectScene(_playerOneInput, _data.Game);
    }

    protected override void UnloadContent()
    {
        _renderer?.Dispose();
        _screen?.Dispose();
        _spriteBatch?.Dispose();
    }

    protected override void Update(GameTime gameTime)
    {
        // Freeze while the window is in the background so stray input does not play the game.
        if (IsActive)
        {
            _hotkeys.Update();
            if (_hotkeys.ToggleWindowModePressed)
            {
                _display.ToggleWindowMode();
            }

            if (_hotkeys.CycleResolutionPressed)
            {
                _display.CycleWindowedResolution();
            }

            if (_data.Game.Debug.Hotkeys)
            {
                UpdateDebugHotkeys();
            }

            _scene?.Update((float)gameTime.ElapsedGameTime.TotalSeconds);
            SwitchScene();
        }

        base.Update(gameTime);
    }

    // The start screen leads into a run; a finished run goes back to the start screen.
    private void SwitchScene()
    {
        if (_playerOneInput is null)
        {
            return;
        }

        if (_scene is ModeSelectScene { ChosenPlayers: { } players } select)
        {
            _lastChosenPlayers = players;
            _lastMode = select.Mode;
            _scene = new GameplayScene(_data, players == 2 ? _coopInputs : _singleInputs, _audio, _lastMode);
        }
        else if (_scene is GameplayScene { RestartRequested: true })
        {
            _scene = new ModeSelectScene(_playerOneInput, _data.Game, _lastChosenPlayers, _lastMode);
        }
    }

    private void UpdateDebugHotkeys()
    {
        _debugHotkeys.Update();

        if (_debugHotkeys.NextEnemyPressed)
        {
            // Enemies first, then bosses.
            _debugEnemyIndex = (_debugEnemyIndex + 1) % (_data.Enemies.Count + _data.Bosses.Count);
            CurrentRun?.ShowDebugNotice($"F1/F2 spawn: {DebugSpawnName()}");
        }

        var enemyId = _debugEnemyIndex < _data.Enemies.Count
            ? _data.Enemies[_debugEnemyIndex].Id
            : _data.Bosses[_debugEnemyIndex - _data.Enemies.Count].Id;
        if (_debugHotkeys.SpawnOnePressed)
        {
            CurrentRun?.DebugSpawnEnemies(enemyId, 1);
        }

        if (_debugHotkeys.SpawnManyPressed)
        {
            CurrentRun?.DebugSpawnEnemies(enemyId, DebugSpawnGroupSize);
        }

        if (_debugHotkeys.GrantXpPressed)
        {
            CurrentRun?.DebugGrantXp(DebugXpGrant);
        }

        if (_debugHotkeys.DropCratesPressed)
        {
            CurrentRun?.DebugDropCrates();
        }

        if (_debugHotkeys.GrantWeaponsPressed)
        {
            CurrentRun?.DebugGrantAllWeapons();
        }

        if (_debugHotkeys.OneShotKillsPressed)
        {
            CurrentRun?.DebugToggleOneShotKills();
        }

        if (_debugHotkeys.LevelUpWeaponPressed)
        {
            CurrentRun?.DebugLevelUpCurrentWeapons();
        }
    }

    private string DebugSpawnName()
    {
        return _debugEnemyIndex < _data.Enemies.Count
            ? _data.Enemies[_debugEnemyIndex].DisplayName
            : _data.Bosses[_debugEnemyIndex - _data.Enemies.Count].DisplayName;
    }

    protected override void Draw(GameTime gameTime)
    {
        if (_screen is null || _renderer is null || _scene is null || _spriteBatch is null)
        {
            return;
        }

        _screen.Begin(BackgroundColor);
        _scene.Draw(_renderer);
        _screen.Present(_spriteBatch);

        base.Draw(gameTime);
    }
}
