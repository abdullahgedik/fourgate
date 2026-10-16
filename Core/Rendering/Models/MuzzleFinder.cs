using Microsoft.Xna.Framework;

namespace Fourgate.Core.Rendering.Models;

/// <summary>
/// Finds where a held gun's barrel ends, measured from the body that holds it: posed standing,
/// facing along +X, so shots can leave from the real muzzle instead of the middle of the body.
/// </summary>
public static class MuzzleFinder
{
    /// <param name="library">The models.</param>
    /// <param name="bodyModel">The model holding the gun (the survivor).</param>
    /// <param name="gunModel">The gun model.</param>
    /// <param name="hold">How it is held.</param>
    /// <param name="forward">How far ahead of the body's centre the muzzle is.</param>
    /// <param name="right">How far to the body's right (negative: left).</param>
    /// <param name="dualHalfSpread">With two guns, half the distance between their muzzles; 0 otherwise.</param>
    /// <returns>False when a model or the gun's muzzle point is missing.</returns>
    public static bool TryFind(
        ModelLibrary library, string bodyModel, string gunModel, ModelHold hold, out float forward, out float right,
        out float dualHalfSpread)
    {
        forward = 0f;
        right = 0f;
        dualHalfSpread = 0f;
        if (!library.TryGet(bodyModel, out var body) || !library.TryGet(gunModel, out var gun) || gun.Muzzle is not { } muzzle)
        {
            return false;
        }

        var pose = new ModelPose(bodyModel, Vector2.Zero, 0f, 0f, 0f, 0f, Color.White, Hold: hold);
        var worlds = new Matrix[body.Parts.Count];
        ModelAnimator.Pose(body, pose, ModelAnimator.Root(pose), worlds);

        var found = 0;
        var sum = Vector2.Zero;
        var first = Vector2.Zero;
        for (var i = 0; i < body.Parts.Count; i++)
        {
            if (!Holds(body.Parts[i].Mount, hold))
            {
                continue;
            }

            // Facing +X: the world's x is ahead of the body, its z is to the body's right.
            var point = Vector3.Transform(muzzle, worlds[i]);
            var flat = new Vector2(point.X, point.Z);
            if (found == 0)
            {
                first = flat;
            }
            else
            {
                dualHalfSpread = Vector2.Distance(first, flat) / 2f;
            }

            sum += flat;
            found++;
        }

        if (found == 0)
        {
            return false;
        }

        forward = sum.X / found;
        right = sum.Y / found;
        return true;
    }

    private static bool Holds(ModelMount mount, ModelHold hold)
    {
        return hold switch
        {
            ModelHold.TwoHands => mount == ModelMount.GunTwoHands,
            ModelHold.Shoulder => mount == ModelMount.GunShoulder,
            ModelHold.Dual => mount is ModelMount.Gun or ModelMount.GunLeft,
            _ => mount == ModelMount.Gun,
        };
    }
}
