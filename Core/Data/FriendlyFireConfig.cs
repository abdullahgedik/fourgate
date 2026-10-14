namespace Fourgate.Core.Data;

/// <summary>How much of their own damage players take from themselves and each other.</summary>
public sealed class FriendlyFireConfig
{
    /// <summary>When false, players never damage the other player.</summary>
    public required bool Enabled { get; init; }

    /// <summary>Share of the damage a player deals to the other player (0–1).</summary>
    public required float DamageToOther { get; init; }

    /// <summary>Share of their own explosives' damage a player takes (0–1).</summary>
    public required float SelfExplosiveDamage { get; init; }

    /// <summary>Most a player loses to one explosion from themselves or the other player; 0 for no cap.</summary>
    public float MaxDamagePerExplosion { get; init; }

    /// <summary>Most a player loses per second to fire from themselves or the other player; 0 for no cap.</summary>
    public float MaxDamagePerSecond { get; init; }

    /// <summary>When false, damage from a player's own or the other player's weapons never goes below 1 HP.</summary>
    public bool CanKill { get; init; } = true;

    /// <summary>Checks value ranges; throws <see cref="DataLoadException"/> naming the bad field.</summary>
    public void Validate(string file)
    {
        DataLoadException.Require(
            DamageToOther >= 0 && DamageToOther <= 1, file, "friendlyFire.damageToOther", "must be between 0 and 1");
        DataLoadException.Require(
            SelfExplosiveDamage >= 0 && SelfExplosiveDamage <= 1,
            file, "friendlyFire.selfExplosiveDamage", "must be between 0 and 1");
        DataLoadException.Require(
            MaxDamagePerExplosion >= 0, file, "friendlyFire.maxDamagePerExplosion", "must not be negative (0 means no cap)");
        DataLoadException.Require(
            MaxDamagePerSecond >= 0, file, "friendlyFire.maxDamagePerSecond", "must not be negative (0 means no cap)");
    }
}
