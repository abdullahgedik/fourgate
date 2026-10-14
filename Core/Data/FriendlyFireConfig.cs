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

    /// <summary>Checks value ranges; throws <see cref="DataLoadException"/> naming the bad field.</summary>
    public void Validate(string file)
    {
        DataLoadException.Require(
            DamageToOther >= 0 && DamageToOther <= 1, file, "friendlyFire.damageToOther", "must be between 0 and 1");
        DataLoadException.Require(
            SelfExplosiveDamage >= 0 && SelfExplosiveDamage <= 1,
            file, "friendlyFire.selfExplosiveDamage", "must be between 0 and 1");
    }
}
