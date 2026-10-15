using Fourgate.Core.Input;
using Fourgate.Core.Rendering;
using Microsoft.Xna.Framework;

namespace Fourgate.UI;

/// <summary>The pause menu over a frozen run: Resume, Settings, Quit to menu.</summary>
public sealed class PauseOverlay
{
    private const float TitleSize = 100f;
    private const float HintSize = 26f;

    private static readonly Color DimColor = new(0, 0, 0, 190);
    private static readonly Color TitleColor = new(240, 240, 245);
    private static readonly Color HintColor = new(150, 155, 170);

    private static readonly string[] Labels = { "RESUME", "SETTINGS", "QUIT TO MENU" };
    private static readonly PauseChoice[] Choices = { PauseChoice.Resume, PauseChoice.Settings, PauseChoice.QuitToMenu };

    private readonly MenuList _menu;
    private readonly int _screenWidth;
    private readonly int _screenHeight;
    private int _selected;

    /// <summary>The highlighted button.</summary>
    public int Selected => _selected;

    public PauseOverlay(int screenWidth, int screenHeight)
    {
        _screenWidth = screenWidth;
        _screenHeight = screenHeight;
        _menu = new MenuList(new Vector2(screenWidth / 2f, screenHeight * 0.4f), 460f, 80f, 24f);
    }

    /// <summary>Highlights Resume again; call when the menu opens.</summary>
    public void Open()
    {
        _selected = 0;
    }

    /// <summary>Reads one player's input; returns what they picked, or null while they are still choosing.</summary>
    public PauseChoice? Update(IPlayerInput input)
    {
        if (input.PausePressed || input.BackPressed)
        {
            return PauseChoice.Resume;
        }

        if (input.MenuUpPressed)
        {
            _selected = (_selected + Labels.Length - 1) % Labels.Length;
        }

        if (input.MenuDownPressed)
        {
            _selected = (_selected + 1) % Labels.Length;
        }

        if (input.HasPointer)
        {
            var hovered = _menu.ButtonAt(input.AimScreen, Labels.Length);
            if (hovered >= 0)
            {
                _selected = hovered;
                if (input.FirePressed)
                {
                    return Choices[hovered];
                }
            }
        }

        return input.ConfirmPressed ? Choices[_selected] : null;
    }

    public void Draw(IRenderer renderer)
    {
        renderer.FillRect(Vector2.Zero, new Vector2(_screenWidth, _screenHeight), DimColor);
        renderer.DrawText("PAUSED", new Vector2(_screenWidth / 2f, _screenHeight * 0.18f), TitleSize, TitleColor, TextAlign.Center);
        _menu.Draw(renderer, Labels, _selected);
        renderer.DrawText(
            "Esc / Start: resume", new Vector2(_screenWidth / 2f, _screenHeight * 0.85f), HintSize, HintColor, TextAlign.Center);
    }
}
