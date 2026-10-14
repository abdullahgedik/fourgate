namespace Fourgate.Core.Data;

/// <summary>How hits push enemies back. Bullet knockback is set per weapon (<c>projectile.knockback</c>).</summary>
public sealed class KnockbackConfig
{
    /// <summary>World units an explosion pushes an enemy away from its center.</summary>
    public float Explosion { get; init; }

    /// <summary>
    /// How fast a push is used up: this share of what is left is applied per second, so a push
    /// plays out over roughly 3 / damping seconds. 0 applies it at once.
    /// </summary>
    public float Damping { get; init; }

    public void Validate(string file)
    {
        DataLoadException.Require(Explosion >= 0, file, "knockback.explosion", "must not be negative");
        DataLoadException.Require(Damping >= 0, file, "knockback.damping", "must not be negative");
    }
}
