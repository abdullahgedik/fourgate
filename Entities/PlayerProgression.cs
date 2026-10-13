using Fourgate.Core.Data;

namespace Fourgate.Entities;

/// <summary>One player's level, XP and unspent upgrade picks.</summary>
public sealed class PlayerProgression
{
    public int Level { get; private set; } = 1;

    /// <summary>XP gathered toward the next level.</summary>
    public int Xp { get; private set; }

    /// <summary>Upgrade picks waiting to be spent in the rest phase.</summary>
    public int BankedPicks { get; private set; }

    /// <summary>Adds XP and applies any level-ups; each one banks an upgrade pick. Returns the levels gained.</summary>
    public int AddXp(int amount, XpConfig config)
    {
        if (amount <= 0)
        {
            return 0;
        }

        Xp += amount;

        var gained = 0;
        var needed = config.XpToNext(Level);
        while (Xp >= needed)
        {
            Xp -= needed;
            Level++;
            BankedPicks++;
            gained++;
            needed = config.XpToNext(Level);
        }

        return gained;
    }

    /// <summary>Uses up one banked pick.</summary>
    public void SpendPick()
    {
        if (BankedPicks > 0)
        {
            BankedPicks--;
        }
    }

    /// <summary>Banks one extra upgrade pick (upgrade crate).</summary>
    public void AddPick()
    {
        BankedPicks++;
    }
}
