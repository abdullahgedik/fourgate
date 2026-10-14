using System.Collections.Generic;

namespace Fourgate.Core.Data;

/// <summary>Everything loaded from <c>Data/</c>, already validated.</summary>
public sealed class GameData
{
    private readonly Dictionary<string, WeaponConfig> _weaponsById = new();
    private readonly Dictionary<string, EnemyConfig> _enemiesById = new();

    /// <summary>Combines the loaded files and checks the references between them.</summary>
    public GameData(
        GameConfig game,
        IReadOnlyList<WeaponConfig> weapons,
        IReadOnlyList<EnemyConfig> enemies,
        WavesConfig waves,
        UpgradesConfig upgrades,
        IReadOnlyList<BossConfig>? bosses = null)
    {
        Bosses = bosses ?? new List<BossConfig>();
        Waves = waves;
        Upgrades = upgrades;
        Game = game;
        Weapons = weapons;
        Enemies = enemies;

        for (var i = 0; i < weapons.Count; i++)
        {
            _weaponsById[weapons[i].Id] = weapons[i];
        }

        for (var i = 0; i < enemies.Count; i++)
        {
            _enemiesById[enemies[i].Id] = enemies[i];
        }

        // Boss bodies can be looked up like enemies (debug spawning), and summons must name real enemies.
        for (var i = 0; i < Bosses.Count; i++)
        {
            var boss = Bosses[i];
            DataLoadException.Require(
                !_enemiesById.ContainsKey(boss.Id), DataLoader.BossesFileName, $"bosses[{i}].id",
                $"'{boss.Id}' is also an enemy id in {DataLoader.EnemiesFileName}");
            for (var p = 0; p < boss.Patterns.Count; p++)
            {
                var pattern = boss.Patterns[p];
                var patternPath = $"bosses[{i}].patterns[{p}]";
                while (pattern is not null)
                {
                    DataLoadException.Require(
                        pattern.Type != BossPatternType.Summon || _enemiesById.ContainsKey(pattern.EnemyId),
                        DataLoader.BossesFileName, $"{patternPath}.enemyId",
                        $"'{pattern.EnemyId}' is not an enemy id in {DataLoader.EnemiesFileName}");
                    pattern = pattern.FollowUp;
                    patternPath += ".followUp";
                }
            }

            DataLoadException.Require(
                boss.DeathSpawn is null || _enemiesById.ContainsKey(boss.DeathSpawn.EnemyId),
                DataLoader.BossesFileName, $"bosses[{i}].deathSpawn.enemyId",
                $"'{boss.DeathSpawn?.EnemyId}' is not an enemy id in {DataLoader.EnemiesFileName}");
        }

        for (var i = 0; i < Bosses.Count; i++)
        {
            _enemiesById[Bosses[i].Id] = Bosses[i].AsEnemy;
        }

        DataLoadException.Require(
            _weaponsById.ContainsKey(game.Player.StartingWeapon),
            DataLoader.GameFileName,
            "player.startingWeapon",
            $"'{game.Player.StartingWeapon}' is not a weapon id in {DataLoader.WeaponsFileName}");
    }

    public GameConfig Game { get; }

    public IReadOnlyList<WeaponConfig> Weapons { get; }

    public IReadOnlyList<EnemyConfig> Enemies { get; }

    public WavesConfig Waves { get; }

    public UpgradesConfig Upgrades { get; }

    public IReadOnlyList<BossConfig> Bosses { get; }

    /// <summary>The boss leading a wave, or null for a normal wave.</summary>
    public BossConfig? GetBossForWave(int wave)
    {
        for (var i = 0; i < Bosses.Count; i++)
        {
            if (Bosses[i].Wave == wave)
            {
                return Bosses[i];
            }
        }

        return null;
    }

    public WeaponConfig GetWeapon(string id)
    {
        return _weaponsById[id];
    }

    public EnemyConfig GetEnemy(string id)
    {
        return _enemiesById[id];
    }
}
