using System;
using FontStashSharp;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Fourgate.Core.Rendering;

/// <summary>
/// Geometric-shape renderer built on generated textures and a runtime-loaded TTF font
/// (no content pipeline assets).
/// </summary>
public sealed class ShapeRenderer : IRenderer, IDisposable
{
    private const int CircleTextureSize = 128;

    private static readonly RasterizerState ScissorState = new() { ScissorTestEnable = true, CullMode = CullMode.None };

    private readonly GraphicsDevice _device;
    private readonly SpriteBatch _spriteBatch;
    private readonly Texture2D _pixel;
    private readonly Texture2D _circle;
    private readonly Texture2D _triangle;
    private readonly Texture2D _hexagon;
    private readonly FontSystem _fonts = new();

    /// <param name="device">Graphics device to create textures on.</param>
    /// <param name="spriteBatch">Shared sprite batch.</param>
    /// <param name="fontData">Contents of the TTF file used for all text.</param>
    public ShapeRenderer(GraphicsDevice device, SpriteBatch spriteBatch, byte[] fontData)
    {
        _fonts.AddFont(fontData);

        _device = device;
        _spriteBatch = spriteBatch;

        _pixel = new Texture2D(device, 1, 1);
        _pixel.SetData(new[] { Color.White });

        _circle = CreateCircleTexture(device, CircleTextureSize);
        _triangle = CreateTriangleTexture(device, CircleTextureSize);
        _hexagon = CreateHexagonTexture(device, CircleTextureSize);
    }

    public void BeginScreen()
    {
        _spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp);
    }

    public void BeginWorld(Camera2D camera)
    {
        _device.ScissorRectangle = camera.ViewRect;
        _spriteBatch.Begin(
            SpriteSortMode.Deferred,
            BlendState.AlphaBlend,
            SamplerState.LinearClamp,
            DepthStencilState.None,
            ScissorState,
            null,
            camera.GetTransform());
    }

    public void End()
    {
        _spriteBatch.End();
    }

    public void DrawVisual(in VisualDescriptor visual, Vector2 position, float rotation)
    {
        switch (visual.Shape)
        {
            case ShapeKind.Circle:
                FillCircle(position, visual.Radius, visual.Color);
                break;

            case ShapeKind.Square:
                FillRect(position - new Vector2(visual.Radius), new Vector2(visual.Radius * 2f), visual.Color);
                break;

            case ShapeKind.Diamond:
                // A square of side r·√2 turned 45°, so its corners reach the radius.
                var side = visual.Radius * MathF.Sqrt(2f);
                _spriteBatch.Draw(
                    _pixel, position, null, visual.Color, MathF.PI / 4f, new Vector2(0.5f), new Vector2(side),
                    SpriteEffects.None, 0f);
                break;

            case ShapeKind.Triangle:
                DrawShapeTexture(_triangle, visual, position, rotation);
                break;

            case ShapeKind.Hexagon:
                DrawShapeTexture(_hexagon, visual, position, rotation);
                break;
        }
    }

    private void DrawShapeTexture(Texture2D texture, in VisualDescriptor visual, Vector2 position, float rotation)
    {
        var scale = visual.Radius * 2f / CircleTextureSize;
        var origin = new Vector2(CircleTextureSize / 2f);
        _spriteBatch.Draw(texture, position, null, visual.Color, rotation, origin, scale, SpriteEffects.None, 0f);
    }

    public void FillRect(Vector2 topLeft, Vector2 size, Color color)
    {
        _spriteBatch.Draw(_pixel, topLeft, null, color, 0f, Vector2.Zero, size, SpriteEffects.None, 0f);
    }

    public void FillCircle(Vector2 center, float radius, Color color)
    {
        var scale = radius * 2f / CircleTextureSize;
        var origin = new Vector2(CircleTextureSize / 2f);
        _spriteBatch.Draw(_circle, center, null, color, 0f, origin, scale, SpriteEffects.None, 0f);
    }

    public void DrawLine(Vector2 from, Vector2 to, float thickness, Color color)
    {
        var delta = to - from;
        var rotation = MathF.Atan2(delta.Y, delta.X);
        var scale = new Vector2(delta.Length(), thickness);
        _spriteBatch.Draw(_pixel, from, null, color, rotation, new Vector2(0f, 0.5f), scale, SpriteEffects.None, 0f);
    }

    public void DrawText(string text, Vector2 position, float size, Color color, TextAlign align = TextAlign.Left)
    {
        var font = _fonts.GetFont(size);
        if (align != TextAlign.Left)
        {
            var width = font.MeasureString(text).X;
            position.X -= align == TextAlign.Center ? width / 2f : width;
        }

        _spriteBatch.DrawString(font, text, position, color);
    }

    public void Dispose()
    {
        _fonts.Dispose();
        _pixel.Dispose();
        _circle.Dispose();
        _triangle.Dispose();
        _hexagon.Dispose();
    }

    // A triangle filling the circle of the texture, its tip pointing to +X.
    private static Texture2D CreateTriangleTexture(GraphicsDevice device, int size)
    {
        var radius = size / 2f;
        return CreatePolygonTexture(device, size, new[]
        {
            new Vector2(radius, 0f),
            new Vector2(-radius * 0.75f, radius * 0.87f),
            new Vector2(-radius * 0.75f, -radius * 0.87f),
        });
    }

    // A hexagon touching the circle of the texture, with a corner pointing to +X.
    private static Texture2D CreateHexagonTexture(GraphicsDevice device, int size)
    {
        var radius = size / 2f;
        var corners = new Vector2[6];
        for (var i = 0; i < corners.Length; i++)
        {
            var angle = MathF.PI / 3f * i;
            corners[i] = new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * radius;
        }

        return CreatePolygonTexture(device, size, corners);
    }

    // A convex polygon (corners around the texture's center, in order). Each pixel is sampled
    // 4x4 times for a soft edge; premultiplied alpha like the circle.
    private static Texture2D CreatePolygonTexture(GraphicsDevice device, int size, Vector2[] corners)
    {
        const int Samples = 4;
        var data = new Color[size * size];
        var radius = size / 2f;

        for (var y = 0; y < size; y++)
        {
            for (var x = 0; x < size; x++)
            {
                var inside = 0;
                for (var sy = 0; sy < Samples; sy++)
                {
                    for (var sx = 0; sx < Samples; sx++)
                    {
                        var point = new Vector2(
                            x + (sx + 0.5f) / Samples - radius,
                            y + (sy + 0.5f) / Samples - radius);
                        if (IsInsideConvex(point, corners))
                        {
                            inside++;
                        }
                    }
                }

                data[y * size + x] = Color.White * ((float)inside / (Samples * Samples));
            }
        }

        var texture = new Texture2D(device, size, size);
        texture.SetData(data);
        return texture;
    }

    // Inside when the point is on the same side of every edge.
    private static bool IsInsideConvex(Vector2 point, Vector2[] corners)
    {
        var positive = false;
        var negative = false;
        for (var i = 0; i < corners.Length; i++)
        {
            var a = corners[i];
            var b = corners[(i + 1) % corners.Length];
            var side = Cross(b - a, point - a);
            positive |= side > 0f;
            negative |= side < 0f;
        }

        return !(positive && negative);
    }

    private static float Cross(Vector2 a, Vector2 b)
    {
        return a.X * b.Y - a.Y * b.X;
    }

    private static Texture2D CreateCircleTexture(GraphicsDevice device, int size)
    {
        var data = new Color[size * size];
        var radius = size / 2f;

        for (var y = 0; y < size; y++)
        {
            for (var x = 0; x < size; x++)
            {
                var dx = x + 0.5f - radius;
                var dy = y + 0.5f - radius;
                var distance = MathF.Sqrt(dx * dx + dy * dy);

                // One-pixel soft edge; premultiplied alpha to match BlendState.AlphaBlend.
                var alpha = Math.Clamp(radius - distance, 0f, 1f);
                data[y * size + x] = Color.White * alpha;
            }
        }

        var texture = new Texture2D(device, size, size);
        texture.SetData(data);
        return texture;
    }
}
