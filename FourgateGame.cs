using System;
using System.IO;
using Fourgate.Core.Audio;
using Fourgate.Core.Data;
using Fourgate.Core.Input;
using Fourgate.Core.Rendering;
using Fourgate.Scenes;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Fourgate;

/// <summary>Game entry point: owns the main loop and (later) scene switching.</summary>
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
    private readonly IAudioManager _audio = new NullAudioManager();
    private readonly GameData _data;

    private SpriteBatch? _spriteBatch;
    private VirtualScreen? _screen;
    private ShapeRenderer? _renderer;
    private GameplayScene? _scene;
    private IPlayerInput? _playerOneInput;

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
        _playerOneInput = new SwitchingInput(new KeyboardMouseInput(_screen), new GamepadInput(PlayerIndex.One));
        _scene = new GameplayScene(_data, _playerOneInput, _audio);
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
            if (_scene is { RestartRequested: true } && _playerOneInput is not null)
            {
                _scene = new GameplayScene(_data, _playerOneInput, _audio);
            }
        }

        base.Update(gameTime);
    }

    private void UpdateDebugHotkeys()
    {
        _debugHotkeys.Update();

        var enemyId = _data.Enemies[0].Id;
        if (_debugHotkeys.SpawnOnePressed)
        {
            _scene?.DebugSpawnEnemies(enemyId, 1);
        }

        if (_debugHotkeys.SpawnManyPressed)
        {
            _scene?.DebugSpawnEnemies(enemyId, DebugSpawnGroupSize);
        }

        if (_debugHotkeys.GrantXpPressed)
        {
            _scene?.DebugGrantXp(DebugXpGrant);
        }

        if (_debugHotkeys.DropCratesPressed)
        {
            _scene?.DebugDropCrates();
        }

        if (_debugHotkeys.GrantWeaponsPressed)
        {
            _scene?.DebugGrantAllWeapons();
        }

        if (_debugHotkeys.OneShotKillsPressed)
        {
            _scene?.DebugToggleOneShotKills();
        }
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
