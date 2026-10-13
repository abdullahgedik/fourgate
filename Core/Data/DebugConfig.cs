namespace Fourgate.Core.Data;

/// <summary>Optional testing switches in <c>game.json</c>; everything is off when the section is missing.</summary>
public sealed class DebugConfig
{
    /// <summary>Places a still and a moving dummy target in the arena for trying out weapons.</summary>
    public bool PracticeTargets { get; init; }
}
