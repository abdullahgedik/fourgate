namespace Fourgate.Core.Data;

/// <summary>What a gun's shot throws off where it hits; purely visual, chosen per weapon.</summary>
public enum HitEffect
{
    /// <summary>A small spray of blood drops (pistol, SMG).</summary>
    Light,

    /// <summary>A few drops per pellet, flung wide (shotgun).</summary>
    Pellet,

    /// <summary>A bigger spray and a mist out of the back (rifles).</summary>
    Heavy,

    /// <summary>A flash and a long spray thrown far out of the back, big chips off walls (sniper).</summary>
    Sniper,

    /// <summary>A hot flash, glowing sparks and smoke, no blood (railgun).</summary>
    Energy,
}
