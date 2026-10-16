using Microsoft.Xna.Framework.Input;

namespace Fourgate.Core.Input;

/// <summary>Keyboard shortcuts for testing; only read when debug hotkeys are enabled in game.json.</summary>
public sealed class DebugHotkeys
{
    private const Keys SpawnOneKey = Keys.F1;
    private const Keys SpawnManyKey = Keys.F2;
    private const Keys GrantXpKey = Keys.F3;
    private const Keys DropCratesKey = Keys.F4;
    private const Keys GrantWeaponsKey = Keys.F5;
    private const Keys OneShotKillsKey = Keys.F6;
    private const Keys LevelUpWeaponKey = Keys.F7;
    private const Keys NextEnemyKey = Keys.F8;

    private KeyboardState _previous;

    /// <summary>Spawn a single enemy.</summary>
    public bool SpawnOnePressed { get; private set; }

    /// <summary>Spawn a group of enemies.</summary>
    public bool SpawnManyPressed { get; private set; }

    /// <summary>Give every player some XP.</summary>
    public bool GrantXpPressed { get; private set; }

    /// <summary>Drop one crate of each type next to the first player.</summary>
    public bool DropCratesPressed { get; private set; }

    /// <summary>Unlock every weapon and give it to every player.</summary>
    public bool GrantWeaponsPressed { get; private set; }

    /// <summary>Turn one-shot kills on or off.</summary>
    public bool OneShotKillsPressed { get; private set; }

    /// <summary>Raise the level of every player's gun in hand and active equipment by one.</summary>
    public bool LevelUpWeaponPressed { get; private set; }

    /// <summary>Shift + F7: every weapon of every player to the top level.</summary>
    public bool MaxAllWeaponsPressed { get; private set; }

    /// <summary>Switch which enemy type F1/F2 spawn.</summary>
    public bool NextEnemyPressed { get; private set; }

    /// <summary>Shift + F8: show or hide the frame-time readout.</summary>
    public bool ToggleFrameTimePressed { get; private set; }

    public void Update()
    {
        var current = Keyboard.GetState();
        SpawnOnePressed = current.IsKeyDown(SpawnOneKey) && _previous.IsKeyUp(SpawnOneKey);
        SpawnManyPressed = current.IsKeyDown(SpawnManyKey) && _previous.IsKeyUp(SpawnManyKey);
        GrantXpPressed = current.IsKeyDown(GrantXpKey) && _previous.IsKeyUp(GrantXpKey);
        DropCratesPressed = current.IsKeyDown(DropCratesKey) && _previous.IsKeyUp(DropCratesKey);
        GrantWeaponsPressed = current.IsKeyDown(GrantWeaponsKey) && _previous.IsKeyUp(GrantWeaponsKey);
        OneShotKillsPressed = current.IsKeyDown(OneShotKillsKey) && _previous.IsKeyUp(OneShotKillsKey);
        var levelKey = current.IsKeyDown(LevelUpWeaponKey) && _previous.IsKeyUp(LevelUpWeaponKey);
        // Ctrl, not Shift, picks the second action: Shift is the dash key.
        var ctrl = current.IsKeyDown(Keys.LeftControl) || current.IsKeyDown(Keys.RightControl);
        LevelUpWeaponPressed = levelKey && !ctrl;
        MaxAllWeaponsPressed = levelKey && ctrl;
        var nextKey = current.IsKeyDown(NextEnemyKey) && _previous.IsKeyUp(NextEnemyKey);
        NextEnemyPressed = nextKey && !ctrl;
        ToggleFrameTimePressed = nextKey && ctrl;
        _previous = current;
    }
}
