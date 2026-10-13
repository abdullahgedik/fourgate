using Microsoft.Xna.Framework.Input;

namespace Fourgate.Core.Input;

/// <summary>Keyboard shortcuts for testing; only read when debug hotkeys are enabled in game.json.</summary>
public sealed class DebugHotkeys
{
    private const Keys SpawnOneKey = Keys.F1;
    private const Keys SpawnManyKey = Keys.F2;
    private const Keys GrantXpKey = Keys.F3;
    private const Keys DropCratesKey = Keys.F4;

    private KeyboardState _previous;

    /// <summary>Spawn a single enemy.</summary>
    public bool SpawnOnePressed { get; private set; }

    /// <summary>Spawn a group of enemies.</summary>
    public bool SpawnManyPressed { get; private set; }

    /// <summary>Give every player some XP.</summary>
    public bool GrantXpPressed { get; private set; }

    /// <summary>Drop one crate of each type next to the first player.</summary>
    public bool DropCratesPressed { get; private set; }

    public void Update()
    {
        var current = Keyboard.GetState();
        SpawnOnePressed = current.IsKeyDown(SpawnOneKey) && _previous.IsKeyUp(SpawnOneKey);
        SpawnManyPressed = current.IsKeyDown(SpawnManyKey) && _previous.IsKeyUp(SpawnManyKey);
        GrantXpPressed = current.IsKeyDown(GrantXpKey) && _previous.IsKeyUp(GrantXpKey);
        DropCratesPressed = current.IsKeyDown(DropCratesKey) && _previous.IsKeyUp(DropCratesKey);
        _previous = current;
    }
}
