namespace Fourgate.Systems;

/// <summary>The difficulty toggles chosen for a run; both off is Normal.</summary>
/// <param name="Fast">Earlier unlocks and more, quicker enemies.</param>
/// <param name="Nightmare">Everything from the start and much harder waves.</param>
public readonly record struct RunMode(bool Fast, bool Nightmare)
{
    /// <summary>Short label for the HUD and high-score lists.</summary>
    public string Name => (Fast, Nightmare) switch
    {
        (false, false) => "NORMAL",
        (true, false) => "FAST",
        (false, true) => "NIGHTMARE",
        _ => "NIGHTMARE + FAST",
    };
}
