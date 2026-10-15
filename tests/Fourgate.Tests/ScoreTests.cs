using Fourgate.Core.Audio;
using Fourgate.Core.Data;
using Fourgate.Core.Input;
using Fourgate.Scenes;
using Fourgate.Systems;

namespace Fourgate.Tests;

public class ScoreTests
{
    private static readonly ScoreConfig Points = new() { WaveClearBonus = 250, PointsPerSecond = 1f };

    [Fact]
    public void Score_AddsKills_ClearedWaves_AndWholeSecondsSurvived()
    {
        var score = new ScoreSystem(Points, 1f);

        score.AddKill(10);
        score.AddKill(1000);
        score.AddClearedWave();
        score.AddClearedWave();
        score.AddTime(61.9f);

        Assert.Equal(10 + 1000 + 2 * 250 + 61, score.Total);
    }

    [Fact]
    public void Score_IsMultipliedByTheModeFactor()
    {
        var score = new ScoreSystem(Points, 1.9f);

        score.AddKill(100);
        score.AddClearedWave();

        Assert.Equal((int)MathF.Round(350 * 1.9f), score.Total);
    }

    [Theory]
    [InlineData(false, false, 1f)]
    [InlineData(true, false, 1.25f)]
    [InlineData(false, true, 1.5f)]
    [InlineData(true, true, 1.9f)]
    public void ShippedModes_HaveTheDesignedScoreFactors(bool fast, bool nightmare, float factor)
    {
        var modes = DataLoader.LoadAll(DataLoader.DefaultDirectory).Modes;

        Assert.Equal(factor, new ModeRules(modes, new RunMode(fast, nightmare)).ScoreMultiplier, 3);
    }

    [Fact]
    public void Run_CountsSurvivalTime_AndOneTeamScore()
    {
        var data = DataLoader.LoadAll(DataLoader.DefaultDirectory);
        var scene = new GameplayScene(data, new IPlayerInput[] { new FakeInput(), new FakeInput() }, new NullAudioManager());

        for (var i = 0; i < 60 * 3; i++)
        {
            scene.Update(1f / 60f);
        }

        Assert.InRange(scene.Score, 2, 3);
    }
}
