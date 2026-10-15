using Fourgate.Core.Audio;
using Fourgate.Core.Data;
using Fourgate.Core.Input;
using Fourgate.Core.Rendering;
using Fourgate.Systems;
using Microsoft.Xna.Framework;

namespace Fourgate.Scenes;

/// <summary>
/// Start screen: one player or two-player co-op, plus the Fast and Nightmare toggles. Picking
/// a player card starts the run; the toggles row sits underneath.
/// </summary>
public sealed class ModeSelectScene : IScene
{
    private const float TitleSize = 120f;
    private const float SubtitleSize = 34f;
    private const float OptionTitleSize = 48f;
    private const float OptionTextSize = 26f;
    private const float ToggleTextSize = 36f;
    private const float HintSize = 26f;
    private const float CardWidth = 440f;
    private const float CardHeight = 240f;
    private const float ToggleHeight = 80f;
    private const float CardGap = 80f;
    private const float Border = 4f;
    private const float CardsTop = 0.34f;
    private const float TogglesTop = 0.61f;

    private const int CardsRow = 0;
    private const int TogglesRow = 1;
    private const int BackRow = 2;
    private const float BackTop = 0.79f;
    private const float ButtonWidth = 360f;

    private static readonly Color BackgroundColor = new(20, 21, 28);
    private static readonly Color TitleColor = new(240, 240, 245);
    private static readonly Color SubtitleColor = new(170, 175, 190);
    private static readonly Color CardColor = new(30, 33, 44);
    private static readonly Color CardSelectedColor = new(46, 52, 72);
    private static readonly Color BorderColor = new(70, 76, 98);
    private static readonly Color BorderSelectedColor = new(255, 225, 120);
    private static readonly Color TextColor = new(200, 205, 220);
    private static readonly Color NoteColor = new(130, 134, 150);
    private static readonly Color HintColor = new(150, 155, 170);
    private static readonly Color ToggleOnColor = new(120, 40, 40);
    private static readonly Color ToggleOnTextColor = new(255, 170, 140);

    private static readonly Toggle[] Toggles =
    {
        new("FAST", "Fast: weapons and enemies unlock about twice as early, more enemies come quicker"),
        new("NIGHTMARE", "Nightmare: every weapon from the start, waves as hard as wave 15 from wave 1"),
    };

    private readonly Option[] _options;
    private readonly IAudioManager _audio;
    private readonly IPlayerInput _input;
    private readonly Camera2D _camera;
    private readonly int _screenWidth;
    private readonly int _screenHeight;
    private readonly string[] _toggleLabels = new string[Toggles.Length];
    private int _row;
    private int _selectedCard;
    private int _selectedToggle;
    private bool _fast;
    private bool _nightmare;
    private string _modeLine = string.Empty;

    /// <param name="input">Player 1's input; drives the menu.</param>
    /// <param name="config">Game settings (screen size, camera).</param>
    /// <param name="selectedPlayers">The choice to highlight first, e.g. the last one made.</param>
    /// <param name="mode">The toggles as they were last time.</param>
    public ModeSelectScene(
        IPlayerInput input, GameConfig config, int selectedPlayers = 1, RunMode mode = default, IAudioManager? audio = null)
    {
        _audio = audio ?? new NullAudioManager();
        _audio.PlayMusic(MusicIds.Menu);
        _options = new Option[]
        {
            new(1, "1 PLAYER", "Keyboard + mouse or gamepad", null),
            new(
                2,
                "2 PLAYERS",
                "P1 keyboard + mouse, P2 gamepad",
                config.Debug.KeyboardPlayerTwo ? "Debug: P2 also on IJKL keys" : null),
        };
        _input = input;
        _screenWidth = config.VirtualWidth;
        _screenHeight = config.VirtualHeight;
        _camera = new Camera2D(new Rectangle(0, 0, _screenWidth, _screenHeight), config.Camera);
        _selectedCard = selectedPlayers == 2 ? 1 : 0;
        _fast = mode.Fast;
        _nightmare = mode.Nightmare;
        RefreshToggleLabels();
    }

    /// <summary>Number of players chosen; null until a choice is confirmed.</summary>
    public int? ChosenPlayers { get; private set; }

    /// <summary>True when the player went back to the main menu.</summary>
    public bool BackRequested { get; private set; }

    /// <summary>The toggles as they are set now.</summary>
    public RunMode Mode => new(_fast, _nightmare);

    public void Update(float deltaSeconds)
    {
        var before = (_row, _selectedCard, _selectedToggle);
        var modeBefore = Mode;
        UpdateMenu(deltaSeconds);
        if (ChosenPlayers is not null || Mode != modeBefore)
        {
            _audio.PlaySfx(SfxIds.UiConfirm);
        }
        else if (BackRequested)
        {
            _audio.PlaySfx(SfxIds.UiBack);
        }
        else if ((_row, _selectedCard, _selectedToggle) != before)
        {
            _audio.PlaySfx(SfxIds.UiMove);
        }
    }

    private void UpdateMenu(float deltaSeconds)
    {
        _input.Update(_camera.ViewCenter, _camera);
        if (_input.BackPressed)
        {
            BackRequested = true;
            return;
        }

        if (_input.MenuUpPressed)
        {
            _row = System.Math.Max(CardsRow, _row - 1);
        }

        if (_input.MenuDownPressed)
        {
            _row = System.Math.Min(BackRow, _row + 1);
        }

        if (_input.MenuLeftPressed)
        {
            SelectInRow(0);
        }

        if (_input.MenuRightPressed)
        {
            SelectInRow(1);
        }

        if (_input.HasPointer)
        {
            var card = CardAt(_input.AimScreen);
            var toggle = ToggleAt(_input.AimScreen);
            if (card >= 0)
            {
                _row = CardsRow;
                _selectedCard = card;
                if (_input.FirePressed)
                {
                    ChosenPlayers = _options[card].Players;
                    return;
                }
            }
            else if (toggle >= 0)
            {
                _row = TogglesRow;
                _selectedToggle = toggle;
                if (_input.FirePressed)
                {
                    Flip(toggle);
                }
            }
            else if (IsOnBackButton(_input.AimScreen))
            {
                _row = BackRow;
                if (_input.FirePressed)
                {
                    BackRequested = true;
                    return;
                }
            }
        }

        if (_input.ConfirmPressed)
        {
            if (_row == CardsRow)
            {
                ChosenPlayers = _options[_selectedCard].Players;
            }
            else if (_row == TogglesRow)
            {
                Flip(_selectedToggle);
            }
            else
            {
                BackRequested = true;
            }
        }
    }

    public void Draw(IRenderer renderer)
    {
        renderer.BeginScreen();
        renderer.FillRect(Vector2.Zero, new Vector2(_screenWidth, _screenHeight), BackgroundColor);

        var center = _screenWidth / 2f;
        renderer.DrawText("FOURGATE", new Vector2(center, _screenHeight * 0.12f), TitleSize, TitleColor, TextAlign.Center);
        renderer.DrawText(
            "Choose players and mode", new Vector2(center, _screenHeight * 0.12f + 140f), SubtitleSize, SubtitleColor,
            TextAlign.Center);

        for (var i = 0; i < _options.Length; i++)
        {
            DrawCard(renderer, i);
        }

        for (var i = 0; i < Toggles.Length; i++)
        {
            DrawToggle(renderer, i);
        }

        // Explains the highlighted toggle; on the cards row, the mode that will be played.
        var line = _row == TogglesRow ? Toggles[_selectedToggle].Description : _modeLine;
        renderer.DrawText(
            line, new Vector2(center, _screenHeight * TogglesTop + ToggleHeight + 22f), OptionTextSize, NoteColor,
            TextAlign.Center);

        DrawBackButton(renderer);

        var hint = _input.HasPointer
            ? "Arrows / WASD to move  ·  Enter: start on a card, switch on a toggle  ·  or click"
            : "D-pad to move  ·  A: start on a card, switch on a toggle";
        renderer.DrawText(hint, new Vector2(center, _screenHeight * 0.93f), HintSize, HintColor, TextAlign.Center);

        if (_input.HasPointer)
        {
            renderer.FillRect(_input.AimScreen - new Vector2(10f, 1f), new Vector2(20f, 2f), TitleColor);
            renderer.FillRect(_input.AimScreen - new Vector2(1f, 10f), new Vector2(2f, 20f), TitleColor);
        }

        renderer.End();
    }

    private void SelectInRow(int index)
    {
        if (_row == CardsRow)
        {
            _selectedCard = index;
        }
        else
        {
            _selectedToggle = index;
        }
    }

    private void Flip(int toggle)
    {
        if (toggle == 0)
        {
            _fast = !_fast;
        }
        else
        {
            _nightmare = !_nightmare;
        }

        RefreshToggleLabels();
    }

    // Built only when a toggle changes, so drawing allocates nothing.
    private void RefreshToggleLabels()
    {
        _toggleLabels[0] = $"{Toggles[0].Name}: {(_fast ? "ON" : "OFF")}";
        _toggleLabels[1] = $"{Toggles[1].Name}: {(_nightmare ? "ON" : "OFF")}";
        _modeLine = $"Mode: {Mode.Name}";
    }

    private bool IsOn(int toggle)
    {
        return toggle == 0 ? _fast : _nightmare;
    }

    private void DrawCard(IRenderer renderer, int index)
    {
        var option = _options[index];
        var selected = _row == CardsRow && index == _selectedCard;
        var topLeft = SlotTopLeft(index, CardsTop);
        var size = new Vector2(CardWidth, CardHeight);

        renderer.FillRect(topLeft, size, selected ? BorderSelectedColor : BorderColor);
        renderer.FillRect(topLeft + new Vector2(Border), size - new Vector2(Border * 2f), selected ? CardSelectedColor : CardColor);

        var middle = topLeft.X + CardWidth / 2f;
        renderer.DrawText(option.Title, new Vector2(middle, topLeft.Y + 60f), OptionTitleSize, TitleColor, TextAlign.Center);
        renderer.DrawText(
            option.Description, new Vector2(middle, topLeft.Y + 130f), OptionTextSize, TextColor, TextAlign.Center);
        if (option.Note is not null)
        {
            renderer.DrawText(option.Note, new Vector2(middle, topLeft.Y + 175f), OptionTextSize, NoteColor, TextAlign.Center);
        }
    }

    private void DrawToggle(IRenderer renderer, int index)
    {
        var selected = _row == TogglesRow && index == _selectedToggle;
        var on = IsOn(index);
        var topLeft = SlotTopLeft(index, TogglesTop);
        var size = new Vector2(CardWidth, ToggleHeight);

        renderer.FillRect(topLeft, size, selected ? BorderSelectedColor : BorderColor);
        var fill = on ? ToggleOnColor : selected ? CardSelectedColor : CardColor;
        renderer.FillRect(topLeft + new Vector2(Border), size - new Vector2(Border * 2f), fill);
        renderer.DrawTextCentered(_toggleLabels[index], topLeft + size / 2f, ToggleTextSize, on ? ToggleOnTextColor : TextColor);
    }

    // Cards and toggles share the same two columns.
    private Vector2 SlotTopLeft(int index, float topShare)
    {
        var totalWidth = 2 * CardWidth + CardGap;
        var left = (_screenWidth - totalWidth) / 2f + index * (CardWidth + CardGap);
        return new Vector2(left, _screenHeight * topShare);
    }

    private Vector2 BackButtonTopLeft => new((_screenWidth - ButtonWidth) / 2f, _screenHeight * BackTop);

    private bool IsOnBackButton(Vector2 screen)
    {
        var topLeft = BackButtonTopLeft;
        return screen.X >= topLeft.X && screen.X <= topLeft.X + ButtonWidth
            && screen.Y >= topLeft.Y && screen.Y <= topLeft.Y + ToggleHeight;
    }

    private void DrawBackButton(IRenderer renderer)
    {
        var selected = _row == BackRow;
        var topLeft = BackButtonTopLeft;
        var size = new Vector2(ButtonWidth, ToggleHeight);
        renderer.FillRect(topLeft, size, selected ? BorderSelectedColor : BorderColor);
        renderer.FillRect(topLeft + new Vector2(Border), size - new Vector2(Border * 2f), selected ? CardSelectedColor : CardColor);
        renderer.DrawTextCentered("BACK", topLeft + size / 2f, ToggleTextSize, TextColor);
    }

    private int CardAt(Vector2 screen)
    {
        return SlotAt(screen, CardsTop, CardHeight, _options.Length);
    }

    private int ToggleAt(Vector2 screen)
    {
        return SlotAt(screen, TogglesTop, ToggleHeight, Toggles.Length);
    }

    private int SlotAt(Vector2 screen, float topShare, float height, int count)
    {
        for (var i = 0; i < count; i++)
        {
            var topLeft = SlotTopLeft(i, topShare);
            if (screen.X >= topLeft.X && screen.X <= topLeft.X + CardWidth
                && screen.Y >= topLeft.Y && screen.Y <= topLeft.Y + height)
            {
                return i;
            }
        }

        return -1;
    }

    private readonly record struct Option(int Players, string Title, string Description, string? Note);

    private readonly record struct Toggle(string Name, string Description);
}
