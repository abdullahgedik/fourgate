using System;
using Fourgate.Core;
using Fourgate.Core.Rendering;
using Microsoft.Xna.Framework;

namespace Fourgate.UI;

/// <summary>
/// The boss's entrance: a dark band sweeps across the screen, "BOSS" appears and its name slides
/// in, holds a moment and fades away. The game keeps running underneath.
/// </summary>
public sealed class BossIntro
{
    private const float InSeconds = 0.45f;
    private const float HoldSeconds = 2.2f;
    private const float OutSeconds = 0.6f;
    private const float BandHeight = 170f;
    private const float BandY = 0.3f;
    private const float NameSize = 96f;
    private const float LabelSize = 30f;
    private const float SlideDistance = 260f;

    private static readonly Color BandColor = new Color(0, 0, 0) * 0.6f;
    private static readonly Color EdgeColor = new(200, 40, 35);
    private static readonly Color NameColor = new(255, 225, 150);
    private static readonly Color LabelColor = new(230, 70, 60);

    private string _name = string.Empty;
    private float _age = float.MaxValue;

    /// <summary>True while it is on screen.</summary>
    public bool IsShowing => _age < InSeconds + HoldSeconds + OutSeconds;

    public void Start(string name)
    {
        _name = name.ToUpperInvariant();
        _age = 0f;
    }

    public void Update(float deltaSeconds)
    {
        if (IsShowing)
        {
            _age += deltaSeconds;
        }
    }

    /// <param name="renderer">Renderer in a screen-space pass.</param>
    public void Draw(IRenderer renderer, int screenWidth, int screenHeight)
    {
        if (!IsShowing)
        {
            return;
        }

        var shown = Ease.OutCubic(_age / InSeconds);
        var fade = 1f - Ease.InCubic((_age - InSeconds - HoldSeconds) / OutSeconds);
        var opacity = Math.Min(shown, fade);
        var center = new Vector2(screenWidth / 2f, screenHeight * BandY);

        // The band opens from a line to its full height; thin red edges frame it.
        var height = BandHeight * shown;
        var top = center.Y - height / 2f;
        renderer.FillRect(new Vector2(0f, top), new Vector2(screenWidth, height), BandColor * fade);
        renderer.FillRect(new Vector2(0f, top - 3f), new Vector2(screenWidth, 3f), EdgeColor * opacity);
        renderer.FillRect(new Vector2(0f, top + height), new Vector2(screenWidth, 3f), EdgeColor * opacity);

        var slide = (1f - shown) * SlideDistance;
        renderer.DrawTextCentered("BOSS", center + new Vector2(-slide * 0.5f, -48f), LabelSize, LabelColor * opacity);
        renderer.DrawTextCentered(_name, center + new Vector2(slide, 18f), NameSize, NameColor * opacity);
    }
}
