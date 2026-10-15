using System.Collections.Generic;

namespace Fourgate.Core.Rendering.Models;

/// <summary>A blocky model ready to draw: its parts, parents before children.</summary>
public sealed class ModelDefinition
{
    public required string Id { get; init; }

    public required float Height { get; init; }

    public required float Stride { get; init; }

    public required float FullStrideSpeed { get; init; }

    public required float Bob { get; init; }

    public required IReadOnlyList<ModelPart> Parts { get; init; }
}
