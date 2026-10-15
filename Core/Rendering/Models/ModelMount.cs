namespace Fourgate.Core.Rendering.Models;

/// <summary>What a model part holds.</summary>
public enum ModelMount
{
    None,

    /// <summary>The gun (the right hand).</summary>
    Gun,

    /// <summary>The second gun of a dual-wielded weapon (the left hand).</summary>
    GunLeft,

    /// <summary>A gun held at the chest with both hands.</summary>
    GunTwoHands,

    /// <summary>A launcher on the shoulder.</summary>
    GunShoulder,
}
