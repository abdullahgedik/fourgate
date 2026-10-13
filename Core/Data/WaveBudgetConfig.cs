namespace Fourgate.Core.Data;

/// <summary>Spawn budget curve: <c>base + perWave·w + quadratic·w²</c>.</summary>
public sealed class WaveBudgetConfig
{
    public required float Base { get; init; }

    public required float PerWave { get; init; }

    public required float Quadratic { get; init; }
}
