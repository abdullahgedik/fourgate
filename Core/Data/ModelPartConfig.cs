using System.Collections.Generic;

namespace Fourgate.Core.Data;

/// <summary>
/// One box of a model, hung from its parent's joint. Angles are in degrees: pitch turns about
/// the part's x axis (negative swings a hanging limb forward), yaw about y, roll about z.
/// </summary>
public sealed class ModelPartConfig
{
    public required string Id { get; init; }

    /// <summary>The part this one hangs from; null for the root.</summary>
    public string? Parent { get; init; }

    /// <summary>Where its joint sits in the parent's space (in model space for the root).</summary>
    public required List<float> Pivot { get; init; }

    /// <summary>Width, height and depth of its box; all zero for an invisible joint (a hand).</summary>
    public List<float>? Size { get; init; }

    /// <summary>Middle of its box, measured from its joint.</summary>
    public List<float>? Center { get; init; }

    /// <summary>RGB colour; ignored when <see cref="Tint"/> is set.</summary>
    public List<int>? Color { get; init; }

    /// <summary>Takes the entity's own colour (its body colour, hit flash and burn included).</summary>
    public bool Tint { get; init; }

    /// <summary>Darkens (below 1) or lightens the colour.</summary>
    public float Shade { get; init; } = 1f;

    /// <summary>Its pose at rest: pitch, yaw, roll.</summary>
    public List<float>? Rest { get; init; }

    /// <summary>Walk swing (pitch, either way) at full stride.</summary>
    public float Swing { get; init; }

    /// <summary>Where in the step cycle its swing peaks (180 for the opposite leg).</summary>
    public float SwingPhase { get; init; }

    /// <summary>
    /// A leg: its walk swing follows the way the body actually moves (forward, back or
    /// sideways), not just the way it faces.
    /// </summary>
    public bool Step { get; init; }

    /// <summary>Different rest poses and swings for each way of holding a gun.</summary>
    public ModelPartHoldsConfig? Holds { get; init; }

    /// <summary>Turns (yaw) toward the aim, carrying its children with it.</summary>
    public bool Aim { get; init; }

    /// <summary>Pitch added at full recoil (a gun kicking).</summary>
    public float Recoil { get; init; }

    /// <summary>Pitch added at the height of a lunge (an attack).</summary>
    public float Lunge { get; init; }

    /// <summary>Pitch added at the height of a flinch (being hit).</summary>
    public float Flinch { get; init; }

    /// <summary>Pitch added at the end of a wind-up (an arm drawn back before a throw or a blow).</summary>
    public float Windup { get; init; }

    /// <summary>"gun", "gunLeft" or "gunTwoHands": where a held gun model goes for each way of holding it.</summary>
    public string? Mount { get; init; }

    public void Validate(string file, string path)
    {
        DataLoadException.Require(!string.IsNullOrWhiteSpace(Id), file, $"{path}.id", "is required");
        RequireThree(Pivot, file, $"{path}.pivot");
        if (Size is not null)
        {
            RequireThree(Size, file, $"{path}.size");
            DataLoadException.Require(Size[0] >= 0 && Size[1] >= 0 && Size[2] >= 0, file, $"{path}.size", "must not be negative");
        }

        if (Center is not null)
        {
            RequireThree(Center, file, $"{path}.center");
        }

        if (Rest is not null)
        {
            RequireThree(Rest, file, $"{path}.rest");
        }

        if (Color is not null)
        {
            DataLoadException.Require(Color.Count == 3, file, $"{path}.color", "must have 3 values (RGB)");
        }

        var visible = Size is not null && Size[0] > 0 && Size[1] > 0 && Size[2] > 0;
        DataLoadException.Require(
            !visible || Tint || Color is not null, file, $"{path}.color", "a visible part needs a color or tint: true");
        DataLoadException.Require(Shade > 0, file, $"{path}.shade", "must be greater than 0");
        DataLoadException.Require(
            Mount is null or "gun" or "gunLeft" or "gunTwoHands" or "gunShoulder", file, $"{path}.mount",
            "must be \"gun\", \"gunLeft\", \"gunTwoHands\" or \"gunShoulder\"");
        Holds?.Validate(file, $"{path}.holds");
    }

    private static void RequireThree(List<float>? values, string file, string field)
    {
        DataLoadException.Require(values is { Count: 3 }, file, field, "must have 3 values (x, y, z)");
    }
}
