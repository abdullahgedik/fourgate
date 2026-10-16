using System;
using Fourgate.Core.Rendering;
using Fourgate.Core.Rng;
using Microsoft.Xna.Framework;

namespace Fourgate.UI;

/// <summary>
/// The menus' background: near-black at the top warming to a deep ember red below, a glowing
/// fissure across the lower screen with molten lava welling up beneath it (white-hot at the
/// crack, cooling to deep red at the bottom edge, slow waves of heat rolling through it), and
/// embers drifting up.
/// </summary>
public sealed class HellBackdrop
{
    private const int Bands = 24;
    private const int EmberCount = 90;
    private const int FissurePoints = 28;
    // The fissure wanders across the lower screen, above the menus' hint lines.
    private const float FissureHeight = 0.86f;

    private static readonly Color TopColor = new(10, 6, 7);
    private static readonly Color BottomColor = new(58, 12, 8);
    private static readonly Color HeatColor = new(255, 80, 20);
    private static readonly Color FissureColor = new(255, 150, 50);
    private static readonly Color FissureCore = new(255, 230, 160);
    private static readonly Color EmberHot = new(255, 190, 90);
    private static readonly Color EmberCool = new(230, 60, 20);
    private static readonly Color CornerShade = new Color(0, 0, 0) * 0.8f;
    private static readonly Color HintTextColor = new(240, 226, 210);
    private static readonly Color HintShadowColor = new Color(0, 0, 0) * 0.85f;

    // The lava below the crack: colours by depth under it, cooling from white-hot to deep red.
    private static readonly float[] LavaDepths = { 0f, 8f, 22f, 45f, 80f, 130f, 200f };
    private static readonly Color[] LavaColors =
    {
        new(255, 225, 150), new(255, 175, 70), new(250, 125, 35), new(225, 80, 22), new(185, 50, 15),
        new(140, 30, 10), new(105, 20, 8),
    };

    // Width of each strip the lava is drawn in.
    private const float LavaColumn = 6f;

    private readonly int _width;
    private readonly int _height;
    private readonly GameRandom _random = new(31);
    private readonly Vector2[] _fissure = new Vector2[FissurePoints];
    private readonly Vector2[] _emberPosition = new Vector2[EmberCount];
    private readonly Vector2[] _emberVelocity = new Vector2[EmberCount];
    private readonly float[] _emberLife = new float[EmberCount];
    private readonly float[] _emberSize = new float[EmberCount];
    private float _time;

    public HellBackdrop(int width, int height)
    {
        _width = width;
        _height = height;

        // A jagged crack across the bottom, wandering up and down.
        var y = height * FissureHeight;
        for (var i = 0; i < FissurePoints; i++)
        {
            y = Math.Clamp(y + _random.Range(-26f, 26f), height * 0.8f, height * 0.92f);
            _fissure[i] = new Vector2(width * i / (FissurePoints - 1f), y);
        }

        for (var i = 0; i < EmberCount; i++)
        {
            Respawn(i);
            _emberPosition[i].Y = _random.Range(0f, height);
        }
    }

    public void Update(float deltaSeconds)
    {
        _time += deltaSeconds;
        for (var i = 0; i < EmberCount; i++)
        {
            _emberLife[i] -= deltaSeconds;
            _emberPosition[i] += _emberVelocity[i] * deltaSeconds;
            _emberPosition[i].X += MathF.Sin(_time * 1.7f + i) * 12f * deltaSeconds;
            if (_emberLife[i] <= 0f || _emberPosition[i].Y < -20f)
            {
                Respawn(i);
            }
        }
    }

    public void Draw(IRenderer renderer)
    {
        var bandHeight = _height / (float)Bands;
        for (var i = 0; i < Bands; i++)
        {
            var t = i / (Bands - 1f);
            renderer.FillRect(
                new Vector2(0f, i * bandHeight), new Vector2(_width, bandHeight + 1f), Color.Lerp(TopColor, BottomColor, t * t));
        }

        // Heat rising from below, breathing slowly.
        var breath = 0.8f + 0.2f * MathF.Sin(_time * 0.9f);
        for (var i = 0; i < 4; i++)
        {
            var x = _width * (0.125f + 0.25f * i);
            renderer.FillGlow(new Vector2(x, _height * 1.02f), _width * 0.33f, Light.Of(HeatColor, 0.22f * breath));
        }

        DrawLava(renderer);

        for (var i = 1; i < FissurePoints; i++)
        {
            var from = _fissure[i - 1];
            var to = _fissure[i];
            var pulse = 0.7f + 0.3f * MathF.Sin(_time * 2.1f + i * 0.8f);
            renderer.FillGlow((from + to) / 2f, 70f, Light.Of(FissureColor, 0.32f * pulse));
            renderer.DrawLine(from, to, 5f, Light.Of(FissureColor, 0.8f * pulse));
            renderer.DrawLine(from, to, 2f, Light.Of(FissureCore, 0.9f));
        }

        for (var i = 0; i < EmberCount; i++)
        {
            var flicker = 0.6f + 0.4f * MathF.Sin(_time * 9f + i * 2.3f);
            var color = Color.Lerp(EmberCool, EmberHot, flicker);
            renderer.FillGlow(_emberPosition[i], _emberSize[i] * 3f, Light.Of(color, 0.35f * flicker));
            renderer.FillGlow(_emberPosition[i], _emberSize[i], Light.Of(EmberHot, 0.9f * flicker));
        }

        // Darker corners pull the eye to the middle.
        var reach = _width * 0.45f;
        renderer.FillGlow(Vector2.Zero, reach, CornerShade);
        renderer.FillGlow(new Vector2(_width, 0f), reach, CornerShade);
    }

    /// <summary>A menu's hint line, kept readable over the lava with a dark shadow.</summary>
    public static void DrawHint(IRenderer renderer, string text, Vector2 position, float size, Color? color = null)
    {
        renderer.DrawText(text, position + new Vector2(2f, 2f), size, HintShadowColor, TextAlign.Center);
        renderer.DrawText(text, position, size, color ?? HintTextColor, TextAlign.Center);
    }

    // Narrow strips from the crack down to the bottom edge, each in bands that cool with depth;
    // the brightness rolls slowly sideways like a flow.
    private void DrawLava(IRenderer renderer)
    {
        var step = _width / (FissurePoints - 1f);
        for (var x = 0f; x < _width; x += LavaColumn)
        {
            var at = Math.Min(x / step, FissurePoints - 1.001f);
            var index = (int)at;
            var crack = MathHelper.Lerp(_fissure[index].Y, _fissure[index + 1].Y, at - index);
            var flow = 0.82f + 0.12f * MathF.Sin(_time * 1.3f + x * 0.012f) + 0.06f * MathF.Sin(_time * 2.7f - x * 0.031f);
            for (var band = 0; band < LavaDepths.Length; band++)
            {
                var top = crack + LavaDepths[band];
                if (top >= _height)
                {
                    break;
                }

                var bottom = band + 1 < LavaDepths.Length ? Math.Min(crack + LavaDepths[band + 1], _height) : _height;
                var color = new Color(Vector3.Clamp(LavaColors[band].ToVector3() * flow, Vector3.Zero, Vector3.One));
                renderer.FillRect(new Vector2(x, top), new Vector2(LavaColumn + 0.5f, bottom - top), color);
            }
        }
    }

    private void Respawn(int i)
    {
        _emberPosition[i] = new Vector2(_random.Range(0f, _width), _height * _random.Range(0.85f, 1.02f));
        _emberVelocity[i] = new Vector2(_random.Range(-15f, 15f), -_random.Range(40f, 120f));
        _emberLife[i] = _random.Range(4f, 11f);
        _emberSize[i] = _random.Range(2f, 4.5f);
    }
}
