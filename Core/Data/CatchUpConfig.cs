namespace Fourgate.Core.Data;

/// <summary>
/// Enemies far from every player walk faster, so a wave crosses the big arena quickly and then
/// slows to its own pace before the players can see it. Without this block enemies always walk
/// at their own speed.
/// </summary>
public sealed class CatchUpConfig
{
    /// <summary>Closer than this to the nearest player, an enemy walks at its own speed.</summary>
    public required float NearDistance { get; init; }

    /// <summary>Farther than this, it walks at the full <see cref="SpeedMult"/>; in between it eases.</summary>
    public required float FarDistance { get; init; }

    /// <summary>Speed multiplier at <see cref="FarDistance"/> and beyond.</summary>
    public required float SpeedMult { get; init; }

    /// <summary>Speed multiplier for an enemy this far from the nearest player.</summary>
    public float MultiplierAt(float distance)
    {
        if (distance <= NearDistance)
        {
            return 1f;
        }

        var t = System.Math.Min(1f, (distance - NearDistance) / (FarDistance - NearDistance));
        var eased = t * t * (3f - 2f * t);
        return 1f + (SpeedMult - 1f) * eased;
    }

    public void Validate(string file, string path)
    {
        DataLoadException.Require(NearDistance >= 0, file, $"{path}.nearDistance", "must not be negative");
        DataLoadException.Require(
            FarDistance > NearDistance, file, $"{path}.farDistance", "must be greater than nearDistance");
        DataLoadException.Require(SpeedMult >= 1, file, $"{path}.speedMult", "must be 1 or greater");
    }
}
