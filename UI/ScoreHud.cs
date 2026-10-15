using System.Globalization;
using Fourgate.Core.Rendering;
using Microsoft.Xna.Framework;

namespace Fourgate.UI;

/// <summary>The team score in the top-right corner, with the mode played under it.</summary>
public sealed class ScoreHud
{
    private const float RightMargin = 30f;
    private const float Top = 18f;
    private const float ScoreSize = 36f;
    private const float ModeSize = 22f;
    private const float ModeGap = 40f;

    private static readonly Color ScoreColor = new(240, 240, 245);
    private static readonly Color ModeColor = new(255, 140, 110);
    private static readonly Color ShadowColor = new(0, 0, 0, 160);

    private int _shownScore = -1;
    private string _scoreText = string.Empty;

    /// <param name="renderer">Renderer in a screen-space pass.</param>
    /// <param name="score">Score to show.</param>
    /// <param name="modeLabel">Mode name, or empty for Normal.</param>
    /// <param name="screenWidth">Width of the virtual screen.</param>
    public void Draw(IRenderer renderer, int score, string modeLabel, int screenWidth)
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
    }
}
