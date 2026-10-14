namespace Fourgate.Core.Rendering;

public enum ShapeKind
{
    Circle,
    Square,

    /// <summary>Points along the rotation (0 = +X).</summary>
    Triangle,

    /// <summary>A square standing on one corner; does not turn.</summary>
    Diamond,

    /// <summary>Six-sided; turns with the rotation like the triangle.</summary>
    Hexagon,
}
