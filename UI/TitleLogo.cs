using System;
using Fourgate.Core.Rendering;
using Microsoft.Xna.Framework;

namespace Fourgate.UI;

/// <summary>
/// The game's name: "FOUR" in bone white and "GATE" smouldering, each letter slowly shifting
/// between blood red and fire orange at its own pace, with a glow breathing behind the word.
/// </summary>
public sealed class TitleLogo
{
    private const string First = "FOUR";
    private const string Second = "GATE";

    // Where the middle of the capitals sits, as a share of the font size below the text position.
    private const float CapMiddle = 0.5f;

    // How fast the letters' heat shifts: two slow waves at odd speeds, so it never looks looped.
    private const float HeatSpeed = 1.6f;
    private const float HeatSpeedSecond = 2.9f;
    private const float BreathSpeed = 1.2f;

    private static readonly Color WhiteColor = new(240, 234, 226);
    private static readonly Color ShadowColor = new Color(0, 0, 0) * 0.75f;
    private static readonly Color DeepRed = new(120, 10, 8);
    private static readonly Color BloodRed = new(205, 28, 16);
    private static readonly Color FireOrange = new(255, 120, 30);
    private static readonly Color HotYellow = new(255, 215, 110);

    private readonly float _size;
    private readonly float[] _letterOffsets = new float[Second.Length];
    private readonly string[] _letters = new string[Second.Length];
    private float _firstWidth = -1f;
    private float _secondWidth;
    private float _time;

    public TitleLogo(float size)
    {
        _size = size;
        for (var i = 0; i < Second.Length; i++)
        {
            _letters[i] = Second[i].ToString();
        }
    }

    public void Update(float deltaSeconds)
    {
        _time += deltaSeconds;
    }

    /// <param name="renderer">Renderer in a screen pass.</param>
    /// <param name="center">Middle of the whole name.</param>
    public void Draw(IRenderer renderer, Vector2 center)
    {
        Measure(renderer);
        var left = center.X - (_firstWidth + _secondWidth) / 2f;
        var top = center.Y - _size * CapMiddle;
        var gateLeft = left + _firstWidth;
        var gateMiddle = new Vector2(gateLeft + _secondWidth / 2f, top + _size * CapMiddle);

        // A glow breathing behind the burning half.
        var breath = 0.75f + 0.25f * MathF.Sin(_time * BreathSpeed);
        renderer.FillGlow(gateMiddle, _secondWidth * 0.9f, Light.Of(BloodRed, 0.4f * breath));
        renderer.FillGlow(gateMiddle, _secondWidth * 0.55f, Light.Of(FireOrange, 0.2f * breath));

        renderer.DrawText(First, new Vector2(left + 5f, top + 6f), _size, ShadowColor);
        renderer.DrawText(First, new Vector2(left, top), _size, WhiteColor);

        for (var i = 0; i < _letters.Length; i++)
        {
            var at = new Vector2(gateLeft + _letterOffsets[i], top);
            var heat = Heat(i);
            renderer.DrawText(_letters[i], at + new Vector2(5f, 6f), _size, ShadowColor);
            renderer.DrawText(_letters[i], at + new Vector2(0f, 3f), _size, DeepRed);
            renderer.DrawText(_letters[i], at, _size, Color.Lerp(BloodRed, FireOrange, heat));
            renderer.DrawText(_letters[i], at + new Vector2(0f, -2f), _size, Light.Of(HotYellow, 0.35f * heat * heat));
        }
    }

    // How hot a letter burns now, 0–1: a slow, uneven shift, different for each letter.
    private float Heat(int letter)
    {
        var a = MathF.Sin(_time * HeatSpeed + letter * 1.9f);
        var b = MathF.Sin(_time * HeatSpeedSecond + letter * 0.7f);
        return Math.Clamp(0.55f + 0.3f * a + 0.15f * b, 0f, 1f);
    }

    // The widths are measured once, with the renderer's font.
    private void Measure(IRenderer renderer)
    {
        if (_firstWidth >= 0f)
        {
            return;
        }

        _firstWidth = renderer.MeasureText(First, _size);
        _secondWidth = renderer.MeasureText(Second, _size);
        for (var i = 0; i < Second.Length; i++)
        {
            _letterOffsets[i] = renderer.MeasureText(Second.Substring(0, i), _size);
        }
    }
}
