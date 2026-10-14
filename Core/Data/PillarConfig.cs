namespace Fourgate.Core.Data;

/// <summary>A solid block inside the arena. The center is measured from the arena's top-left corner.</summary>
public sealed class PillarConfig
{
    public required float CenterX { get; init; }

    public required float CenterY { get; init; }

    public required float Width { get; init; }

    public required float Height { get; init; }
}
