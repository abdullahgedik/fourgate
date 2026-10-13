using System;
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

    private static readonly Color BackgroundColor = new(16, 17, 22);
    private static readonly Color CrosshairColor = new(240, 240, 240);
    private static readonly Color AimMarkerColor = new(255, 90, 90);

    private readonly DisplayManager _display;
    private readonly WindowHotkeys _hotkeys = new();
    private readonly MoveKeys _moveKeys = new();
    private readonly VirtualMouse _mouse = new();
    private readonly GameConfig _config;

    private SpriteBatch? _spriteBatch;
    private VirtualScreen? _screen;
    private ShapeRenderer? _renderer;
    private GameplayScene? _scene;

    public FourgateGame()
    {
        _config = DataLoader.LoadGameConfig(DataLoader.DefaultDirectory);
        _display = new DisplayManager(new GraphicsDeviceManager(this));

        Content.RootDirectory = "Content";
        IsMouseVisible = true;
        IsFixedTimeStep = true;
        TargetElapsedTime = TimeSpan.FromSeconds(1.0 / SimulationHz);
        Window.Title = "Fourgate";
        Window.AllowUserResizing = true;
    }

    protected override void LoadContent()
    {
        _spriteBatch = new SpriteBatch(GraphicsDevice);
        _screen = new VirtualScreen(GraphicsDevice, _config.VirtualWidth, _config.VirtualHeight);
        _renderer = new ShapeRenderer(GraphicsDevice, _spriteBatch);
        _scene = new GameplayScene(_config);
    }

    protected override void UnloadContent()
    {
        _renderer?.Dispose();
        _screen?.Dispose();
        _spriteBatch?.Dispose();
    }

    protected override void Update(GameTime gameTime)
    {
        var move = Vector2.Zero;
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

            _moveKeys.Update();
            move = _moveKeys.Direction;
        }

        if (_screen is not null && _scene is not null)
        {
            _mouse.Update(_screen.Viewport);
            _scene.Update((float)gameTime.ElapsedGameTime.TotalSeconds, move, _mouse.Position);
        }

        base.Update(gameTime);
    }

    protected override void Draw(GameTime gameTime)
    {
        if (_screen is null || _renderer is null || _scene is null || _spriteBatch is null)
        {
            return;
        }

        _screen.Begin(BackgroundColor);

        _renderer.Begin(_scene.Camera);
        _scene.Draw(_renderer);

        // World-space dot at the converted mouse position; it must stay centered in the crosshair.
        _renderer.FillCircle(_scene.Camera.ScreenToWorld(_mouse.Position), 4f, AimMarkerColor);
        _renderer.End();

        _renderer.Begin();
        DrawCrosshair(_renderer, _mouse.Position);
        _renderer.End();

        _screen.Present(_spriteBatch);

        base.Draw(gameTime);
    }

    // Screen-space crosshair; it must sit exactly under the OS cursor.
    private static void DrawCrosshair(IRenderer renderer, Vector2 position)
    {
        const float arm = 14f;
        const float thickness = 2f;

        renderer.DrawLine(position - new Vector2(arm, 0f), position + new Vector2(arm, 0f), thickness, CrosshairColor);
        renderer.DrawLine(position - new Vector2(0f, arm), position + new Vector2(0f, arm), thickness, CrosshairColor);
    }
}
