using Microsoft.Xna.Framework.Input;

namespace Fourgate.Core.Input;

/// <summary>Raw keyboard presses, for the Controls screen to learn a new key.</summary>
public interface IKeyCapture
{
    /// <summary>A key that went down this frame, or <see cref="Keys.None"/>.</summary>
    Keys KeyPressed { get; }

    /// <summary>The keys the keyboard player uses; changing them takes effect at once.</summary>
    KeyBindings Bindings { get; }
}
