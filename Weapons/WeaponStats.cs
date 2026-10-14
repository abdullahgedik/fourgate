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
    int Pellets,
    bool Automatic,
    int Pierce,
    int Ricochets,
    float RicochetRadius,
    float RicochetDamageMult,
    bool DualWield,
    float BlastRadius,
    int ReserveMaxBonus,
    int ClusterCount,
    float ClusterDistance,
    float ClusterDamageMult,
    float ClusterRadiusMult,
    int EmpoweredEvery,
    float EmpoweredDamageMult,
    float OverdriveFireRateMult,
    float OverdriveSeconds,
    bool Overkill)
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
        var range = config.Projectile?.Range ?? config.Thrown?.MaxRange ?? 0f;
        var blastRadius = config.Explosion?.Radius ?? 0f;
        var reserveMaxBonus = 0;
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
            blastRadius *= bonus.BlastRadiusMult;
            reserveMaxBonus += bonus.ReserveMaxBonus;
        }

        var pierce = 0;
        var ricochets = 0;
        var ricochetRadius = 0f;
        var ricochetDamageMult = 1f;
        var dualWield = false;
        var automatic = config.Automatic;
        var clusterCount = 0;
        var clusterDistance = 0f;
        var clusterDamageMult = 1f;
        var clusterRadiusMult = 1f;
        var empoweredEvery = 0;
        var empoweredDamageMult = 1f;
        var overdriveFireRateMult = 1f;
        var overdriveSeconds = 0f;
        var overkill = false;
        if (level >= WeaponConfig.MaxLevel && config.LevelTen is not null)
        {
            damage *= config.LevelTen.DamageMult;
            fireRate *= config.LevelTen.FireRateMult;
            magSize += config.LevelTen.MagSizeBonus;

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
                        ricochetRadius = abilities[i].Radius;
                        ricochetDamageMult = abilities[i].DamageMult;
                        break;

                    case WeaponAbility.DualWield:
                        dualWield = true;
                        break;

                    case WeaponAbility.FullAuto:
                        automatic = true;
                        break;

                    case WeaponAbility.EmpoweredShot:
                        empoweredEvery = abilities[i].Count;
                        empoweredDamageMult = abilities[i].DamageMult;
                        break;

                    case WeaponAbility.Overkill:
                        overkill = true;
                        break;

                    case WeaponAbility.Overdrive:
                        overdriveFireRateMult = abilities[i].FireRateMult;
                        overdriveSeconds = abilities[i].Seconds;
                        break;

                    case WeaponAbility.Cluster:
                        clusterCount += abilities[i].Count;
                        clusterDistance = abilities[i].Radius;
                        clusterDamageMult = abilities[i].DamageMult;
                        clusterRadiusMult = abilities[i].RadiusMult;
                        break;
                }
            }
        }

        return new WeaponStats(
            damage, fireRate, magSize, reloadTime, range, spread, config.Pellets, automatic,
            pierce, ricochets, ricochetRadius, ricochetDamageMult, dualWield,
            blastRadius, reserveMaxBonus, clusterCount, clusterDistance, clusterDamageMult, clusterRadiusMult,
            empoweredEvery, empoweredDamageMult, overdriveFireRateMult, overdriveSeconds, overkill);
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
            AddPercent(lines, bonus.BlastRadiusMult, "blast radius");
            if (bonus.ReserveMaxBonus > 0)
            {
                lines.Add($"+{bonus.ReserveMaxBonus} carried");
            }
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
