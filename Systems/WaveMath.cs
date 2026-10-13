using System;
using System.Collections.Generic;
using Fourgate.Core.Data;
using Fourgate.Core.Rng;

namespace Fourgate.Systems;

/// <summary>Pure wave formulas. All of them work for any wave number, however high.</summary>
public static class WaveMath
{
    /// <summary>Spawn budget of a wave.</summary>
    public static float Budget(int wave, WavesConfig config)
    {
        var budget = config.Budget;
        return budget.Base + budget.PerWave * wave + budget.Quadratic * wave * wave;
    }

    /// <summary>Seconds between spawn groups in a wave.</summary>
    public static float SpawnInterval(int wave, WavesConfig config)
    {
        var interval = config.SpawnIntervalStart - config.SpawnIntervalStepPerWave * (wave - 1);
        return Math.Max(config.SpawnIntervalMin, interval);
    }

    /// <summary>Enemies per spawn group in a wave.</summary>
    public static int GroupSize(int wave, WavesConfig config)
    {
        var size = (int)MathF.Floor(config.GroupSizeBase + config.GroupSizePerWave * (wave - 1));
        return Math.Clamp(size, 1, config.GroupSizeMax);
    }

    /// <summary>
    /// Spends a budget on enemy types unlocked at <paramref name="wave"/> and appends the picks to
    /// <paramref name="result"/>. Types that unlocked later are picked more often.
    /// </summary>
    public static void Compose(
        int wave,
        float budget,
        IReadOnlyList<EnemyConfig> enemies,
        float newerTypeBias,
        GameRandom random,
        List<EnemyConfig> result)
    {
        while (true)
        {
            var totalWeight = 0f;
            for (var i = 0; i < enemies.Count; i++)
            {
                totalWeight += Weight(enemies[i], wave, budget, enemies, newerTypeBias);
            }

            if (totalWeight <= 0f)
            {
                return;
            }

            var roll = random.Range(0f, totalWeight);
            EnemyConfig? pick = null;
            for (var i = 0; i < enemies.Count; i++)
            {
                var weight = Weight(enemies[i], wave, budget, enemies, newerTypeBias);
                if (weight <= 0f)
                {
                    continue;
                }

                pick = enemies[i];
                roll -= weight;
                if (roll < 0f)
                {
                    break;
                }
            }

            if (pick is null)
            {
                return;
            }

            result.Add(pick);
            budget -= pick.Cost;
        }
    }

    // Zero when the type is locked or too expensive for what is left.
    private static float Weight(
        EnemyConfig enemy, int wave, float budget, IReadOnlyList<EnemyConfig> enemies, float newerTypeBias)
    {
        if (enemy.UnlockWave > wave || enemy.Cost > budget)
        {
            return 0f;
        }

        // "Newness" = how many unlocked types came before this one.
        var olderTypes = 0;
        for (var i = 0; i < enemies.Count; i++)
        {
            if (enemies[i].UnlockWave <= wave && enemies[i].UnlockWave < enemy.UnlockWave)
            {
                olderTypes++;
            }
        }

        return 1f + newerTypeBias * olderTypes;
    }
}
