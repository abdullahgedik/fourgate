using Fourgate.Core.Audio;
using Fourgate.Core.Data;
using Fourgate.Core.Input;
using Fourgate.Scenes;
using Microsoft.Xna.Framework;

namespace Fourgate.Tests;

public class PauseTests
{
    private const float Step = 1f / 60f;

    [Fact]
    public void Pause_FreezesTheRun_UntilResumed()
    {
        var input = new FakeInput { Move = Vector2.UnitX };
        var scene = Run(input);
        var start = scene.Players[0].Position;

        input.PausePressed = true;
        scene.Update(Step);
        input.PausePressed = false;
        for (var i = 0; i < 30; i++)
        {
            scene.Update(Step);
        }

        Assert.True(scene.IsPaused);
        Assert.Equal(start, scene.Players[0].Position);

        // Esc again resumes, and the same key press does not pause it straight back.
        input.PausePressed = true;
        scene.Update(Step);
        scene.Update(Step);
        input.PausePressed = false;
        scene.Update(Step);

        Assert.False(scene.IsPaused);
        Assert.True(scene.Players[0].Position.X > start.X);
    }

    [Fact]
    public void Pause_SecondPlayerCanPause_TooAndDrivesTheMenu()
    {
        var one = new FakeInput();
        var two = new FakeInput { PausePressed = true };
        var scene = Run(one, two);

        scene.Update(Step);
        Assert.True(scene.IsPaused);

        // Player 2 picks Settings (one down, confirm).
        two.PausePressed = false;
        two.MenuDownPressed = true;
        scene.Update(Step);
        two.MenuDownPressed = false;
        two.ConfirmPressed = true;
        scene.Update(Step);

        Assert.True(scene.SettingsRequested);
        scene.ReturnFromSettings();
        Assert.False(scene.SettingsRequested);
        Assert.True(scene.IsPaused);
    }

    [Fact]
    public void Pause_QuitToMenu_EndsTheRun()
    {
        var input = new FakeInput { PausePressed = true };
        var scene = Run(input);
        scene.Update(Step);

        input.PausePressed = false;
        input.MenuUpPressed = true;
        scene.Update(Step);
        input.MenuUpPressed = false;
        input.ConfirmPressed = true;
        scene.Update(Step);

        Assert.True(scene.QuitRequested);
    }

    [Fact]
    public void ClearingTheLastBossWave_ShowsTheWavesSurvivedScreen_ThenContinues()
    {
        var input = new FakeInput();
        var scene = EmptyCampaign(input);

        RunUntil(scene, () => scene.IsShowingVictory);
        Assert.True(scene.IsShowingVictory);

        input.ConfirmPressed = true;
        scene.Update(Step);

        Assert.False(scene.IsShowingVictory);
        Assert.False(scene.IsGameOver);

        // It is shown once only.
        input.ConfirmPressed = false;
        for (var i = 0; i < 60 * 12; i++)
        {
            scene.Update(Step);
        }

        Assert.False(scene.IsShowingVictory);
    }

    [Fact]
    public void WavesSurvivedScreen_CanEndTheRun()
    {
        var input = new FakeInput();
        var scene = EmptyCampaign(input);
        RunUntil(scene, () => scene.IsShowingVictory);

        input.MenuDownPressed = true;
        scene.Update(Step);
        input.MenuDownPressed = false;
        input.ConfirmPressed = true;
        scene.Update(Step);

        Assert.True(scene.IsGameOver);
    }

    private static GameplayScene Run(params IPlayerInput[] inputs)
    {
        return new GameplayScene(DataLoader.LoadAll(DataLoader.DefaultDirectory), inputs, new NullAudioManager());
    }

    // A one-wave campaign whose waves have no enemies, so wave 1 is cleared at once.
    private static GameplayScene EmptyCampaign(FakeInput input)
    {
        var shipped = DataLoader.LoadAll(DataLoader.DefaultDirectory);
        var waves = TestData.Waves();
        var empty = new WavesConfig
        {
            Budget = new WaveBudgetConfig { Base = 0, PerWave = 0, Quadratic = 0 },
            NewerTypeBias = waves.NewerTypeBias,
            SpawnIntervalStart = waves.SpawnIntervalStart,
            SpawnIntervalStepPerWave = waves.SpawnIntervalStepPerWave,
            SpawnIntervalMin = waves.SpawnIntervalMin,
            GroupSizeBase = waves.GroupSizeBase,
            GroupSizePerWave = waves.GroupSizePerWave,
            GroupSizeMax = waves.GroupSizeMax,
            DoorWarningSeconds = waves.DoorWarningSeconds,
        };
        var data = new GameData(
            TestData.Game(campaignWaves: 1), shipped.Weapons, shipped.Enemies, empty, shipped.Upgrades, shipped.Bosses,
            shipped.Modes);
        return new GameplayScene(data, new IPlayerInput[] { input }, new NullAudioManager());
    }

    private static void RunUntil(GameplayScene scene, Func<bool> done)
    {
        for (var i = 0; i < 60 * 20 && !done(); i++)
        {
            scene.Players[0].HealToFull();
            scene.Update(Step);
        }
    }
}
