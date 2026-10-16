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

    /// <summary>Stretch of the whole model (width, height, depth).</summary>
    public required Microsoft.Xna.Framework.Vector3 Scale { get; init; }

    /// <summary>A gun's muzzle in its own space; null for anything that is not a gun.</summary>
    public Microsoft.Xna.Framework.Vector3? Muzzle { get; init; }

    /// <summary>A launcher's back end; null for none.</summary>
    public Microsoft.Xna.Framework.Vector3? Exhaust { get; init; }

    public required IReadOnlyList<ModelPart> Parts { get; init; }
}
