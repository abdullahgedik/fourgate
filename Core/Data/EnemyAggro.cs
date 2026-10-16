namespace Fourgate.Core.Data;

/// <summary>How an enemy type chooses whom to chase in co-op (<c>enemies.json</c> <c>aggro</c>).</summary>
public enum EnemyAggro
{
    /// <summary>The nearest player, kept until another is clearly closer.</summary>
    Nearest,

    /// <summary>The player with the least health left, kept until another is clearly weaker.</summary>
    Weakest,

    /// <summary>Whoever hurt it last, for a while; otherwise like <see cref="Nearest"/>.</summary>
    Grudge,

    /// <summary>The nearest player, held for a few seconds before it looks again.</summary>
    Committed,
}
