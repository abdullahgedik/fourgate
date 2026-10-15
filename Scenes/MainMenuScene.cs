using Fourgate.Core.Data;
using Fourgate.Core.Input;
using Fourgate.Core.Rendering;
using Fourgate.UI;
using Microsoft.Xna.Framework;

namespace Fourgate.Scenes;

/// <summary>The first screen: Play, High Scores, Settings, Quit.</summary>
public sealed class MainMenuScene : IScene
{
    private const float TitleSize = 140f;
    private const float HintSize = 26f;

    private static readonly Color BackgroundColor = new(20, 21, 28);
    private static readonly Color TitleColor = new(240, 240, 245);
    private static readonly Color HintColor = new(150, 155, 170);

    private static readonly string[] Labels = { "PLAY", "HIGH SCORES", "SETTINGS", "QUIT" };
    private static readonly MainMenuChoice[] Choices =
    {
        MainMenuChoice.Play, MainMenuChoice.HighScores, MainMenuChoice.Settings, MainMenuChoice.Quit,
    };

    private readonly IPlayerInput _input;
    private readonly Camera2D _camera;
    private readonly int _screenWidth;
    private readonly int _screenHeight;
    private readonly MenuList _menu;
    private int _selected;

    public MainMenuScene(IPlayerInput input, GameConfig config, MainMenuChoice highlighted = MainMenuChoice.Play)
    {
        _input = input;
        _screenWidth = config.VirtualWidth;
        _screenHeight = config.VirtualHeight;
        _camera = new Camera2D(new Rectangle(0, 0, _screenWidth, _screenHeight), config.Camera);
        _menu = new MenuList(new Vector2(_screenWidth / 2f, _screenHeight * 0.42f), 460f, 80f, 24f);
        _selected = System.Array.IndexOf(Choices, highlighted);
    }

    /// <summary>What was picked; null until a button is confirmed.</summary>
    public MainMenuChoice? Choice { get; private set; }

    public void Update(float deltaSeconds)
    {
        _input.Update(_camera.ViewCenter, _camera);
        if (_input.MenuUpPressed)
        {
            _selected = (_selected + Labels.Length - 1) % Labels.Length;
        }

        if (_input.MenuDownPressed)
        {
            _selected = (_selected + 1) % Labels.Length;
        }

        if (_input.HasPointer)
        {
            var hovered = _menu.ButtonAt(_input.AimScreen, Labels.Length);
            if (hovered >= 0)
            {
                _selected = hovered;
                if (_input.FirePressed)
                {
                    Choice = Choices[hovered];
                    return;
                }
            }
        }

        if (_input.ConfirmPressed)
        {
            Choice = Choices[_selected];
        }
    }

    public void Draw(IRenderer renderer)
    {
        renderer.BeginScreen();
        renderer.FillRect(Vector2.Zero, new Vector2(_screenWidth, _screenHeight), BackgroundColor);
        renderer.DrawText("FOURGATE", new Vector2(_screenWidth / 2f, _screenHeight * 0.14f), TitleSize, TitleColor, TextAlign.Center);
        _menu.Draw(renderer, Labels, _selected);

        var hint = _input.HasPointer ? "Arrows / WASD and Enter, or click" : "D-pad and A";
        renderer.DrawText(hint, new Vector2(_screenWidth / 2f, _screenHeight * 0.93f), HintSize, HintColor, TextAlign.Center);
        if (_input.HasPointer)
        {
            renderer.FillRect(_input.AimScreen - new Vector2(10f, 1f), new Vector2(20f, 2f), TitleColor);
            renderer.FillRect(_input.AimScreen - new Vector2(1f, 10f), new Vector2(2f, 20f), TitleColor);
        }

        renderer.End();
    }
}
