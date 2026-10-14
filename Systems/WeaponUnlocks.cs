using System.Collections.Generic;
using Fourgate.Core.Audio;
using Fourgate.Core.Data;
using Fourgate.Core.Rng;
using Fourgate.Entities;
using Fourgate.Weapons;

namespace Fourgate.Systems;

/// <summary>Tracks which weapons have unlocked in this run and hands them to players.</summary>
public sealed class WeaponUnlocks
{
    private readonly GameConfig _rules;
    private readonly IAudioManager _audio;
    private readonly GameRandom? _random;
    private readonly List<WeaponConfig> _unlocked = new();

    public WeaponUnlocks(GameConfig rules, IAudioManager audio, GameRandom? random)
    {
        _rules = rules;
        _audio = audio;
        _random = random;
    }

    /// <summary>Weapons available in this run, in unlock order (guns before equipment within a wave).</summary>
    public IReadOnlyList<WeaponConfig> Unlocked => _unlocked;

    public bool IsUnlocked(WeaponConfig weapon)
    {
        return _unlocked.Contains(weapon);
    }

    /// <summary>Makes a weapon available. Returns false when it already was.</summary>
    public bool Unlock(WeaponConfig weapon)
    {
        if (_unlocked.Contains(weapon))
        {
            return false;
        }

        var index = 0;
        while (index < _unlocked.Count && ComesBefore(_unlocked[index], weapon))
        {
            index++;
        }

        _unlocked.Insert(index, weapon);
        return true;
    }

    /// <summary>The earliest-unlocked weapon the player does not carry, or null when they have them all.</summary>
    public WeaponConfig? FirstMissingFor(Player player)
    {
        for (var i = 0; i < _unlocked.Count; i++)
        {
            if (!player.OwnsWeapon(_unlocked[i].Id))
            {
                return _unlocked[i];
            }
        }

        return null;
    }

    private static bool ComesBefore(WeaponConfig a, WeaponConfig b)
    {
        if (a.UnlockWave != b.UnlockWave)
        {
            return a.UnlockWave < b.UnlockWave;
        }

        if (a.Category != b.Category)
        {
            return a.Category < b.Category;
        }

        return a.Slot <= b.Slot;
    }

    /// <summary>Gives the player a fresh copy of a weapon. Returns false when they already carry it.</summary>
    public bool Grant(Player player, WeaponConfig weapon)
    {
        if (player.OwnsWeapon(weapon.Id))
        {
            return false;
        }

        player.GiveWeapon(new Weapon(weapon, _rules, _audio, _random));
        return true;
    }
}
