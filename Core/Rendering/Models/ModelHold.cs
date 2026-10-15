namespace Fourgate.Core.Rendering.Models;

/// <summary>How a model holds what it carries; parts can pose differently for each.</summary>
public enum ModelHold
{
    /// <summary>One gun aimed with the right arm; the left arm is free.</summary>
    OneHand,

    /// <summary>One gun at the chest, held with both hands.</summary>
    TwoHands,

    /// <summary>A gun in each hand, both arms aimed.</summary>
    Dual,

    /// <summary>A launcher resting on the right shoulder, steadied with both hands.</summary>
    Shoulder,
}
