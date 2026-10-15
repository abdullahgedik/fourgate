namespace Fourgate.Core.Data;

/// <summary>The final score is multiplied by the factor of the mode played.</summary>
public sealed class ScoreMultipliersConfig
{
    public required float Normal { get; init; }

    public required float Fast { get; init; }

    public required float Nightmare { get; init; }

    public required float NightmareFast { get; init; }

    public void Validate(string file)
    {
        DataLoadException.Require(Normal > 0, file, "scoreMultipliers.normal", "must be greater than 0");
        DataLoadException.Require(Fast > 0, file, "scoreMultipliers.fast", "must be greater than 0");
        DataLoadException.Require(Nightmare > 0, file, "scoreMultipliers.nightmare", "must be greater than 0");
        DataLoadException.Require(NightmareFast > 0, file, "scoreMultipliers.nightmareFast", "must be greater than 0");
    }
}
