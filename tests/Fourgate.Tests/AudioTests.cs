using Fourgate.Core.Audio;
using Fourgate.Core.Data;
using Fourgate.Core.Input;
using Fourgate.Core.Rng;
using Fourgate.Entities;
using Fourgate.Scenes;
using Fourgate.Systems;
using Fourgate.Weapons;
using Microsoft.Xna.Framework;

namespace Fourgate.Tests;

public class AudioTests
{
    private const float Step = 1f / 60f;

    [Fact]
    public void Log_KeepsTheLatestTen_NewestFirst()
    {
        var log = new AudioEventLog(new NullAudioManager());
        for (var i = 0; i < 12; i++)
        {
            log.PlaySfx("event_" + i);
        }

        var recent = log.Recent.ToList();
        Assert.Equal(10, recent.Count);
        Assert.Equal("event_11", recent[0]);
        Assert.Equal("event_2", recent[^1]);
        Assert.Equal(12, log.Total);
    }

    [Fact]
    public void EachWeapon_FiresWithItsOwnSound()
    {
        var log = new AudioEventLog(new NullAudioManager());
        var shotgun = new Weapon(DataLoader.LoadAll(DataLoader.DefaultDirectory).GetWeapon("shotgun"), TestData.Game(), log);

        shotgun.Update(Step, new WeaponTrigger(true, true, false, 500f), Vector2.Zero, Vector2.UnitX, 0, new FakeShotHandler());

        Assert.Contains("weapon_fire_shotgun", log.Recent);
    }

    [Fact]
    public void Menus_SoundOnMoveAndConfirm_AndPlayMenuMusic()
    {
        var log = new AudioEventLog(new NullAudioManager());
        var input = new FakeInput { MenuDownPressed = true };
        var menu = new MainMenuScene(input, TestData.Game(), MainMenuChoice.Play, log);
        menu.Update(Step);
        input.MenuDownPressed = false;
        input.ConfirmPressed = true;
        menu.Update(Step);

        Assert.Equal(new[] { SfxIds.UiConfirm, SfxIds.UiMove, MusicIds.Menu }, log.Recent.ToArray());
    }

    [Fact]
    public void Run_PlaysGameplayMusic_AndSoundsWhenPausing()
    {
        var log = new AudioEventLog(new NullAudioManager());
        var input = new FakeInput { PausePressed = true };
        var scene = new GameplayScene(
            DataLoader.LoadAll(DataLoader.DefaultDirectory), new IPlayerInput[] { input }, log);

        scene.Update(Step);

        Assert.Contains(MusicIds.Gameplay, log.Recent);
        Assert.Equal(SfxIds.Pause, log.Recent.First());
    }

    [Fact]
    public void Boss_AnnouncesItself_AndSwitchesTheMusic()
    {
        var data = DataLoader.LoadAll(DataLoader.DefaultDirectory);
        var log = new AudioEventLog(new NullAudioManager());
        var enemies = new EnemySystem(log);
        var bosses = new BossSystem(enemies, data, new GameRandom(1), log);
        enemies.Spawn(data.GetBossForWave(10)!.AsEnemy, new Vector2(1500, 1500));
        var player = new Player(0, new FakeInput(), data.Game.Player, log, new Vector2(1500, 1200));

        bosses.Update(Step, new List<Player> { player }, new Arena(data.Game.Arena));

        Assert.Contains(SfxIds.BossAppear, log.Recent);
        Assert.Contains(MusicIds.Boss, log.Recent);
    }
}
