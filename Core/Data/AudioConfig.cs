namespace Fourgate.Core.Data;

/// <summary>How sound effects are mixed (<c>game.json</c> <c>audio</c>).</summary>
public sealed class AudioConfig
{
    /// <summary>Most copies of one sound playing at once; more requests are dropped (rapid fire).</summary>
    public int MaxVoicesPerSound { get; init; } = 4;

    /// <summary>Shortest time between two starts of the same sound, in seconds.</summary>
    public float MinRepeatSeconds { get; init; } = 0.03f;

    /// <summary>Each play is pitched up or down by up to this much (0–1, in octaves), so repeats do not sound identical.</summary>
    public float PitchVariation { get; init; } = 0.06f;

    public void Validate(string file)
    {
        DataLoadException.Require(MaxVoicesPerSound >= 1, file, "audio.maxVoicesPerSound", "must be 1 or greater");
        DataLoadException.Require(MinRepeatSeconds >= 0, file, "audio.minRepeatSeconds", "must not be negative");
        DataLoadException.Require(
            PitchVariation is >= 0 and <= 1, file, "audio.pitchVariation", "must be between 0 and 1");
    }
}
