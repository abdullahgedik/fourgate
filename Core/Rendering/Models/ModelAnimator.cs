using System;
using Microsoft.Xna.Framework;

namespace Fourgate.Core.Rendering.Models;

/// <summary>
/// Builds a model's pose from what the entity reports: legs and arms swing with the walk, the
/// body bobs, aiming parts turn to the aim, and recoil, lunge and flinch tilt the parts that
/// react to them. Pure maths, so it is easy to test and allocates nothing.
/// </summary>
public static class ModelAnimator
{
    // A fallen body lies about this high, so it does not sink into the floor.
    private const float FallenLift = 4f;

    /// <summary>
    /// The model's placement in the world: standing at the pose's position, facing its way.
    /// World space is (x, height, y). Model x is the body's right, y up, z forward.
    /// </summary>
    public static Matrix Root(in ModelPose pose)
    {
        var forward = new Vector3(MathF.Cos(pose.Facing), 0f, MathF.Sin(pose.Facing));
        var right = new Vector3(-MathF.Sin(pose.Facing), 0f, MathF.Cos(pose.Facing));
        var basis = new Matrix(
            right.X, right.Y, right.Z, 0f,
            0f, 1f, 0f, 0f,
            forward.X, forward.Y, forward.Z, 0f,
            pose.Position.X, pose.Elevation, pose.Position.Y, 1f);

        var fall = Math.Clamp(pose.Fall, 0f, 1f);
        var scale = 1f - Math.Clamp(pose.Shrink, 0f, 1f);
        return Matrix.CreateScale(scale)
            * Matrix.CreateRotationX(pose.Tumble)
            * Matrix.CreateRotationX(-MathHelper.PiOver2 * fall)
            * Matrix.CreateTranslation(0f, FallenLift * fall, 0f)
            * basis;
    }

    /// <summary>Fills <paramref name="worlds"/> with each part's joint-to-world placement.</summary>
    public static void Pose(ModelDefinition model, in ModelPose pose, Matrix root, Matrix[] worlds)
    {
        var stride = Math.Clamp(pose.Speed / model.FullStrideSpeed, 0f, 1f);
        var cycle = pose.WalkDistance / model.Stride * MathHelper.TwoPi;
        var aimTurn = MathHelper.WrapAngle(pose.Aim - pose.Facing);
        var recoil = Math.Clamp(pose.Recoil, 0f, 1f);
        var lunge = Math.Clamp(pose.Lunge, 0f, 1f);
        var flinch = Math.Clamp(pose.Flinch, 0f, 1f);
        var windup = Math.Clamp(pose.Windup, 0f, 1f);
        var hold = (int)pose.Hold;
        if (model.Scale != Vector3.One)
        {
            root = Matrix.CreateScale(model.Scale) * root;
        }

        // Legs step along the way the body moves, seen from the body: forward and back swing
        // them front to back (backward reverses the stride), sideways swings them out to the side.
        var moving = MathHelper.WrapAngle((pose.MoveHeading ?? pose.Facing) - pose.Facing);
        var stepForward = MathF.Cos(moving);
        var stepSideways = MathF.Sin(moving);

        var parts = model.Parts;
        for (var i = 0; i < parts.Count; i++)
        {
            var part = parts[i];
            var rest = part.RestByHold[hold];
            var swing = part.SwingByHold[hold] * stride * MathF.Sin(cycle + part.SwingPhaseByHold[hold]);
            var roll = rest.Z;
            var pitch = rest.X + part.Recoil * recoil + part.Lunge * lunge + part.Flinch * flinch + part.Windup * windup;
            if (part.Step)
            {
                pitch += swing * stepForward;
                roll -= swing * stepSideways;
            }
            else
            {
                pitch += swing;
            }

            var yaw = rest.Y + (part.Aim ? aimTurn : 0f);
            var pivot = part.Pivot;
            if (part.Parent < 0)
            {
                // The root rises and falls twice per cycle, once per step.
                pivot.Y += model.Bob * stride * MathF.Abs(MathF.Sin(cycle));
            }

            var local = Matrix.CreateFromYawPitchRoll(yaw, pitch, roll) * Matrix.CreateTranslation(pivot);
            worlds[i] = part.Parent < 0 ? local * root : local * worlds[part.Parent];
        }
    }
}
