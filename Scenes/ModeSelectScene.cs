using Fourgate.Core.Data;
using Fourgate.Core.Input;
using Fourgate.Core.Rendering;
using Microsoft.Xna.Framework;

namespace Fourgate.Scenes;

/// <summary>
/// Start screen: choose one player or two-player co-op. Grows into the full mode select
/// (Fast, Nightmare) later.
/// </summary>
public sealed class ModeSelectScene : IScene
{
    private const float TitleSize = 120f;
    private const float SubtitleSize = 34f;
    private const float OptionTitleSize = 48f;
    private const float OptionTextSize = 26f;
    private const float HintSize = 26f;
    private const float CardWidth = 440f;
    private const float CardHeight = 260f;
    private const float CardGap = 80f;
    private const float Border = 4f;

    private static readonly Color BackgroundColor = new(20, 21, 28);
    private static readonly Color TitleColor = new(240, 240, 245);
    private static readonly Color SubtitleColor = new(170, 175, 190);
    private static readonly Color CardColor = new(30, 33, 44);
    private static readonly Color CardSelectedColor = new(46, 52, 72);
    private static readonly Color BorderColor = new(70, 76, 98);
    private static readonly Color BorderSelectedColor = new(255, 225, 120);
    private static readonly Color TextColor = new(200, 205, 220);
    private static readonly Color DisabledColor = new(110, 114, 130);
    private static readonly Color HintColor = new(150, 155, 170);

    private static readonly Option[] Options =
    {
        new(1, "1 PLAYER", "Keyboard + mouse or gamepad", null),
        new(2, "2 PLAYERS", "Co-op: player 2 on a gamepad", "Coming soon"),
    };

    private readonly IPlayerInput _input;
    private readonly Camera2D _camera;
    private readonly int _screenWidth;
    private readonly int _screenHeight;
    private int _selected;

    /// <param name="input">Player 1's input; drives the menu.</param>
    /// <param name="config">Game settings (screen size, camera).</param>
    /// <param name="selectedPlayers">The choice to highlight first, e.g. the last one made.</param>
    public ModeSelectScene(IPlayerInput input, GameConfig config, int selectedPlayers = 1)
    {
        _input = input;
        _screenWidth = config.VirtualWidth;
        _screenHeight = config.VirtualHeight;
        _camera = new Camera2D(new Rectangle(0, 0, _screenWidth, _screenHeight), config.Camera);
        _selected = selectedPlayers == 2 ? 1 : 0;
    }

    /// <summary>Number of players chosen; null until a choice is confirmed.</summary>
    public int? ChosenPlayers { get; private set; }

    public void Update(float deltaSeconds)
    {
        _input.Update(_camera.ViewCenter, _camera);
        if (_input.MenuLeftPressed)
        {
            _selected = 0;
        }

        if (_input.MenuRightPressed)
        {
            _selected = Options.Length - 1;
        }

        var clicked = false;
        if (_input.HasPointer)
        {
            var hovered = OptionAt(_input.AimScreen);
            if (hovered >= 0)
            {
                _selected = hovered;
                clicked = _input.FirePressed;
            }
        }

        var option = Options[_selected];
        if ((clicked || _input.ConfirmPressed) && option.UnavailableNote is null)
        {
            ChosenPlayers = option.Players;
        }
    }

    public void Draw(IRenderer renderer)
    {
        renderer.BeginScreen();
        renderer.FillRect(Vector2.Zero, new Vector2(_screenWidth, _screenHeight), BackgroundColor);

        var center = _screenWidth / 2f;
        renderer.DrawText("FOURGATE", new Vector2(center, _screenHeight * 0.18f), TitleSize, TitleColor, TextAlign.Center);
        renderer.DrawText(
            "Choose players", new Vector2(center, _screenHeight * 0.18f + 140f), SubtitleSize, SubtitleColor, TextAlign.Center);

        for (var i = 0; i < Options.Length; i++)
        {
            DrawOption(renderer, i);
        }

        var hint = _input.HasPointer
            ? "A / D or arrows to choose  ·  Enter or click to start"
            : "D-pad to choose  ·  A to start";
        renderer.DrawText(hint, new Vector2(center, _screenHeight * 0.85f), HintSize, HintColor, TextAlign.Center);

        if (_input.HasPointer)
        {
            renderer.FillRect(_input.AimScreen - new Vector2(10f, 1f), new Vector2(20f, 2f), TitleColor);
            renderer.FillRect(_input.AimScreen - new Vector2(1f, 10f), new Vector2(2f, 20f), TitleColor);
        }

        renderer.End();
    }

    private void DrawOption(IRenderer renderer, int index)
    {
        var option = Options[index];
        var selected = index == _selected;
        var topLeft = CardTopLeft(index);
        var size = new Vector2(CardWidth, CardHeight);

        renderer.FillRect(topLeft, size, selected ? BorderSelectedColor : BorderColor);
        renderer.FillRect(topLeft + new Vector2(Border), size - new Vector2(Border * 2f), selected ? CardSelectedColor : CardColor);

        var available = option.UnavailableNote is null;
        var middle = topLeft.X + CardWidth / 2f;
        renderer.DrawText(
            option.Title, new Vector2(middle, topLeft.Y + 70f), OptionTitleSize, available ? TitleColor : DisabledColor,
            TextAlign.Center);
        renderer.DrawText(
            option.Description, new Vector2(middle, topLeft.Y + 140f), OptionTextSize, available ? TextColor : DisabledColor,
            TextAlign.Center);
        if (option.UnavailableNote is not null)
        {
            renderer.DrawText(
                option.UnavailableNote, new Vector2(middle, topLeft.Y + 190f), OptionTextSize, DisabledColor, TextAlign.Center);
        }
    }

    private Vector2 CardTopLeft(int index)
    {
        var totalWidth = Options.Length * CardWidth + (Options.Length - 1) * CardGap;
        var left = (_screenWidth - totalWidth) / 2f + index * (CardWidth + CardGap);
        return new Vector2(left, _screenHeight * 0.42f);
    }

    private int OptionAt(Vector2 screen)
    {
        for (var i = 0; i < Options.Length; i++)
        {
            var topLeft = CardTopLeft(i);
            if (screen.X >= topLeft.X && screen.X <= topLeft.X + CardWidth
                && screen.Y >= topLeft.Y && screen.Y <= topLeft.Y + CardHeight)
            {
                return i;
            }
        }

        return -1;
    }

    private readonly record struct Option(int Players, string Title, string Description, string? UnavailableNote);
}
