using System;
using System.Collections.Generic;
using Fourgate.Core.Data;

namespace Fourgate.Weapons;

/// <summary>A weapon's effective numbers at a given level.</summary>
public readonly record struct WeaponStats(
    float Damage,
    float FireRate,
    int MagSize,
    float ReloadTime,
    float Range,
    float SpreadDegrees,
    int Pierce,
    int Ricochets,
    bool DualWield)
{
    /// <summary>
    /// Applies every level bonus up to <paramref name="level"/> to the weapon's base stats;
    /// level 10 adds the special abilities.
    /// </summary>
    public static WeaponStats Compute(WeaponConfig config, int level)
    {
        level = Math.Clamp(level, 1, WeaponConfig.MaxLevel);

        var damage = config.Damage;
        var fireRate = config.FireRate;
        var magSize = config.Ammo.MagSize;
        var reloadTime = config.Ammo.ReloadTime;
        var range = config.Projectile.Range;
        var spread = config.SpreadDegrees;

        var levels = config.Levels;
        for (var i = 0; i < levels.Count; i++)
        {
            var bonus = levels[i];
            if (bonus.Level > level)
            {
                continue;
            }

            damage *= bonus.DamageMult;
            fireRate *= bonus.FireRateMult;
            magSize += bonus.MagSizeBonus;
            reloadTime *= bonus.ReloadTimeMult;
            range *= bonus.RangeMult;
            spread *= bonus.SpreadMult;
        }

        var pierce = 0;
        var ricochets = 0;
        var dualWield = false;
        if (level >= WeaponConfig.MaxLevel && config.LevelTen is not null)
        {
            damage *= config.LevelTen.DamageMult;

            var abilities = config.LevelTen.Abilities;
            for (var i = 0; i < abilities.Count; i++)
            {
                switch (abilities[i].Id)
                {
                    case WeaponAbility.Pierce:
                        pierce += abilities[i].Count;
                        break;

                    case WeaponAbility.Ricochet:
                        ricochets += abilities[i].Count;
                        break;

                    case WeaponAbility.DualWield:
                        dualWield = true;
                        break;
                }
            }
        }

        return new WeaponStats(damage, fireRate, magSize, reloadTime, range, spread, pierce, ricochets, dualWield);
    }

    /// <summary>
    /// Appends short lines describing what reaching <paramref name="level"/> adds, for the upgrade card.
    /// </summary>
    public static void DescribeLevel(WeaponConfig config, int level, List<string> lines)
    {
        if (level >= WeaponConfig.MaxLevel)
        {
            if (config.LevelTen is not null)
            {
                lines.AddRange(config.LevelTen.Description);
            }

            return;
        }

        for (var i = 0; i < config.Levels.Count; i++)
        {
            var bonus = config.Levels[i];
            if (bonus.Level != level)
            {
                continue;
            }

            AddPercent(lines, bonus.DamageMult, "damage");
            AddPercent(lines, bonus.FireRateMult, "fire rate");
            if (bonus.MagSizeBonus > 0)
            {
                lines.Add($"+{bonus.MagSizeBonus} magazine");
            }

            AddPercent(lines, bonus.ReloadTimeMult, "reload time");
            AddPercent(lines, bonus.RangeMult, "range");
            AddPercent(lines, bonus.SpreadMult, "spread");
        }
    }

    private static void AddPercent(List<string> lines, float multiplier, string stat)
    {
        var percent = (int)MathF.Round((multiplier - 1f) * 100f);
        if (percent != 0)
        {
            lines.Add($"{(percent > 0 ? "+" : "-")}{Math.Abs(percent)}% {stat}");
        }
    }
}
