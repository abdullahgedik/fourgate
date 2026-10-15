using System.Globalization;
using Fourgate.Core.Rendering;
using Microsoft.Xna.Framework;

namespace Fourgate.UI;

/// <summary>Full-screen game over panel with the run's stats.</summary>
public sealed class GameOverOverlay
{
    private const float TitleSize = 110f;
    private const float StatSize = 40f;
    private const float PromptSize = 30f;
    private const float StatLineHeight = 52f;

    private static readonly Color DimColor = new(0, 0, 0, 185);
    private static readonly Color TitleColor = new(230, 60, 60);
    private static readonly Color StatColor = new(235, 235, 240);
    private static readonly Color PromptColor = new(170, 175, 190);
    private static readonly Color ScoreColor = new(255, 225, 120);

    private string _waveText = string.Empty;
    private string _killsText = string.Empty;
    private string _timeText = string.Empty;
    private string _scoreText = string.Empty;

    /// <summary>Sets the numbers to show; call once when the run ends.</summary>
    public void SetStats(int wave, int kills, float elapsedSeconds, int score, string modeName)
    {
        var totalSeconds = (int)elapsedSeconds;
        _scoreText = $"SCORE   {score.ToString("N0", CultureInfo.InvariantCulture)}   ({modeName})";
        _waveText = $"WAVE REACHED   {wave}";
        _killsText = $"KILLS   {kills}";
        _timeText = $"TIME   {totalSeconds / 60}:{totalSeconds % 60:00}";
    }

    /// <param name="renderer">Renderer in a screen-space pass.</param>
    /// <param name="screenWidth">Width of the virtual screen.</param>
    /// <param name="screenHeight">Height of the virtual screen.</param>
    public void Draw(IRenderer renderer, int screenWidth, int screenHeight)
    {
        renderer.FillRect(Vector2.Zero, new Vector2(screenWidth, screenHeight), DimColor);

        var center = screenWidth / 2f;
        var top = screenHeight * 0.26f;
        renderer.DrawText("GAME OVER", new Vector2(center, top), TitleSize, TitleColor, TextAlign.Center);

        var statsTop = top + TitleSize + 50f;
        renderer.DrawText(_scoreText, new Vector2(center, statsTop), StatSize, ScoreColor, TextAlign.Center);
        statsTop += StatLineHeight * 1.4f;
        renderer.DrawText(_waveText, new Vector2(center, statsTop), StatSize, StatColor, TextAlign.Center);
        renderer.DrawText(
            _killsText, new Vector2(center, statsTop + StatLineHeight), StatSize, StatColor, TextAlign.Center);
        renderer.DrawText(
            _timeText, new Vector2(center, statsTop + StatLineHeight * 2f), StatSize, StatColor, TextAlign.Center);

        renderer.DrawText(
            "PRESS ENTER TO CONTINUE",
            new Vector2(center, statsTop + StatLineHeight * 3f + 50f),
            PromptSize,
            PromptColor,
            TextAlign.Center);
    }
}
