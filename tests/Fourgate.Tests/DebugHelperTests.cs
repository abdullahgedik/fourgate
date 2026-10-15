using Fourgate.Core.Audio;
using Fourgate.Core.Data;
using Fourgate.Core.Input;
using Fourgate.Scenes;

namespace Fourgate.Tests;

public class DebugHelperTests
{
    [Fact]
    public void MaxAllWeapons_PutsEveryWeaponAtTheTopLevel_CarriedOrNot()
    {
        var data = DataLoader.LoadAll(DataLoader.DefaultDirectory);
        var scene = new GameplayScene(data, new IPlayerInput[] { new FakeInput(), new FakeInput() }, new NullAudioManager());

        scene.DebugMaxAllWeapons();

        foreach (var player in scene.Players)
        {
            Assert.Equal(WeaponConfig.MaxLevel, player.CurrentWeapon!.Level);
            foreach (var weapon in data.Weapons)
            {
                Assert.Equal(WeaponConfig.MaxLevel, player.GetWeaponLevel(weapon.Id));
            }
        }

        // Weapons found later come at that level.
        scene.DebugGrantAllWeapons();
        Assert.All(scene.Players[0].Weapons, w => Assert.Equal(WeaponConfig.MaxLevel, w.Level));
    }
}
