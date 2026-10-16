using System;
using Fourgate.Core.Rendering;
using Fourgate.Core.Rng;
using Microsoft.Xna.Framework;

namespace Fourgate.UI;

/// <summary>
/// The game's name: "FOUR" in bone white and "GATE" blazing, each letter flickering between
/// blood red and fire orange with flames licking up off the word and a glow breathing behind it.
/// </summary>
public sealed class TitleLogo
{
    private const string First = "FOUR";
    private const string Second = "GATE";
    private const int FlameCount = 70;

    // Where the tops and middles of the capitals sit, as a share of the font size below the text position.
    private const float CapTop = 0.22f;
    private const float CapMiddle = 0.5f;

    private static readonly Color WhiteColor = new(240, 234, 226);
    private static readonly Color ShadowColor = new Color(0, 0, 0) * 0.75f;
    private static readonly Color DeepRed = new(120, 10, 8);
    private static readonly Color BloodRed = new(205, 28, 16);
    private static readonly Color FireOrange = new(255, 120, 30);
    private static readonly Color HotYellow = new(255, 215, 110);

    private readonly float _size;
    private readonly GameRandom _random = new(53);
    private readonly float[] _letterOffsets = new float[Second.Length];
    private readonly string[] _letters = new string[Second.Length];
    private readonly Vector2[] _flamePosition = new Vector2[FlameCount];
    private readonly float[] _flameAge = new float[FlameCount];
    private readonly float[] _flameLife = new float[FlameCount];
    private readonly float[] _flameSize = new float[FlameCount];
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

        for (var i = 0; i < FlameCount; i++)
        {
            _flameAge[i] = _random.Range(0f, 1f);
            _flameLife[i] = _random.Range(0.5f, 1.1f);
            _flamePosition[i] = new Vector2(_random.Range(0f, 1f), _random.Range(-0.3f, 0.1f) * _size);
            _flameSize[i] = _random.Range(0.08f, 0.16f) * _size;
        }
    }

    public void Update(float deltaSeconds)
    {
        _time += deltaSeconds;
        for (var i = 0; i < FlameCount; i++)
        {
            _flameAge[i] += deltaSeconds / _flameLife[i];
            _flamePosition[i].Y -= (40f + 50f * (1f - _flameAge[i])) * deltaSeconds;
            _flamePosition[i].X += MathF.Sin(_time * 5f + i * 1.3f) * 14f * deltaSeconds;
            if (_flameAge[i] >= 1f)
            {
                // A new tongue somewhere along the top of the word (placed relative to it; see Draw).
                _flameAge[i] = 0f;
                _flameLife[i] = _random.Range(0.5f, 1.1f);
                _flamePosition[i] = new Vector2(_random.Range(0f, 1f), _random.Range(-0.05f, 0.1f) * _size);
                _flameSize[i] = _random.Range(0.08f, 0.16f) * _size;
            }
        }
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
        var breath = 0.75f + 0.25f * MathF.Sin(_time * 2.2f);
        renderer.FillGlow(gateMiddle, _secondWidth * 0.9f, Light.Of(BloodRed, 0.45f * breath));
        renderer.FillGlow(gateMiddle + new Vector2(0f, -_size * 0.15f), _secondWidth * 0.6f, Light.Of(FireOrange, 0.3f * breath));

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

        // Tongues of flame licking up off the letters, yellow-hot when young, red as they die.
        var flameBase = top + _size * CapTop;
        for (var i = 0; i < FlameCount; i++)
        {
            var age = Math.Clamp(_flameAge[i], 0f, 1f);
            var position = new Vector2(gateLeft + _flamePosition[i].X * _secondWidth, flameBase + _flamePosition[i].Y);
            var radius = _flameSize[i] * (1f - age * 0.6f);
            var color = age < 0.4f ? Color.Lerp(HotYellow, FireOrange, age / 0.4f) : Color.Lerp(FireOrange, BloodRed, (age - 0.4f) / 0.6f);
            renderer.FillGlow(position, radius * 2f, Light.Of(color, 0.35f * (1f - age)));
        }
    }

    // How hot a letter burns now, 0–1: an uneven flicker, different for each letter.
    private float Heat(int letter)
    {
        var a = MathF.Sin(_time * 6.3f + letter * 1.9f);
        var b = MathF.Sin(_time * 11.7f + letter * 0.7f);
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
