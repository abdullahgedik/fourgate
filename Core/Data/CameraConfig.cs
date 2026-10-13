namespace Fourgate.Core.Data;

/// <summary>Follow-camera tuning.</summary>
public sealed class CameraConfig
{
    /// <summary>Screen pixels per world unit.</summary>
    public required float Zoom { get; init; }

    /// <summary>Share of the aim point's offset from the view center that the camera shifts by (0–1).</summary>
    public required float LeadFraction { get; init; }

    /// <summary>Largest aim shift, in world units.</summary>
    public required float LeadMaxDistance { get; init; }

    /// <summary>How quickly the camera catches up with its target (per second); 0 snaps instantly.</summary>
    public required float Smoothing { get; init; }

    /// <summary>How far past the arena walls the view may extend, in world units.</summary>
    public required float EdgeMargin { get; init; }
}
