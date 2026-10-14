using Fourgate.Scenes;
using Microsoft.Xna.Framework;

namespace Fourgate.Tests;

public class ModeSelectSceneTests
{
    private const float Step = 1f / 60f;

    [Fact]
    public void Confirming_WithoutMoving_ChoosesOnePlayer()
    {
        var input = new FakeInput();
        var scene = new ModeSelectScene(input, TestData.Game());

        scene.Update(Step);
        Assert.Null(scene.ChosenPlayers);

        input.ConfirmPressed = true;
        scene.Update(Step);

        Assert.Equal(1, scene.ChosenPlayers);
    }

    [Fact]
    public void TwoPlayers_CannotBeStarted_UntilCoopExists()
    {
        var input = new FakeInput { MenuRightPressed = true };
        var scene = new ModeSelectScene(input, TestData.Game());
        scene.Update(Step);

        input.MenuRightPressed = false;
        input.ConfirmPressed = true;
        scene.Update(Step);

        Assert.Null(scene.ChosenPlayers);
    }

    [Fact]
    public void ClickingOnACard_ChoosesIt()
    {
        // The left card spans x 480-920 and y from 42% of the height (453.6) for 260.
        var input = new FakeInput { HasPointer = true, AimScreen = new Vector2(700, 580), FirePressed = true };
        var scene = new ModeSelectScene(input, TestData.Game(), selectedPlayers: 2);

        scene.Update(Step);

        Assert.Equal(1, scene.ChosenPlayers);
    }
}
