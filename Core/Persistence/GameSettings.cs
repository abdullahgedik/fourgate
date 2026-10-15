using Fourgate.Core.Rendering;

namespace Fourgate.Core.Persistence;

/// <summary>The player's settings, kept in <c>settings.json</c>.</summary>
public sealed class GameSettings
{
    public const int MaxVolume = 100;

    public WindowMode WindowMode { get; set; } = WindowMode.Windowed;

    /// <summary>Window size while windowed.</summary>
    public int ResolutionWidth { get; set; } = 1280;

    public int ResolutionHeight { get; set; } = 720;

    /// <summary>Draw the game with the 3D look; false for the flat top-down look.</summary>
    public bool ThreeDView { get; set; } = true;

    /// <summary>Shake the view on explosions and hits.</summary>
    public bool ScreenShake { get; set; } = true;

    /// <summary>0–100; scales music and effects; stored for when audio exists.</summary>
    public int MasterVolume { get; set; } = 100;

    /// <summary>0–100; stored for when music exists.</summary>
    public int MusicVolume { get; set; } = 70;

    /// <summary>0–100; stored for when sound effects exist.</summary>
    public int SfxVolume { get; set; } = 80;

    /// <summary>Pulls hand-edited or old values back into range.</summary>
    public void Clamp()
    {
        MasterVolume = System.Math.Clamp(MasterVolume, 0, MaxVolume);
        MusicVolume = System.Math.Clamp(MusicVolume, 0, MaxVolume);
        SfxVolume = System.Math.Clamp(SfxVolume, 0, MaxVolume);
        if (ResolutionWidth <= 0 || ResolutionHeight <= 0)
        {
            ResolutionWidth = 1280;
            ResolutionHeight = 720;
        }

        if (!System.Enum.IsDefined(WindowMode))
        {
            WindowMode = WindowMode.Windowed;
        }
    }
}
