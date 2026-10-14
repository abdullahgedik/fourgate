using Fourgate.Core.Audio;
using Fourgate.Core.Data;
using Fourgate.Core.Input;
using Fourgate.Scenes;
using Microsoft.Xna.Framework;

namespace Fourgate.Tests;

public class CoopSceneTests
{
    private const float Step = 1f / 60f;

    [Fact]
    public void TwoPlayers_EachMoveWithTheirOwnInput()
    {
        var data = DataLoader.LoadAll(DataLoader.DefaultDirectory);
        var one = new FakeInput();
        var two = new FakeInput { Move = Vector2.UnitX };
        var scene = new GameplayScene(data, new IPlayerInput[] { one, two }, new NullAudioManager());

        var startOne = scene.Players[0].Position;
        var startTwo = scene.Players[1].Position;
        for (var i = 0; i < 30; i++)
        {
            scene.Update(Step);
        }

        Assert.Equal(2, scene.Players.Count);
        Assert.NotEqual(startOne, startTwo);
        Assert.Equal(startOne, scene.Players[0].Position);
        Assert.True(scene.Players[1].Position.X > startTwo.X + 50f);
    }

    [Fact]
    public void OnePlayer_StartsInTheMiddle()
    {
        var data = DataLoader.LoadAll(DataLoader.DefaultDirectory);
        var scene = new GameplayScene(data, new IPlayerInput[] { new FakeInput() }, new NullAudioManager());

        Assert.Single(scene.Players);
        Assert.Equal(new Vector2(1500, 1500), scene.Players[0].Position);
    }

    [Fact]
    public void WhenOnePlayerIsDown_ThePartnerFightsOn_WithTheWholeScreen()
    {
        var data = DataLoader.LoadAll(DataLoader.DefaultDirectory);
        var scene = new GameplayScene(data, new IPlayerInput[] { new FakeInput(), new FakeInput() }, new NullAudioManager());
        Assert.Equal(960, scene.GetView(0).Width);
        Assert.Equal(960, scene.GetView(1).Width);

        scene.Players[1].TakeDamage(1000f);
        scene.Update(Step);

        Assert.False(scene.IsGameOver);
        Assert.Equal(new Rectangle(0, 0, 1920, 1080), scene.GetView(0));
        Assert.Equal(Rectangle.Empty, scene.GetView(1));
    }

    [Fact]
    public void Revive_EmptiesTheInventory_ButKeepsWeaponLevels()
    {
        var data = DataLoader.LoadAll(DataLoader.DefaultDirectory);
        var unlocks = new Fourgate.Systems.WeaponUnlocks(data.Game, new NullAudioManager(), null);
        var player = new Fourgate.Entities.Player(
            1, new FakeInput(), data.Game.Player, new NullAudioManager(), new Vector2(1500, 1500));
        var pistol = data.GetWeapon("pistol");
        var smg = data.GetWeapon("smg");
        unlocks.Grant(player, pistol);
        unlocks.Grant(player, smg);
        player.LevelUpWeapon("smg");
        player.TakeDamage(1000f);

        player.Revive(new Vector2(1600, 1500), 0.5f);
        unlocks.Grant(player, pistol);

        Assert.True(player.IsAlive);
        Assert.Equal(player.MaxHp * 0.5f, player.Hp);
        Assert.Equal(new Vector2(1600, 1500), player.Position);
        Assert.Single(player.Weapons);
        Assert.Equal("pistol", player.CurrentWeapon!.Config.Id);
        Assert.False(player.OwnsWeapon("smg"));
        Assert.Equal(2, player.GetWeaponLevel("smg"));
    }

    [Fact]
    public void UnpluggedController_PausesTheGame_UntilItIsBack()
    {
        var data = DataLoader.LoadAll(DataLoader.DefaultDirectory);
        var one = new FakeInput { Move = Vector2.UnitX };
        var two = new FakeInput { IsConnected = false };
        var scene = new GameplayScene(data, new IPlayerInput[] { one, two }, new NullAudioManager());
        var start = scene.Players[0].Position;

        scene.Update(Step);
        Assert.Equal(1, scene.DisconnectedPlayer);
        Assert.Equal(start, scene.Players[0].Position);

        two.IsConnected = true;
        scene.Update(Step);
        scene.Update(Step);
        Assert.Equal(-1, scene.DisconnectedPlayer);
        Assert.True(scene.Players[0].Position.X > start.X);
    }

    [Fact]
    public void CoopWaves_HaveABiggerEnemyBudget()
    {
        var data = DataLoader.LoadAll(DataLoader.DefaultDirectory);

        var single = StartFirstWave(data, 1f);
        var coop = StartFirstWave(data, data.Game.Coop.EnemyBudgetMultiplier);

        Assert.True(coop > single * 1.4f);
    }

    private static int StartFirstWave(GameData data, float budgetMultiplier)
    {
        var arena = new Fourgate.Entities.Arena(data.Game.Arena);
        var enemies = new Fourgate.Systems.EnemySystem(new NullAudioManager());
        var waves = new Fourgate.Systems.WaveSystem(data, enemies, arena, new Fourgate.Core.Rng.GameRandom(1), new NullAudioManager())
        {
            BudgetMultiplier = budgetMultiplier,
        };

        while (waves.Phase != Fourgate.Systems.WavePhase.Active)
        {
            waves.Update(Step);
        }

        return waves.EnemiesRemaining;
    }
}
