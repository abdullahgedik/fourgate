using System.Collections.Generic;

namespace Fourgate.Core.Data;

/// <summary>
/// One blocky model: boxes joined in a tree. Model space: x to the right, y up, z forward;
/// units are world units, standing on the floor at the origin.
/// </summary>
public sealed class ModelConfig
{
    public required string Id { get; init; }

    /// <summary>Height of the standing model (for its shadow).</summary>
    public float Height { get; init; }

    /// <summary>Distance walked per full step cycle (both legs).</summary>
    public float Stride { get; init; } = 30f;

    /// <summary>Speed at which the walk swings at full size; slower walks swing less.</summary>
    public float FullStrideSpeed { get; init; } = 120f;

    /// <summary>How far the body rises and falls with each step.</summary>
    public float Bob { get; init; }

    /// <summary>
    /// Stretches the whole model: width, height, depth (to fill its hitbox; chunkier, in the
    /// Boxhead way). Leaving it out keeps the model as built.
    /// </summary>
    public List<float>? Scale { get; init; }

    /// <summary>A gun's muzzle: where its flash shows, in the gun's own space (the end of the barrel).</summary>
    public List<float>? Muzzle { get; init; }

    /// <summary>A launcher's back end: where it also flares when it fires.</summary>
    public List<float>? Exhaust { get; init; }

    /// <summary>The parts, parents before their children.</summary>
    public required List<ModelPartConfig> Parts { get; init; }

    public void Validate(string file, string path)
    {
        DataLoadException.Require(!string.IsNullOrWhiteSpace(Id), file, $"{path}.id", "is required");
        DataLoadException.Require(Height >= 0, file, $"{path}.height", "must not be negative");
        DataLoadException.Require(Stride > 0, file, $"{path}.stride", "must be greater than 0");
        DataLoadException.Require(FullStrideSpeed > 0, file, $"{path}.fullStrideSpeed", "must be greater than 0");
        DataLoadException.Require(Bob >= 0, file, $"{path}.bob", "must not be negative");
        DataLoadException.Require(
            Scale is null || (Scale.Count == 3 && Scale.TrueForAll(v => v > 0f)), file, $"{path}.scale",
            "must have 3 values above 0 (width, height, depth)");
        DataLoadException.Require(Muzzle is null || Muzzle.Count == 3, file, $"{path}.muzzle", "must have 3 values (x, y, z)");
        DataLoadException.Require(Exhaust is null || Exhaust.Count == 3, file, $"{path}.exhaust", "must have 3 values (x, y, z)");
        DataLoadException.Require(Parts is { Count: > 0 }, file, $"{path}.parts", "must contain at least one part");

        var seen = new HashSet<string>();
        for (var i = 0; i < Parts!.Count; i++)
        {
            var partPath = $"{path}.parts[{i}]";
            var part = Parts[i];
            DataLoadException.Require(part is not null, file, partPath, "must not be null");
            part!.Validate(file, partPath);
            DataLoadException.Require(
                part.Parent is null || seen.Contains(part.Parent), file, $"{partPath}.parent",
                $"'{part.Parent}' must be a part listed before this one");
            DataLoadException.Require(seen.Add(part.Id), file, $"{partPath}.id", $"'{part.Id}' is used twice");
        }
    }
}
