namespace Fourgate.Core.Audio;

/// <summary>Silent audio manager used until real sound is added.</summary>
public sealed class NullAudioManager : IAudioManager
{
    public void PlaySfx(string id, float volume = 1f)
    {
    }

    public void PlayMusic(string id)
    {
    }
}
