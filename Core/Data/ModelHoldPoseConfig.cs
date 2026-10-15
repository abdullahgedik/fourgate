using System.Collections.Generic;

namespace Fourgate.Core.Data;

/// <summary>A part's pose for one way of holding a gun; anything left out keeps the part's own value.</summary>
public sealed class ModelHoldPoseConfig
{
    /// <summary>Rest pose: pitch, yaw, roll in degrees.</summary>
    public List<float>? Rest { get; init; }

    /// <summary>Walk swing at full stride, in degrees.</summary>
    public float? Swing { get; init; }

    /// <summary>Where in the step cycle the swing peaks, in degrees.</summary>
    public float? SwingPhase { get; init; }

    public void Validate(string file, string path)
    {
        DataLoadException.Require(Rest is null || Rest.Count == 3, file, $"{path}.rest", "must have 3 values (pitch, yaw, roll)");
    }
}
