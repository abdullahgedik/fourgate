using Fourgate.Core.Input;
using Fourgate.Core.Rendering;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace Fourgate.UI;

/// <summary>
/// The key list inside Settings: pick an action, press its new key. A key already in use swaps
/// with the action's old one; Esc cancels. RESET TO DEFAULTS and BACK close the list.
/// </summary>
public sealed class ControlsMenu
{
    private const float TitleSize = 64f;
    private const float HintSize = 26f;
    private const float RowHeight = 56f;
    private const float RowGap = 8f;
    private const float RefusedSeconds = 1.5f;

    private static readonly Color TitleColor = new(240, 240, 245);
    private static readonly Color HintColor = new(240, 226, 210);
    private static readonly Color ListenColor = new(255, 225, 120);
    private static readonly Color RefusedColor = new(255, 120, 100);

    private readonly KeyBindings _bindings;
    private readonly int _screenWidth;
    private readonly int _screenHeight;
    private readonly MenuList _menu;
    private readonly string[] _labels;
    private readonly int _resetRow;
    private readonly int _backRow;
    private int _selected;
    private int _listeningRow = -1;
    private float _refusedLeft;
    private string _listenHint = string.Empty;

    public ControlsMenu(KeyBindings bindings, int screenWidth, int screenHeight)
    {
        _bindings = bindings;
        _screenWidth = screenWidth;
        _screenHeight = screenHeight;
        _resetRow = KeyBindings.Actions.Count;
        _backRow = _resetRow + 1;
        _labels = new string[_backRow + 1];
        _menu = new MenuList(new Vector2(screenWidth / 2f, screenHeight * 0.13f), 760f, RowHeight, RowGap);
        Refresh();
    }

    public bool IsOpen { get; private set; }

    public void Open()
    {
        IsOpen = true;
        _selected = 0;
        _listeningRow = -1;
        Refresh();
    }

    /// <summary>Handles one frame of input (already updated). Returns true when something changed.</summary>
    public bool Update(IPlayerInput input, IKeyCapture keys, float deltaSeconds)
    {
        _refusedLeft = System.Math.Max(0f, _refusedLeft - deltaSeconds);
        if (_listeningRow >= 0)
        {
            return Listen(keys.KeyPressed);
        }

        if (input.BackPressed)
        {
            IsOpen = false;
            return true;
        }

        var rows = _labels.Length;
        if (input.MenuUpPressed)
        {
            _selected = (_selected + rows - 1) % rows;
        }

        if (input.MenuDownPressed)
        {
            _selected = (_selected + 1) % rows;
        }

        var clicked = false;
        if (input.HasPointer)
        {
            var hovered = _menu.ButtonAt(input.AimScreen, rows);
            if (hovered >= 0)
            {
                _selected = hovered;
                clicked = input.FirePressed;
            }
        }

        if (!clicked && !input.ConfirmPressed)
        {
            return false;
        }

        if (_selected == _backRow)
        {
            IsOpen = false;
        }
        else if (_selected == _resetRow)
        {
            _bindings.Reset();
        }
        else
        {
            // The key that chose the row is still down; only a key pressed after it counts.
            _listeningRow = _selected;
            _listenHint = $"Press a key for {KeyBindings.NameOf(KeyBindings.Actions[_selected])}   ·   Esc: cancel";
        }

        Refresh();
        return true;
    }

    public void Draw(IRenderer renderer, IPlayerInput input)
    {
        renderer.DrawText(
            "CONTROLS", new Vector2(_screenWidth / 2f, _screenHeight * 0.05f), TitleSize, TitleColor, TextAlign.Center);
        _menu.Draw(renderer, _labels, _selected);

        string hint;
        var color = HintColor;
        if (_refusedLeft > 0f)
        {
            hint = "That key is fixed (Esc, Enter, 1-9, F-keys); pick another";
            color = RefusedColor;
        }
        else if (_listeningRow >= 0)
        {
            hint = _listenHint;
            color = ListenColor;
        }
        else
        {
            hint = "Enter or click: change   ·   Esc: back   ·   Fixed: left mouse fire, mouse wheel / 1-9 guns, Esc pause";
        }

        HellBackdrop.DrawHint(renderer, hint, new Vector2(_screenWidth / 2f, _screenHeight * 0.955f), HintSize, color);
        if (input.HasPointer)
        {
            renderer.FillRect(input.AimScreen - new Vector2(10f, 1f), new Vector2(20f, 2f), TitleColor);
            renderer.FillRect(input.AimScreen - new Vector2(1f, 10f), new Vector2(2f, 20f), TitleColor);
        }
    }

    private bool Listen(Keys key)
    {
        if (key == Keys.None)
        {
            return false;
        }

        if (key == Keys.Escape)
        {
            _listeningRow = -1;
        }
        else if (_bindings.Assign(KeyBindings.Actions[_listeningRow], key))
        {
            _listeningRow = -1;
        }
        else
        {
            _refusedLeft = RefusedSeconds;
            return false;
        }

        Refresh();
        return true;
    }

    // Built only when something changes, so drawing allocates nothing.
    private void Refresh()
    {
        for (var i = 0; i < KeyBindings.Actions.Count; i++)
        {
            var action = KeyBindings.Actions[i];
            var key = i == _listeningRow ? "..." : KeyBindings.NameOf(_bindings.Get(action));
            _labels[i] = $"{KeyBindings.NameOf(action)}:  {key}";
        }

        _labels[_resetRow] = "RESET TO DEFAULTS";
        _labels[_backRow] = "BACK";
    }
}
