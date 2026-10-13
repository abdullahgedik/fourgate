using System;
using Fourgate.Core.Rendering;
using Fourgate.Systems;
using Microsoft.Xna.Framework;

namespace Fourgate.UI;

/// <summary>Top-center readout: wave number and enemies left, or the countdown to the next wave.</summary>
public sealed class WaveHud
{
    private const float TitleSize = 44f;
    private const float DetailSize = 28f;
    private const float TopMargin = 18f;
    private const float LineGap = 44f;

    private static readonly Color TitleColor = new(240, 240, 245);
    private static readonly Color DetailColor = new(190, 195, 210);
    private static readonly Color ShadowColor = new(0, 0, 0, 160);

    // The texts are rebuilt only when their numbers change, to avoid allocating every frame.
    private int _titleWave = -1;
    private string _title = string.Empty;
    private int _detailValue = -1;
    private WavePhase _detailPhase;
    private string _detail = string.Empty;

    /// <param name="renderer">Renderer in a screen-space pass.</param>
    /// <param name="waves">Wave state to show.</param>
    /// <param name="screenWidth">Width of the virtual screen.</param>
    public void Draw(IRenderer renderer, WaveSystem waves, int screenWidth)
    {
        if (waves.Wave != _titleWave)
        {
            _titleWave = waves.Wave;
            _title = $"WAVE {waves.Wave}";
        }

        var value = waves.Phase == WavePhase.Rest
            ? (int)MathF.Ceiling(waves.RestSecondsLeft)
            : waves.EnemiesRemaining;
        if (value != _detailValue || waves.Phase != _detailPhase)
        {
            _detailValue = value;
            _detailPhase = waves.Phase;
            _detail = waves.Phase == WavePhase.Rest ? $"STARTS IN {value}" : $"ENEMIES {value}";
        }

        var center = screenWidth / 2f;
        DrawShadowed(renderer, _title, new Vector2(center, TopMargin), TitleSize, TitleColor);
        DrawShadowed(renderer, _detail, new Vector2(center, TopMargin + LineGap), DetailSize, DetailColor);
    }

    private static void DrawShadowed(IRenderer renderer, string text, Vector2 position, float size, Color color)
    {
        renderer.DrawText(text, position + new Vector2(2f, 2f), size, ShadowColor, TextAlign.Center);
        renderer.DrawText(text, position, size, color, TextAlign.Center);
    }
}
