namespace Fourgate.Core.Data;

/// <summary>A part's poses for each way of holding a gun (arms, mostly).</summary>
public sealed class ModelPartHoldsConfig
{
    /// <summary>One gun aimed with one arm.</summary>
    public ModelHoldPoseConfig? OneHand { get; init; }

    /// <summary>One gun at the chest with both hands.</summary>
    public ModelHoldPoseConfig? TwoHands { get; init; }

    /// <summary>A gun in each hand.</summary>
    public ModelHoldPoseConfig? Dual { get; init; }

    /// <summary>A launcher on the shoulder.</summary>
    public ModelHoldPoseConfig? Shoulder { get; init; }

    public void Validate(string file, string path)
    {
        OneHand?.Validate(file, $"{path}.oneHand");
        TwoHands?.Validate(file, $"{path}.twoHands");
        Dual?.Validate(file, $"{path}.dual");
        Shoulder?.Validate(file, $"{path}.shoulder");
    }
}
