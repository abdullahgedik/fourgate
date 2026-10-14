namespace Fourgate.Core.Data;

/// <summary>Optional testing switches in <c>game.json</c>; everything is off when the section is missing.</summary>
public sealed class DebugConfig
{
    /// <summary>Enables the debug keys (spawning enemies and so on).</summary>
    public bool Hotkeys { get; init; }

    /// <summary>Places a still and a moving dummy target in the arena for trying out weapons.</summary>
    public bool PracticeTargets { get; init; }

    /// <summary>Every player bullet kills what it hits.</summary>
    public bool OneShotKills { get; init; }

    /// <summary>Turns the wave system off, leaving an empty arena for testing.</summary>
    public bool DisableWaves { get; init; }

    /// <summary>In co-op, player 2 can also be played from the keyboard (IJKL, U, O, P...) for testing without a gamepad.</summary>
    public bool KeyboardPlayerTwo { get; init; }

    /// <summary>Wave number the run starts at.</summary>
    public int StartWave { get; init; } = 1;

    /// <summary>Fixed random seed so a run can be reproduced; a new seed is picked each run when missing.</summary>
    public int? Seed { get; init; }
}
