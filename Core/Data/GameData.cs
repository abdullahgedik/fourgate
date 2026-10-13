using System.Collections.Generic;

namespace Fourgate.Core.Data;

/// <summary>Everything loaded from <c>Data/</c>, already validated.</summary>
public sealed class GameData
{
    private readonly Dictionary<string, WeaponConfig> _weaponsById = new();

    /// <summary>Combines the loaded files and checks the references between them.</summary>
    public GameData(GameConfig game, IReadOnlyList<WeaponConfig> weapons)
    {
        Game = game;
        Weapons = weapons;

        for (var i = 0; i < weapons.Count; i++)
        {
            _weaponsById[weapons[i].Id] = weapons[i];
        }

        DataLoadException.Require(
            _weaponsById.ContainsKey(game.Player.StartingWeapon),
            DataLoader.GameFileName,
            "player.startingWeapon",
            $"'{game.Player.StartingWeapon}' is not a weapon id in {DataLoader.WeaponsFileName}");
    }

    public GameConfig Game { get; }

    public IReadOnlyList<WeaponConfig> Weapons { get; }

    public WeaponConfig GetWeapon(string id)
    {
        return _weaponsById[id];
    }
}
