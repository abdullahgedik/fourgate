using System.Globalization;
using Fourgate.Core.Rendering;
using Microsoft.Xna.Framework;

namespace Fourgate.UI;

/// <summary>
/// The team score in the top-right corner, with the mode played under it and, during a kill
/// combo, the combo's multiplier over a bar that drains until the next kill is due.
/// </summary>
public sealed class ScoreHud
{
    private const float RightMargin = 30f;
    private const float Top = 18f;
    private const float ScoreSize = 36f;
    private const float ModeSize = 22f;
    private const float ModeGap = 40f;
    private const float ComboGap = 72f;
    private const float ComboSize = 34f;
    private const float ComboBarGap = 40f;
    private const float ComboBarWidth = 180f;
    private const float ComboBarHeight = 6f;

    // A combo is shown from its second kill on.
    private const int ShownFrom = 2;

    private static readonly Color ScoreColor = new(240, 240, 245);
    private static readonly Color ModeColor = new(255, 140, 110);
    private static readonly Color ShadowColor = new(0, 0, 0, 160);
    private static readonly Color ComboColor = new(255, 200, 70);
    private static readonly Color ComboBarBack = new(0, 0, 0, 140);

    private int _shownScore = -1;
    private string _scoreText = string.Empty;
    private float _shownMultiplier = -1f;
    private string _comboText = string.Empty;

    /// <param name="renderer">Renderer in a screen-space pass.</param>
    /// <param name="score">Score to show.</param>
    /// <param name="modeLabel">Mode name, or empty for Normal.</param>
    /// <param name="screenWidth">Width of the virtual screen.</param>
    /// <param name="combo">Kills in the current combo.</param>
    /// <param name="multiplier">The multiplier the combo gives.</param>
    /// <param name="timeLeft">0–1: how much of the combo window is left.</param>
    public void Draw(
        IRenderer renderer, int score, string modeLabel, int screenWidth, int combo = 0, float multiplier = 1f,
        float timeLeft = 0f)
    {
        // The text is only rebuilt when the score changes.
        if (score != _shownScore)
        {
            _shownScore = score;
            _scoreText = "SCORE " + score.ToString("N0", CultureInfo.InvariantCulture);
        }

        var right = new Vector2(screenWidth - RightMargin, Top);
        renderer.DrawText(_scoreText, right + new Vector2(2f, 2f), ScoreSize, ShadowColor, TextAlign.Right);
        renderer.DrawText(_scoreText, right, ScoreSize, ScoreColor, TextAlign.Right);
        if (modeLabel.Length > 0)
        {
            renderer.DrawText(modeLabel, right + new Vector2(0f, ModeGap), ModeSize, ModeColor, TextAlign.Right);
        }

        if (combo < ShownFrom)
        {
            return;
        }

        if (multiplier != _shownMultiplier)
        {
            _shownMultiplier = multiplier;
            _comboText = "COMBO x" + multiplier.ToString("0.#", CultureInfo.InvariantCulture);
        }

        var comboAt = right + new Vector2(0f, ComboGap);
        renderer.DrawText(_comboText, comboAt + new Vector2(2f, 2f), ComboSize, ShadowColor, TextAlign.Right);
        renderer.DrawText(_comboText, comboAt, ComboSize, ComboColor, TextAlign.Right);

        var barTopLeft = comboAt + new Vector2(-ComboBarWidth, ComboBarGap);
        renderer.FillRect(barTopLeft, new Vector2(ComboBarWidth, ComboBarHeight), ComboBarBack);
        var filled = ComboBarWidth * timeLeft;
        renderer.FillRect(
            barTopLeft + new Vector2(ComboBarWidth - filled, 0f), new Vector2(filled, ComboBarHeight), ComboColor);
    }
}
