namespace Fourgate.Core.Audio;

/// <summary>Audio events raised by gameplay. Ids are listed in <see cref="SfxIds"/>.</summary>
public interface IAudioManager
{
    void PlaySfx(string id, float volume = 1f);

    void PlayMusic(string id);
}
