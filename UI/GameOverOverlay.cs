using System.Collections.Generic;
using System.Globalization;
using Fourgate.Core.Persistence;
using Fourgate.Core.Rendering;
using Microsoft.Xna.Framework;

namespace Fourgate.UI;

/// <summary>
/// Full-screen game over panel: the run's stats, then either the initials entry for a new high
/// score or the mode's high-score list.
/// </summary>
public sealed class GameOverOverlay
{
    private const float TitleSize = 100f;
    private const float StatSize = 40f;
    private const float DetailSize = 30f;
    private const float PromptSize = 30f;
    private const float HeadingSize = 44f;
    private const float HintSize = 26f;

    // How far below its place the panel starts while fading in.
    private const float RiseDistance = 40f;

    private static readonly Color DimColor = new(0, 0, 0, 200);
    private static readonly Color TitleColor = new(230, 60, 60);
    private static readonly Color StatColor = new(235, 235, 240);
    private static readonly Color ScoreColor = new(255, 225, 120);
    private static readonly Color PromptColor = new(170, 175, 190);

    private readonly HighScoreListView _list = new();
    private string _scoreText = string.Empty;
    private string _detailText = string.Empty;
    private string _listTitle = string.Empty;

    /// <summary>Heading of the screen ("GAME OVER", or the waves-survived title after a finished campaign).</summary>
    public string Title { get; set; } = "GAME OVER";

    /// <summary>Sets the numbers to show; call once when the run ends.</summary>
    public void SetStats(int wave, int kills, float elapsedSeconds, int score, string modeName, int bestCombo = 0)
    {
        var totalSeconds = (int)elapsedSeconds;
        _scoreText = $"SCORE   {score.ToString("N0", CultureInfo.InvariantCulture)}   ({modeName})";
        _detailText = $"WAVE {wave}   ·   KILLS {kills}   ·   BEST COMBO {bestCombo}   ·   TIME {totalSeconds / 60}:{totalSeconds % 60:00}";
        _listTitle = $"HIGH SCORES  ·  {modeName}";
    }

    /// <param name="renderer">Renderer in a screen-space pass.</param>
    /// <param name="screenWidth">Width of the virtual screen.</param>
    /// <param name="screenHeight">Height of the virtual screen.</param>
    /// <param name="initials">The initials being entered, or null.</param>
    /// <param name="entries">The mode's list, or null when there is no high-score table.</param>
    /// <param name="version">The table's version.</param>
    /// <param name="highlight">Place of the new entry, or -1.</param>
    /// <param name="opacity">0–1: the panel fades in and drifts up into place as it rises.</param>
    public void Draw(
        IRenderer renderer, int screenWidth, int screenHeight, InitialsEntry? initials,
        IReadOnlyList<HighScoreEntry>? entries, int version, int highlight, float opacity = 1f)
    {
        renderer.FillRect(Vector2.Zero, new Vector2(screenWidth, screenHeight), DimColor * opacity);

        var center = screenWidth / 2f;
        var drift = (1f - opacity) * RiseDistance;
        var top = screenHeight * 0.07f + drift;
        renderer.DrawText(Title, new Vector2(center, top), TitleSize, TitleColor * opacity, TextAlign.Center);
        renderer.DrawText(_scoreText, new Vector2(center, top + 130f), StatSize, ScoreColor * opacity, TextAlign.Center);
        renderer.DrawText(_detailText, new Vector2(center, top + 185f), DetailSize, StatColor * opacity, TextAlign.Center);

        var sectionTop = screenHeight * 0.38f + drift;
        if (initials is { IsDone: false })
        {
            renderer.DrawText(
                "NEW HIGH SCORE!", new Vector2(center, sectionTop), HeadingSize, ScoreColor * opacity, TextAlign.Center);
            initials.Draw(renderer, new Vector2(center, sectionTop + 80f), opacity);
            renderer.DrawText(
                "Up / Down: letter   ·   Left / Right: move   ·   Enter / A: save   ·   Esc / B: don't save",
                new Vector2(center, sectionTop + 220f), HintSize, PromptColor * opacity, TextAlign.Center);
            return;
        }

        if (entries is not null)
        {
            _list.Draw(renderer, _listTitle, entries, version, new Vector2(center, sectionTop), highlight, opacity);
        }

        renderer.DrawText(
            "PRESS ENTER TO CONTINUE", new Vector2(center, screenHeight * 0.92f + drift), PromptSize, PromptColor * opacity,
            TextAlign.Center);
    }
}
