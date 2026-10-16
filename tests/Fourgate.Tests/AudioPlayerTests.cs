using Fourgate.Core.Audio;
using Fourgate.Core.Data;

namespace Fourgate.Tests;

public class AudioPlayerTests
{
    [Theory]
    [InlineData("Assets/Audio/weapon_fire_shotgun.wav", "weapon_fire_shotgun")]
    [InlineData("enemy_death-2.wav", "enemy_death")]
    [InlineData("enemy_death-12.wav", "enemy_death")]
    [InlineData("UI_Move.WAV", "ui_move")]
    [InlineData("music_boss.ogg", "music_boss")]
    [InlineData("odd-name.wav", "odd-name")]
    public void FileNames_GiveTheirEventId_WithoutVariantNumbers(string file, string id)
    {
        Assert.Equal(id, SoundCatalog.IdOf(file));
    }

    [Fact]
    public void Catalog_GathersVariants_UnderOneId()
    {
        var catalog = new SoundCatalog(new[] { "enemy_death.wav", "enemy_death-2.wav", "enemy_death-3.wav", "ui_move.wav" });

        Assert.Equal(3, catalog.Resolve("enemy_death")!.Count);
        Assert.Single(catalog.Resolve("ui_move")!);
        Assert.Null(catalog.Resolve("explosion"));
    }

    [Fact]
    public void Weapons_WithoutTheirOwnSound_UseTheGenericOne()
    {
        var catalog = new SoundCatalog(new[] { "weapon_fire.wav", "weapon_fire_shotgun.wav", "weapon_throw.wav" });

        Assert.Equal("weapon_fire_shotgun.wav", catalog.Resolve(SfxIds.WeaponFireFor("shotgun"))![0]);
        Assert.Equal("weapon_fire.wav", catalog.Resolve(SfxIds.WeaponFireFor("smg"))![0]);
        Assert.Equal("weapon_throw.wav", catalog.Resolve(SfxIds.WeaponThrowFor("grenade"))![0]);
    }

    [Fact]
    public void Limiter_CapsCopiesPlayingAtOnce_AndFreesThemWhenTheyEnd()
    {
        var limiter = new VoiceLimiter(maxVoices: 2, minGapSeconds: 0);

        Assert.True(limiter.TryStart("shot", 0.0, 0.5));
        Assert.True(limiter.TryStart("shot", 0.1, 0.5));
        Assert.False(limiter.TryStart("shot", 0.2, 0.5));
        Assert.True(limiter.TryStart("other", 0.2, 0.5));

        Assert.True(limiter.TryStart("shot", 0.55, 0.5));
    }

    [Fact]
    public void Limiter_DropsTheSameSound_StartedTooSoonAfterItself()
    {
        var limiter = new VoiceLimiter(maxVoices: 8, minGapSeconds: 0.03);

        Assert.True(limiter.TryStart("shot", 1.0, 0.2));
        Assert.False(limiter.TryStart("shot", 1.01, 0.2));
        Assert.True(limiter.TryStart("shot", 1.05, 0.2));
    }

    [Fact]
    public void ShippedGame_HasSensibleMixingRules()
    {
        var audio = DataLoader.LoadAll(DataLoader.DefaultDirectory).Game.Audio;

        Assert.True(audio.MaxVoicesPerSound >= 1);
        Assert.InRange(audio.PitchVariation, 0f, 0.2f);
    }

    [Fact]
    public void MissingAudioFolder_LeavesTheGameSilent_WithoutErrors()
    {
        var manager = new MonoGameAudioManager(Path.Combine(Path.GetTempPath(), "fourgate-no-audio-" + Guid.NewGuid()), new AudioConfig());

        manager.Preload();
        manager.PlaySfx(SfxIds.Explosion);

        Assert.Empty(manager.Problems);
    }
}
