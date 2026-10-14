using Fourgate.Core.Rendering;

namespace Fourgate.Scenes;

/// <summary>One screen of the game (start screen, a run); the game updates and draws the current one.</summary>
public interface IScene
{
    void Update(float deltaSeconds);

    void Draw(IRenderer renderer);
}
