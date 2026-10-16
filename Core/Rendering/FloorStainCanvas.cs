using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Fourgate.Core.Rendering;

/// <summary>
/// The image floor stains are painted into. New stains are painted once, before the frame is
/// drawn; the renderers then lay the whole image over the floor in one go.
/// </summary>
public sealed class FloorStainCanvas : IDisposable
{
    // Image pixels per world unit: half resolution is plenty for soft blots.
    private const float PixelsPerUnit = 0.5f;
    private const int BlotTextureSize = 64;

    private readonly GraphicsDevice _device;
    private readonly SpriteBatch _spriteBatch;
    private readonly RenderTarget2D _target;
    private readonly Texture2D _blot;
    private readonly Texture2D _pixel;
    private FloorStains? _painting;

    public FloorStainCanvas(GraphicsDevice device, float floorSize)
    {
        _device = device;
        _spriteBatch = new SpriteBatch(device);
        var pixels = Math.Max(1, (int)MathF.Ceiling(floorSize * PixelsPerUnit));
        _target = new RenderTarget2D(
            device, pixels, pixels, false, SurfaceFormat.Color, DepthFormat.None, 0, RenderTargetUsage.PreserveContents);
        _blot = CreateBlotTexture(device);
        _pixel = new Texture2D(device, 1, 1);
        _pixel.SetData(new[] { Color.White });
        Clear();
    }

    /// <summary>The floor image, covering the whole floor.</summary>
    public Texture2D Texture => _target;

    /// <summary>
    /// Paints the stains added since last time. A different stain set (a new run) starts from a
    /// clean floor. Must be called outside any other drawing (it switches the render target).
    /// </summary>
    public void Paint(FloorStains stains)
    {
        var fresh = !ReferenceEquals(stains, _painting);
        if (!fresh && stains.PendingCount == 0)
        {
            return;
        }

        _device.SetRenderTarget(_target);
        if (fresh)
        {
            _device.Clear(Color.Transparent);
            _painting = stains;
        }

        var scale = _target.Width / stains.Size;
        var origin = new Vector2(BlotTextureSize / 2f);
        _spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp);
        for (var i = 0; i < stains.PendingCount; i++)
        {
            var stain = stains.GetPending(i);
            var at = (stain.Position - stains.TopLeft) * scale;
            if (stain.Shape == FloorStains.StainShape.Rect)
            {
                _spriteBatch.Draw(
                    _pixel, at, null, stain.Color, stain.Rotation, new Vector2(0.5f), stain.Size * scale, SpriteEffects.None, 0f);
                continue;
            }

            var size = stain.Radius * 2f * scale / BlotTextureSize;
            _spriteBatch.Draw(_blot, at, null, stain.Color, 0f, origin, size, SpriteEffects.None, 0f);
        }

        _spriteBatch.End();
        _device.SetRenderTarget(null);
        stains.ClearPending();
    }

    public void Dispose()
    {
        _target.Dispose();
        _blot.Dispose();
        _pixel.Dispose();
        _spriteBatch.Dispose();
    }

    private void Clear()
    {
        _device.SetRenderTarget(_target);
        _device.Clear(Color.Transparent);
        _device.SetRenderTarget(null);
    }

    // A white disc with a soft edge (premultiplied alpha), tinted per stain when drawn.
    private static Texture2D CreateBlotTexture(GraphicsDevice device)
    {
        var texture = new Texture2D(device, BlotTextureSize, BlotTextureSize);
        var pixels = new Color[BlotTextureSize * BlotTextureSize];
        var center = (BlotTextureSize - 1) / 2f;
        for (var y = 0; y < BlotTextureSize; y++)
        {
            for (var x = 0; x < BlotTextureSize; x++)
            {
                var distance = Vector2.Distance(new Vector2(x, y), new Vector2(center)) / center;
                var alpha = Math.Clamp((1f - distance) / 0.3f, 0f, 1f);
                pixels[y * BlotTextureSize + x] = Color.White * alpha;
            }
        }

        texture.SetData(pixels);
        return texture;
    }
}
