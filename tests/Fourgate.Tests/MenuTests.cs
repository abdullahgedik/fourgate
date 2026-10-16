using Fourgate.Core.Persistence;
using Fourgate.Core.Rendering;
using Fourgate.Scenes;
using Microsoft.Xna.Framework;

namespace Fourgate.Tests;

public sealed class MenuTests : IDisposable
{
    private const float Step = 1f / 60f;

    private static readonly Point[] Resolutions = { new(1280, 720), new(1600, 900), new(1920, 1080) };

    private readonly string _folder = Path.Combine(Path.GetTempPath(), "fourgate-tests-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_folder))
        {
            Directory.Delete(_folder, recursive: true);
        }
    }

    [Theory]
    [InlineData(0, MainMenuChoice.Play)]
    [InlineData(1, MainMenuChoice.HighScores)]
    [InlineData(2, MainMenuChoice.Settings)]
    [InlineData(3, MainMenuChoice.Quit)]
    [InlineData(4, MainMenuChoice.Play)]
    public void MainMenu_PicksTheHighlightedButton(int downs, MainMenuChoice expected)
    {
        var input = new FakeInput();
        var menu = new MainMenuScene(input, TestData.Game());
        for (var i = 0; i < downs; i++)
        {
            Tap(menu, input, f => f.MenuDownPressed = true);
        }

        Tap(menu, input, f => f.ConfirmPressed = true);

        Assert.Equal(expected, menu.Choice);
    }

    [Fact]
    public void StartScreen_GoesBack_FromItsThirdRow_OrWithBack()
    {
        var input = new FakeInput();
        var viaRow = new ModeSelectScene(input, TestData.Game());
        Tap(viaRow, input, f => f.MenuDownPressed = true);
        Tap(viaRow, input, f => f.MenuDownPressed = true);
        Tap(viaRow, input, f => f.ConfirmPressed = true);
        Assert.True(viaRow.BackRequested);
        Assert.Null(viaRow.ChosenPlayers);

        var viaKey = new ModeSelectScene(input, TestData.Game());
        Tap(viaKey, input, f => f.BackPressed = true);
        Assert.True(viaKey.BackRequested);
    }

    [Fact]
    public void Settings_SwitchTheWindow_AndApplyItAtOnce()
    {
        var input = new FakeInput();
        var settings = new GameSettings();
        var applied = 0;
        var scene = new SettingsScene(input, TestData.Game(), settings, Resolutions, _ => applied++);

        Tap(scene, input, f => f.MenuRightPressed = true);

        Assert.Equal(WindowMode.BorderlessFullscreen, settings.WindowMode);
        Assert.Equal(1, applied);

        // The window size cannot change while borderless.
        Tap(scene, input, f => f.MenuDownPressed = true);
        Tap(scene, input, f => f.MenuRightPressed = true);
        Assert.Equal(1280, settings.ResolutionWidth);
        Assert.Equal(1, applied);
    }

    [Fact]
    public void Settings_CycleTheWindowSize_WhileWindowed()
    {
        var input = new FakeInput();
        var settings = new GameSettings();
        var scene = new SettingsScene(input, TestData.Game(), settings, Resolutions, _ => { });

        Tap(scene, input, f => f.MenuDownPressed = true);
        Tap(scene, input, f => f.MenuRightPressed = true);
        Assert.Equal(1600, settings.ResolutionWidth);

        Tap(scene, input, f => f.MenuLeftPressed = true);
        Tap(scene, input, f => f.MenuLeftPressed = true);
        Assert.Equal(1920, settings.ResolutionWidth);
    }

    [Fact]
    public void Settings_StepTheVolumesByTen_WrappingRound()
    {
        var input = new FakeInput();
        var settings = new GameSettings { MasterVolume = 100, MusicVolume = 70, SfxVolume = 0 };
        var scene = new SettingsScene(input, TestData.Game(), settings, Resolutions, _ => { });

        // Window, size, screen shake, then the volumes.
        Tap(scene, input, f => f.MenuDownPressed = true);
        Tap(scene, input, f => f.MenuDownPressed = true);
        Tap(scene, input, f => f.ConfirmPressed = true);
        Assert.False(settings.ScreenShake);

        Tap(scene, input, f => f.MenuDownPressed = true);
        Tap(scene, input, f => f.MenuRightPressed = true);
        Assert.Equal(0, settings.MasterVolume);

        Tap(scene, input, f => f.MenuDownPressed = true);
        Tap(scene, input, f => f.MenuLeftPressed = true);
        Assert.Equal(60, settings.MusicVolume);

        Tap(scene, input, f => f.MenuDownPressed = true);
        Tap(scene, input, f => f.ConfirmPressed = true);
        Assert.Equal(10, settings.SfxVolume);

        // Past CONTROLS (greyed out without a keyboard to learn keys from) to BACK.
        Tap(scene, input, f => f.MenuDownPressed = true);
        Tap(scene, input, f => f.MenuDownPressed = true);
        Tap(scene, input, f => f.ConfirmPressed = true);
        Assert.True(scene.IsDone);
    }

    [Fact]
    public void SettingsStore_SavesLoads_AndClampsHandEditedValues()
    {
        var path = Path.Combine(_folder, "settings.json");
        var store = new SettingsStore(path);
        Assert.Equal(WindowMode.Windowed, store.Load().WindowMode);

        store.Save(new GameSettings { WindowMode = WindowMode.BorderlessFullscreen, MusicVolume = 30 });
        var loaded = store.Load();
        Assert.Equal(WindowMode.BorderlessFullscreen, loaded.WindowMode);
        Assert.Equal(30, loaded.MusicVolume);

        File.WriteAllText(path, "{ \"musicVolume\": 400, \"sfxVolume\": -5 }");
        var clamped = store.Load();
        Assert.Equal(100, clamped.MusicVolume);
        Assert.Equal(0, clamped.SfxVolume);

    }

    [Fact]
    public void SettingsStore_SetsABrokenFileAside_AndUsesTheDefaults()
    {
        Directory.CreateDirectory(_folder);
        var path = Path.Combine(_folder, "settings.json");
        File.WriteAllText(path, "not json at all");
        var store = new SettingsStore(path);

        var settings = store.Load();

        Assert.Equal(new GameSettings().MasterVolume, settings.MasterVolume);
        Assert.True(File.Exists(path + ".bad"));
        Assert.NotNull(store.LastProblem);
    }

    private static void Tap(IScene scene, FakeInput input, Action<FakeInput> press)
    {
        press(input);
        scene.Update(Step);
        input.MenuUpPressed = false;
        input.MenuDownPressed = false;
        input.MenuLeftPressed = false;
        input.MenuRightPressed = false;
        input.ConfirmPressed = false;
        input.BackPressed = false;
    }
}
