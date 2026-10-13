using Microsoft.Xna.Framework;

namespace Fourgate;

/// <summary>Game entry point: owns the main loop and (later) scene switching.</summary>
public sealed class FourgateGame : Game
{
    private readonly GraphicsDeviceManager _graphics;

    public FourgateGame()
    {
        _graphics = new GraphicsDeviceManager(this);
        Content.RootDirectory = "Content";
        IsMouseVisible = true;
        Window.Title = "Fourgate";
    }

    protected override void Draw(GameTime gameTime)
    {
        GraphicsDevice.Clear(Color.CornflowerBlue);

        base.Draw(gameTime);
    }
}
