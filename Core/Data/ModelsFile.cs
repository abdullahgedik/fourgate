using System.Collections.Generic;

namespace Fourgate.Core.Data;

/// <summary>Root of <c>models.json</c>: the blocky 3D models characters and guns are built from.</summary>
public sealed class ModelsFile
{
    public required List<ModelConfig> Models { get; init; }
}
