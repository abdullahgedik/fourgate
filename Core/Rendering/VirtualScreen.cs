using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Fourgate.Core.Rendering;

/// <summary>The fixed-size render target the game draws to, presented letterboxed to the window.</summary>
public sealed class VirtualScreen : IDisposable
{
    private readonly GraphicsDevice _device;
    private readonly RenderTarget2D _target;

    public VirtualScreen(GraphicsDevice device, int width, int height)
    {
        _device = device;
        _target = new RenderTarget2D(
            device, width, height, false, SurfaceFormat.Color, DepthFormat.Depth24Stencil8, 0, RenderTargetUsage.DiscardContents);
    }

    /// <summary>Current placement inside the window; follows window resizes.</summary>
    public VirtualViewport Viewport
    {
        get
        {
            var parameters = _device.PresentationParameters;
            return VirtualViewport.Fit(
                _target.Width, _target.Height, parameters.BackBufferWidth, parameters.BackBufferHeight);
        }
    }

    /// <summary>Redirects drawing to the virtual screen.</summary>
    public void Begin(Color clearColor)
    {
        _device.SetRenderTarget(_target);
        _device.Clear(clearColor);
    }

    /// <summary>Draws the virtual screen to the window with black bars around it.</summary>
    public void Present(SpriteBatch spriteBatch)
    {
        _device.SetRenderTarget(null);
        _device.Clear(Color.Black);

        spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.Opaque, SamplerState.LinearClamp);
        spriteBatch.Draw(_target, Viewport.Destination, Color.White);
        spriteBatch.End();
    }

    public void Dispose()
    {
        _target.Dispose();
    }
}
