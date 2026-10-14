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
}
