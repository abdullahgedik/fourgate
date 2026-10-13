using Fourgate.Core.Rendering;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace Fourgate.Core.Input;

/// <summary>Mouse position translated from window pixels to virtual coordinates.</summary>
public sealed class VirtualMouse
{
    /// <summary>Position in virtual coordinates; can lie outside the virtual screen over the black bars.</summary>
    public Vector2 Position { get; private set; }

    public void Update(VirtualViewport viewport)
    {
        var state = Mouse.GetState();
        Position = viewport.WindowToVirtual(new Vector2(state.X, state.Y));
    }
}
