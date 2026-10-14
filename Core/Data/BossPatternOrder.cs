namespace Fourgate.Core.Data;

/// <summary>How a boss chooses its next pattern.</summary>
public enum BossPatternOrder
{
    /// <summary>A weighted random pick among the patterns that make sense from where it stands.</summary>
    Random,

    /// <summary>The patterns one after another in the order listed, starting over at the end.</summary>
    Sequence,

    /// <summary>
    /// Every pattern once per round, in a new random order each round (never the same one twice
    /// in a row across rounds).
    /// </summary>
    Shuffled,
}
