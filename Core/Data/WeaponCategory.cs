namespace Fourgate.Core.Data;

/// <summary>How a weapon is carried and used.</summary>
public enum WeaponCategory
{
    /// <summary>Held in hand and fired with the fire button; selected with keys 1–9 or cycled.</summary>
    Gun,

    /// <summary>Never held: the active piece is used with its own button while the gun stays in hand.</summary>
    Equipment,
}
