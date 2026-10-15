namespace Fourgate.Core.Rendering;

/// <summary>Shared heights for the 3D look, so things can sit on top of a body. Ignored in 2D.</summary>
public static class Depth3D
{
    /// <summary>A solid body is this many times its radius tall.</summary>
    public const float BodyHeightPerRadius = 1.6f;

    /// <summary>How tall a body of this radius stands.</summary>
    public static float BodyHeight(float radius)
    {
        return radius * BodyHeightPerRadius;
    }
}
