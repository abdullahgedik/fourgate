using Microsoft.Xna.Framework;

namespace Fourgate.Weapons;

/// <summary>One flame sprayed by a stream weapon.</summary>
/// <param name="Origin">World position of the nozzle.</param>
/// <param name="Direction">Unit-length flight direction.</param>
/// <param name="Speed">World units per second.</param>
/// <param name="Range">How far it reaches.</param>
/// <param name="RadiusStart">Size at the nozzle.</param>
/// <param name="RadiusEnd">Size at the end of its reach.</param>
/// <param name="Damage">Damage it deals to each enemy it touches.</param>
/// <param name="OwnerIndex">Index of the player who fired it.</param>
/// <param name="PassThrough">True when it keeps going after touching an enemy.</param>
/// <param name="TrailRadius">Radius of the fire pools it leaves along its path; 0 for none.</param>
/// <param name="TrailSeconds">How long those fire pools burn.</param>
/// <param name="TrailDamagePerSecond">Damage per second of those fire pools.</param>
/// <param name="TrailSpacing">Distance between those fire pools; the first is one spacing from the nozzle.</param>
public readonly record struct FlameShot(
    Vector2 Origin,
    Vector2 Direction,
    float Speed,
    float Range,
    float RadiusStart,
    float RadiusEnd,
    float Damage,
    int OwnerIndex,
    bool PassThrough = false,
    float TrailRadius = 0f,
    float TrailSeconds = 0f,
    float TrailDamagePerSecond = 0f,
    float TrailSpacing = 0f);
