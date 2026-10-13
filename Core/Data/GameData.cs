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
        UpgradesConfig upgrades)
    {
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

    public WeaponConfig GetWeapon(string id)
    {
        return _weaponsById[id];
    }

    public EnemyConfig GetEnemy(string id)
    {
        return _enemiesById[id];
    }
}
