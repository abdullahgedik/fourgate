namespace Fourgate.Core.Audio;

/// <summary>Audio events raised by gameplay. Ids are listed in <see cref="SfxIds"/>.</summary>
public interface IAudioManager
{
    void PlaySfx(string id, float volume = 1f);

    /// <summary>Starts a music track; asking for the track already playing changes nothing.</summary>
    void PlayMusic(string id);

    /// <summary>Volumes 0–1; effects and music are each scaled by the master volume.</summary>
    void SetVolumes(float master, float music, float effects);
}
