using Microsoft.Xna.Framework;

namespace Fourgate.Core.Rendering.Models;

/// <summary>One box of a model, ready to pose (angles in radians).</summary>
public sealed class ModelPart
{
    public required string Id { get; init; }

    /// <summary>Index of the parent part; -1 for the root.</summary>
    public required int Parent { get; init; }

    public required Vector3 Pivot { get; init; }

    /// <summary>Half the box's width, height and depth; zero for an invisible joint.</summary>
    public required Vector3 HalfSize { get; init; }

    public required Vector3 Center { get; init; }

    public required Color Color { get; init; }

    public required bool Tint { get; init; }

    public required float Shade { get; init; }

    /// <summary>Rest pose: pitch (x), yaw (y), roll (z).</summary>
    public required Vector3 Rest { get; init; }

    public required float Swing { get; init; }

    public required float SwingPhase { get; init; }

    public required bool Aim { get; init; }

    public required float Recoil { get; init; }

    public required float Lunge { get; init; }

    public required float Flinch { get; init; }

    public required float Windup { get; init; }

    public required ModelMount Mount { get; init; }

    /// <summary>Its walk swing follows the way the body moves (a leg).</summary>
    public required bool Step { get; init; }

    /// <summary>Rest pose for each <see cref="ModelHold"/> (by its number).</summary>
    public required Vector3[] RestByHold { get; init; }

    /// <summary>Walk swing for each <see cref="ModelHold"/>.</summary>
    public required float[] SwingByHold { get; init; }

    /// <summary>Swing phase for each <see cref="ModelHold"/>.</summary>
    public required float[] SwingPhaseByHold { get; init; }

    public bool IsVisible => HalfSize.X > 0f && HalfSize.Y > 0f && HalfSize.Z > 0f;
}
