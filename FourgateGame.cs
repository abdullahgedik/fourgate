using System;
using System.IO;
using Fourgate.Core.Audio;
using Fourgate.Core.Data;
using Fourgate.Core.Input;
using Fourgate.Core.Persistence;
using Fourgate.Core.Rendering;
using Fourgate.Core.Rendering.Models;
using Fourgate.Scenes;
using Fourgate.Systems;
using Fourgate.UI;
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

    private static readonly Color BackgroundColor = new(14, 9, 9);

    private readonly DisplayManager _display;
    private readonly WindowHotkeys _hotkeys = new();
    private readonly DebugHotkeys _debugHotkeys = new();
    private int _debugEnemyIndex;
    private readonly IAudioManager _audio;
    private readonly AudioEventLog? _audioLog;
    private readonly MonoGameAudioManager _sound;
    private readonly AudioEventOverlay _audioOverlay = new();
    private readonly GameData _data;

    private SpriteBatch? _spriteBatch;
    private VirtualScreen? _screen;
    private ShapeRenderer? _renderer;
    private Renderer3D? _renderer3D;
    private FloorStainCanvas? _stainCanvas;
    private readonly ModelLibrary _models;
    private readonly FrameTimeOverlay _frameTime = new();
    private IScene? _scene;
    private IPlayerInput? _playerOneInput;
    private IPlayerInput[] _singleInputs = Array.Empty<IPlayerInput>();
    private IPlayerInput[] _coopInputs = Array.Empty<IPlayerInput>();
    private int _lastChosenPlayers = 1;
    private RunMode _lastMode;
    private HighScoreStore? _highScoreStore;
    private HighScoreTable _highScores = new();
    private readonly SettingsStore _settingsStore = new(SettingsStore.DefaultPath);
    private GameSettings _settings = new();
    private KeyboardMouseInput? _keyboardMouse;

    // A paused run waiting while its settings are open.
    private GameplayScene? _suspendedRun;

    // The run in progress, for the debug hotkeys; null on the start screen.
    private GameplayScene? CurrentRun => _scene as GameplayScene;

    public FourgateGame()
    {
        _data = DataLoader.LoadAll(DataLoader.DefaultDirectory);
        _models = new ModelLibrary(DataLoader.LoadModels(DataLoader.DefaultDirectory));
        _models.CheckReferences(_data);
        _frameTime.Visible = _data.Game.Debug.ShowFrameTime;

        // Sounds come from Assets/Audio (WAV effects, OGG music, named by event id); events without
        // a file are silent. With debug.showAudioEvents on, the latest events are listed on screen.
        _sound = new MonoGameAudioManager(Path.Combine(AppContext.BaseDirectory, "Assets", "Audio"), _data.Game.Audio);
        if (_data.Game.Debug.ShowAudioEvents)
        {
            _audioLog = new AudioEventLog(_sound);
            _audio = _audioLog;
        }
        else
        {
            _audio = _sound;
        }
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
        _renderer3D = new Renderer3D(GraphicsDevice, _renderer);
        _stainCanvas = new FloorStainCanvas(GraphicsDevice, _data.Game.Arena.Size);
        _renderer.FloorStainTexture = _stainCanvas.Texture;
        _renderer3D.FloorStainTexture = _stainCanvas.Texture;
        _renderer3D.Models = _models;

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
        _highScoreStore = new HighScoreStore(HighScoreStore.DefaultPath);
        _highScores = _highScoreStore.Load();
        _settings = _settingsStore.Load();
        _keyboardMouse = keyboardMouse;
        keyboardMouse.Bindings = _settings.Controls!;
        ApplyDisplay(_settings);
        _sound.Preload();
        foreach (var problem in _sound.Problems)
        {
            System.Diagnostics.Debug.WriteLine("Audio: " + problem);
        }

        ApplyVolumes();
        _scene = new MainMenuScene(_playerOneInput, _data.Game, MainMenuChoice.Play, _audio);
    }

    protected override void UnloadContent()
    {
        _sound.Dispose();
        _renderer3D?.Dispose();
        _stainCanvas?.Dispose();
        _renderer?.Dispose();
        _screen?.Dispose();
        _spriteBatch?.Dispose();
    }

    protected override void Update(GameTime gameTime)
    {
        _frameTime.BeginUpdate();
        // Freeze while the window is in the background so stray input does not play the game.
        if (IsActive)
        {
            _hotkeys.Update();
            if (_hotkeys.ToggleWindowModePressed)
            {
                _display.ToggleWindowMode();
                RememberDisplay();
            }

            if (_hotkeys.CycleResolutionPressed)
            {
                _display.CycleWindowedResolution();
                RememberDisplay();
            }

            if (_data.Game.Debug.Hotkeys)
            {
                UpdateDebugHotkeys();
            }

            _scene?.Update((float)gameTime.ElapsedGameTime.TotalSeconds);
            SwitchScene();
        }

        if (_scene is GameplayScene counted)
        {
            _frameTime.Enemies = counted.EnemyCount;
            _frameTime.Particles = counted.ParticleCount;
        }

        _frameTime.EndUpdate();
        base.Update(gameTime);
    }

    // Main menu → players and modes → a run → back to players and modes; the main menu also
    // leads to the high scores and the settings.
    private void SwitchScene()
    {
        if (_playerOneInput is null)
        {
            return;
        }

        if (_scene is MainMenuScene { Choice: { } choice })
        {
            switch (choice)
            {
                case MainMenuChoice.Play:
                    _scene = new ModeSelectScene(_playerOneInput, _data.Game, _lastChosenPlayers, _lastMode, _audio);
                    break;

                case MainMenuChoice.HighScores:
                    _scene = new HighScoresScene(_playerOneInput, _highScores, _data.Game, _audio);
                    break;

                case MainMenuChoice.Settings:
                    _scene = new SettingsScene(
                        _playerOneInput, _data.Game, _settings, _display.AvailableResolutions, ApplyDisplay, _audio,
                        _keyboardMouse);
                    break;

                case MainMenuChoice.Quit:
                    Exit();
                    break;
            }

            return;
        }

        if (_scene is SettingsScene { IsDone: true })
        {
            _settingsStore.Save(_settings);
            ApplyVolumes();
            if (_suspendedRun is not null)
            {
                _suspendedRun.ReturnFromSettings();
                _scene = _suspendedRun;
                _suspendedRun = null;
            }
            else
            {
                _scene = new MainMenuScene(_playerOneInput, _data.Game, MainMenuChoice.Settings, _audio);
            }

            return;
        }

        if (_scene is GameplayScene { SettingsRequested: true } paused)
        {
            _suspendedRun = paused;
            _scene = new SettingsScene(
                _playerOneInput, _data.Game, _settings, _display.AvailableResolutions, ApplyDisplay, _audio, _keyboardMouse);
            return;
        }

        if (_scene is GameplayScene { QuitRequested: true })
        {
            _scene = new MainMenuScene(_playerOneInput, _data.Game, MainMenuChoice.Play, _audio);
            return;
        }

        if (_scene is ModeSelectScene { BackRequested: true } back)
        {
            _lastMode = back.Mode;
            _scene = new MainMenuScene(_playerOneInput, _data.Game, MainMenuChoice.Play, _audio);
            return;
        }

        if (_scene is ModeSelectScene { ChosenPlayers: { } players } select)
        {
            _lastChosenPlayers = players;
            _lastMode = select.Mode;
            _scene = new GameplayScene(
                _data, players == 2 ? _coopInputs : _singleInputs, _audio, _lastMode, _highScores, _highScoreStore,
                _settings,
                _models);
        }
        else if (_scene is HighScoresScene { IsDone: true })
        {
            _scene = new MainMenuScene(_playerOneInput, _data.Game, MainMenuChoice.HighScores, _audio);
        }
        else if (_scene is GameplayScene { RestartRequested: true })
        {
            _scene = new ModeSelectScene(_playerOneInput, _data.Game, _lastChosenPlayers, _lastMode, _audio);
        }
    }

    private void ApplyVolumes()
    {
        var max = (float)GameSettings.MaxVolume;
        _audio.SetVolumes(_settings.MasterVolume / max, _settings.MusicVolume / max, _settings.SfxVolume / max);
    }

    private void ApplyDisplay(GameSettings settings)
    {
        _display.Apply(settings.WindowMode, new Point(settings.ResolutionWidth, settings.ResolutionHeight));
    }

    // F9 / F11 change the display outside the settings screen; keep the saved settings in step.
    private void RememberDisplay()
    {
        _settings.WindowMode = _display.Mode;
        _settings.ResolutionWidth = _display.WindowedResolution.X;
        _settings.ResolutionHeight = _display.WindowedResolution.Y;
        _settingsStore.Save(_settings);
    }

    private void UpdateDebugHotkeys()
    {
        _debugHotkeys.Update();
        if (_debugHotkeys.ToggleFrameTimePressed)
        {
            _frameTime.Visible = !_frameTime.Visible;
        }

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

        if (_debugHotkeys.MaxAllWeaponsPressed)
        {
            CurrentRun?.DebugMaxAllWeapons();
            CurrentRun?.ShowDebugNotice("All weapons at level 10");
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

        _frameTime.BeginDraw();

        // New blood is painted into the floor image first: that switches the render target.
        if (_scene is GameplayScene run)
        {
            _stainCanvas?.Paint(run.FloorStains);
        }

        _screen.Begin(BackgroundColor);
        _scene.Draw(_renderer3D is not null ? _renderer3D : _renderer);
        if (_audioLog is not null)
        {
            _audioOverlay.Draw(_renderer, _audioLog, _data.Game.VirtualHeight);
        }

        _frameTime.EndDraw(gameTime.ElapsedGameTime.TotalSeconds);
        if (_frameTime.Visible)
        {
            _renderer.BeginScreen();
            _frameTime.Draw(_renderer);
            _renderer.End();
        }

        _screen.Present(_spriteBatch);

        base.Draw(gameTime);
    }
}
