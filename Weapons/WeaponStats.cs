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
    int WallBounces,
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
    bool Overkill,
    float FireRadius,
    float FireSeconds,
    float IgniteSeconds,
    float TriggerRadius,
    float ArmSeconds,
    int MaxPlaced,
    bool ChainDetonation,
    int BlastCount,
    float BlastInterval,
    float BlastSpread,
    bool PassThrough,
    float TrailInterval,
    float TrailSeconds,
    float TrailRadius,
    float TrailDamageMult,
    float TrailSpacing,
    int RingCount,
    float RingDistance,
    float RingDamageMult,
    float RingRadiusMult,
    float RingDelay,
    float RingInterval,
    bool Shootable,
    float LeaveFireRadius,
    float LeaveFireSeconds,
    float LeaveFireDamageMult,
    bool LeaveFireHurtsPlayers,
    bool Solid,
    float GridSize,
    float PlaceDistance,
    int EnemyHitsToDetonate,
    float EndBlastRadius,
    float EndBlastDamageMult)
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
        var range = Reach(config);
        var blastRadius = config.Explosion?.Radius ?? 0f;
        var fireRadius = config.Fire?.Radius ?? 0f;
        var fireSeconds = config.Fire?.Seconds ?? 0f;
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
            fireRadius *= bonus.BlastRadiusMult;
            fireSeconds *= bonus.DurationMult;
            reserveMaxBonus += bonus.ReserveMaxBonus;
        }

        var pierce = 0;
        var ricochets = 0;
        var wallBounces = 0;
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
        var igniteSeconds = 0f;
        var armSeconds = config.Placed?.ArmSeconds ?? 0f;
        var maxPlaced = config.Placed?.MaxPlaced ?? 0;
        var chainDetonation = config.Placed?.ChainDetonation ?? false;
        var blastCount = 1;
        var blastInterval = 0f;
        var blastSpread = 0f;
        var passThrough = config.Stream?.PassThrough ?? false;
        var trailInterval = 0f;
        var trailSeconds = 0f;
        var trailRadius = 0f;
        var trailDamageMult = 0f;
        var trailSpacing = 0f;
        var ringCount = 0;
        var ringDistance = 0f;
        var ringDamageMult = 1f;
        var ringRadiusMult = 1f;
        var ringDelay = 0f;
        var ringInterval = 0f;
        var leaveFireRadius = 0f;
        var leaveFireSeconds = 0f;
        var leaveFireDamageMult = 0f;
        var leaveFireHurtsPlayers = true;
        var endBlastRadius = 0f;
        var endBlastDamageMult = 0f;
        if (level >= WeaponConfig.MaxLevel && config.LevelTen is not null)
        {
            damage *= config.LevelTen.DamageMult;
            fireRate *= config.LevelTen.FireRateMult;
            magSize += config.LevelTen.MagSizeBonus;
            range *= config.LevelTen.RangeMult;
            fireSeconds *= config.LevelTen.DurationMult;
            blastRadius *= config.LevelTen.BlastRadiusMult;
            maxPlaced += config.LevelTen.MaxPlacedBonus;

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
                        wallBounces += abilities[i].WallBounces;
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

                    case WeaponAbility.InstantArm:
                        armSeconds = 0f;
                        break;

                    case WeaponAbility.ChainDetonation:
                        chainDetonation = true;
                        break;

                    case WeaponAbility.LeaveFire:
                        leaveFireRadius = abilities[i].Radius;
                        leaveFireSeconds = abilities[i].Seconds;
                        leaveFireDamageMult = abilities[i].DamageMult;
                        leaveFireHurtsPlayers = abilities[i].HurtsPlayers;
                        break;

                    case WeaponAbility.EndBlast:
                        endBlastRadius = abilities[i].Radius;
                        endBlastDamageMult = abilities[i].DamageMult;
                        break;

                    case WeaponAbility.RingBlast:
                        ringCount = abilities[i].Count;
                        ringDistance = abilities[i].Radius;
                        ringDamageMult = abilities[i].DamageMult;
                        ringRadiusMult = abilities[i].RadiusMult;
                        ringDelay = abilities[i].Seconds;
                        ringInterval = abilities[i].Interval;
                        break;

                    case WeaponAbility.PassThrough:
                        passThrough = true;
                        break;

                    case WeaponAbility.FireTrail:
                        trailInterval = abilities[i].Interval;
                        trailSeconds = abilities[i].Seconds;
                        trailRadius = abilities[i].Radius;
                        trailSpacing = abilities[i].Spacing;
                        trailDamageMult = abilities[i].DamageMult;
                        break;

                    case WeaponAbility.MultiBlast:
                        blastCount = abilities[i].Count;
                        blastInterval = abilities[i].Seconds;
                        blastSpread = abilities[i].Radius;
                        break;

                    case WeaponAbility.SpreadFire:
                        igniteSeconds = abilities[i].Seconds;
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
            pierce, ricochets, wallBounces, ricochetRadius, ricochetDamageMult, dualWield,
            blastRadius, reserveMaxBonus, clusterCount, clusterDistance, clusterDamageMult, clusterRadiusMult,
            empoweredEvery, empoweredDamageMult, overdriveFireRateMult, overdriveSeconds, overkill,
            fireRadius, fireSeconds, igniteSeconds,
            config.Placed?.TriggerRadius ?? 0f, armSeconds, maxPlaced, chainDetonation,
            blastCount, blastInterval, blastSpread,
            passThrough, trailInterval, trailSeconds, trailRadius, trailDamageMult, trailSpacing,
            ringCount, ringDistance, ringDamageMult, ringRadiusMult, ringDelay, ringInterval,
            config.Placed?.Shootable ?? false, leaveFireRadius, leaveFireSeconds, leaveFireDamageMult,
            leaveFireHurtsPlayers, config.Placed?.Solid ?? false, config.Placed?.GridSize ?? 0f,
            config.Placed?.PlaceDistance ?? 0f, config.Placed?.EnemyHitsToDetonate ?? 0,
            endBlastRadius, endBlastDamageMult);
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
            AddPercent(lines, bonus.BlastRadiusMult, config.Fire is not null ? "fire area" : "blast radius");
            AddPercent(lines, bonus.DurationMult, "burn time");
            if (bonus.ReserveMaxBonus > 0)
            {
                lines.Add($"+{bonus.ReserveMaxBonus} carried");
            }
        }
    }

    // Bullets and beams without a range fly on until something solid stops them.
    private static float Reach(WeaponConfig config)
    {
        if (config.Projectile is not null)
        {
            return config.Projectile.Range ?? float.PositiveInfinity;
        }

        if (config.Beam is not null)
        {
            return config.Beam.Range ?? float.PositiveInfinity;
        }

        return config.Thrown?.MaxRange ?? config.Stream?.Range ?? 0f;
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
