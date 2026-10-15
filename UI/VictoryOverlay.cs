using System.Globalization;
using Fourgate.Core.Input;
using Fourgate.Core.Rendering;
using Microsoft.Xna.Framework;

namespace Fourgate.UI;

/// <summary>Shown once the last boss wave is cleared: keep going, or end the run here.</summary>
public sealed class VictoryOverlay
{
    private const float TitleSize = 96f;
    private const float TextSize = 34f;

    private static readonly Color DimColor = new(0, 0, 0, 200);
    private static readonly Color TitleColor = new(255, 225, 120);
    private static readonly Color TextColor = new(225, 228, 238);

    private static readonly string[] Labels = { "CONTINUE", "END RUN" };

    private readonly MenuList _menu;
    private readonly int _screenWidth;
    private readonly int _screenHeight;
    private string _title = string.Empty;
    private string _scoreText = string.Empty;
    private int _selected;

    /// <summary>The highlighted button.</summary>
    public int Selected => _selected;

    public VictoryOverlay(int screenWidth, int screenHeight)
    {
        _screenWidth = screenWidth;
        _screenHeight = screenHeight;
        _menu = new MenuList(new Vector2(screenWidth / 2f, screenHeight * 0.55f), 460f, 80f, 24f);
    }

    /// <summary>Sets the text; call once when the screen opens.</summary>
    public void Open(int wavesSurvived, int score)
    {
        _title = $"{wavesSurvived} WAVES SURVIVED";
        _scoreText = $"SCORE  {score.ToString("N0", CultureInfo.InvariantCulture)}   ·   Continue: the waves keep getting harder";
        _selected = 0;
    }

    /// <summary>Reads one player's input; true = continue, false = end the run, null = still choosing.</summary>
    public bool? Update(IPlayerInput input)
    {
        if (input.MenuUpPressed || input.MenuDownPressed)
        {
            _selected = 1 - _selected;
        }

        if (input.HasPointer)
        {
            var hovered = _menu.ButtonAt(input.AimScreen, Labels.Length);
            if (hovered >= 0)
            {
                _selected = hovered;
                if (input.FirePressed)
                {
                    return hovered == 0;
                }
            }
        }

        return input.ConfirmPressed ? _selected == 0 : null;
    }

    public void Draw(IRenderer renderer)
    {
        renderer.FillRect(Vector2.Zero, new Vector2(_screenWidth, _screenHeight), DimColor);
        renderer.DrawText(_title, new Vector2(_screenWidth / 2f, _screenHeight * 0.2f), TitleSize, TitleColor, TextAlign.Center);
        renderer.DrawText(_scoreText, new Vector2(_screenWidth / 2f, _screenHeight * 0.36f), TextSize, TextColor, TextAlign.Center);
        _menu.Draw(renderer, Labels, _selected);
    }
}
