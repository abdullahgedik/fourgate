using System;
using Fourgate.Core.Rendering;
using Fourgate.Core.Rng;
using Microsoft.Xna.Framework;

namespace Fourgate.UI;

/// <summary>
/// The menus' background: near-black at the top warming to a deep ember red below, a glowing
/// fissure of lava across the bottom that breathes slowly, and embers drifting up through it.
/// </summary>
public sealed class HellBackdrop
{
    private const int Bands = 24;
    private const int EmberCount = 90;
    private const int FissurePoints = 28;
    private const float FissureHeight = 0.86f;

    private static readonly Color TopColor = new(10, 6, 7);
    private static readonly Color BottomColor = new(58, 12, 8);
    private static readonly Color HeatColor = new(255, 80, 20);
    private static readonly Color FissureColor = new(255, 150, 50);
    private static readonly Color FissureCore = new(255, 230, 160);
    private static readonly Color EmberHot = new(255, 190, 90);
    private static readonly Color EmberCool = new(230, 60, 20);
    private static readonly Color CornerShade = new Color(0, 0, 0) * 0.8f;

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
            y = Math.Clamp(y + _random.Range(-26f, 26f), height * 0.8f, height * 0.94f);
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

    private void Respawn(int i)
    {
        _emberPosition[i] = new Vector2(_random.Range(0f, _width), _height * _random.Range(0.85f, 1.02f));
        _emberVelocity[i] = new Vector2(_random.Range(-15f, 15f), -_random.Range(40f, 120f));
        _emberLife[i] = _random.Range(4f, 11f);
        _emberSize[i] = _random.Range(2f, 4.5f);
    }
}
