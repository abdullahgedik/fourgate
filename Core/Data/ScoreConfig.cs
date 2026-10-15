namespace Fourgate.Core.Data;

/// <summary>Score points from <c>game.json</c>; kills score their enemy's own <c>scoreValue</c>.</summary>
public sealed class ScoreConfig
{
    /// <summary>Points for every wave cleared.</summary>
    public required int WaveClearBonus { get; init; }

    /// <summary>Points per second survived.</summary>
    public required float PointsPerSecond { get; init; }

    public void Validate(string file)
    {
        DataLoadException.Require(WaveClearBonus >= 0, file, "score.waveClearBonus", "must not be negative");
        DataLoadException.Require(PointsPerSecond >= 0, file, "score.pointsPerSecond", "must not be negative");
    }
}
