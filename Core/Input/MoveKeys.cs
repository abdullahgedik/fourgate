using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace Fourgate.Core.Input;

/// <summary>
/// WASD movement vector. Stand-in for trying out the camera until the player input
/// abstraction exists.
/// </summary>
public sealed class MoveKeys
{
    /// <summary>Unit-length direction, or zero when no key is held.</summary>
    public Vector2 Direction { get; private set; }

    public void Update()
    {
        var keyboard = Keyboard.GetState();
        var direction = Vector2.Zero;

        if (keyboard.IsKeyDown(Keys.W))
        {
            direction.Y -= 1f;
        }

        if (keyboard.IsKeyDown(Keys.S))
        {
            direction.Y += 1f;
        }

        if (keyboard.IsKeyDown(Keys.A))
        {
            direction.X -= 1f;
        }

        if (keyboard.IsKeyDown(Keys.D))
        {
            direction.X += 1f;
        }

        if (direction != Vector2.Zero)
        {
            direction.Normalize();
        }

        Direction = direction;
    }
}
