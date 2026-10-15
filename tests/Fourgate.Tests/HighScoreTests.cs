using Fourgate.Core.Audio;
using Fourgate.Core.Data;
using Fourgate.Core.Input;
using Fourgate.Core.Persistence;
using Fourgate.Scenes;
using Fourgate.Systems;
using Fourgate.UI;

namespace Fourgate.Tests;

public sealed class HighScoreTests : IDisposable
{
    private const float Step = 1f / 60f;

    private readonly string _folder = Path.Combine(Path.GetTempPath(), "fourgate-tests-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_folder))
        {
            Directory.Delete(_folder, recursive: true);
        }
    }

    [Fact]
    public void Table_KeepsTheBestTen_BestFirst_PerMode()
    {
        var table = new HighScoreTable();
        for (var score = 100; score <= 1200; score += 100)
        {
            table.Insert("normal", Entry(score));
        }

        var list = table.Get("normal");
        Assert.Equal(10, list.Count);
        Assert.Equal(1200, list[0].Score);
        Assert.Equal(300, list[^1].Score);
        Assert.Empty(table.Get("fast"));
    }

    [Fact]
    public void Table_PlacesAnEqualScoreBelow_AndRejectsWhatDoesNotFit()
    {
        var table = new HighScoreTable();
        for (var i = 0; i < 10; i++)
        {
            table.Insert("normal", Entry(500, "AAA"));
        }

        Assert.False(table.Qualifies("normal", 500));
        Assert.False(table.Qualifies("normal", 0));
        Assert.Equal(-1, table.Insert("normal", Entry(400)));
        Assert.Equal(0, table.Insert("normal", Entry(600, "NEW")));

        table.Restore("fast", new[] { Entry(300, "OLD") });
        Assert.Equal(1, table.Insert("fast", Entry(300, "TIE")));
    }

    [Fact]
    public void Store_SavesAndLoadsTheLists()
    {
        var store = new HighScoreStore(Path.Combine(_folder, "scores.json"));
        var table = new HighScoreTable();
        table.Insert("nightmare", Entry(4200, "ABC"));

        Assert.True(store.Save(table));
        var loaded = store.Load();

        Assert.Single(loaded.Get("nightmare"));
        Assert.Equal("ABC", loaded.Get("nightmare")[0].Initials);
        Assert.Equal(4200, loaded.Get("nightmare")[0].Score);
    }

    [Fact]
    public void Store_StartsEmpty_WhenThereIsNoFile()
    {
        var store = new HighScoreStore(Path.Combine(_folder, "missing", "scores.json"));

        Assert.Empty(store.Load().Get("normal"));
        Assert.Null(store.LastProblem);
    }

    [Fact]
    public void Store_SetsABrokenFileAside_AndStartsEmpty()
    {
        Directory.CreateDirectory(_folder);
        var path = Path.Combine(_folder, "scores.json");
        File.WriteAllText(path, "{ this is not json");
        var store = new HighScoreStore(path);

        var table = store.Load();

        Assert.Empty(table.Get("normal"));
        Assert.NotNull(store.LastProblem);
        Assert.True(File.Exists(path + ".bad"));
        Assert.False(File.Exists(path));
    }

    [Fact]
    public void Initials_AreChosenWithUpDownLeftRight()
    {
        var input = new FakeInput();
        var entry = new InitialsEntry();

        Press(entry, input, i => i.MenuUpPressed = true);
        Press(entry, input, i => i.MenuUpPressed = true);
        Press(entry, input, i => i.MenuRightPressed = true);
        Press(entry, input, i => i.MenuDownPressed = true);
        Press(entry, input, i => i.ConfirmPressed = true);

        Assert.True(entry.IsDone);
        Assert.Equal("CZA", entry.Initials);
    }

    [Fact]
    public void GameOver_WithAGoodScore_AsksForInitials_ThenSavesTheEntry()
    {
        var data = DataLoader.LoadAll(DataLoader.DefaultDirectory);
        var store = new HighScoreStore(Path.Combine(_folder, "scores.json"));
        var table = new HighScoreTable();
        var input = new FakeInput();
        var scene = new GameplayScene(
            data, new IPlayerInput[] { input }, new NullAudioManager(), new RunMode(true, false), table, store);

        for (var i = 0; i < 60 * 3; i++)
        {
            scene.Update(Step);
        }

        Die(scene);
        Assert.True(scene.IsEnteringInitials);

        // Confirming the initials saves them, and does not also leave the screen.
        input.ConfirmPressed = true;
        scene.Update(Step);
        Assert.False(scene.IsEnteringInitials);
        Assert.False(scene.RestartRequested);
        Assert.Single(table.Get("fast"));
        Assert.Equal("AAA", table.Get("fast")[0].Initials);
        Assert.Single(store.Load().Get("fast"));

        scene.Update(Step);
        Assert.True(scene.RestartRequested);
    }

    [Fact]
    public void GameOver_HighScore_CanBeLeftUnsaved()
    {
        var data = DataLoader.LoadAll(DataLoader.DefaultDirectory);
        var store = new HighScoreStore(Path.Combine(_folder, "scores.json"));
        var table = new HighScoreTable();
        var input = new FakeInput();
        var scene = new GameplayScene(
            data, new IPlayerInput[] { input }, new NullAudioManager(), new RunMode(true, false), table, store);
        for (var i = 0; i < 60 * 3; i++)
        {
            scene.Update(Step);
        }

        Die(scene);
        Assert.True(scene.IsEnteringInitials);

        input.BackPressed = true;
        scene.Update(Step);
        input.BackPressed = false;

        Assert.False(scene.IsEnteringInitials);
        Assert.False(scene.RestartRequested);
        Assert.Empty(table.Get("fast"));
        Assert.Empty(store.Load().Get("fast"));

        input.ConfirmPressed = true;
        scene.Update(Step);
        Assert.True(scene.RestartRequested);
    }

    [Fact]
    public void Death_ShowsYouDiedFirst_WhileTheWorldGoesOn_ThenTheResults()
    {
        var data = DataLoader.LoadAll(DataLoader.DefaultDirectory);
        var input = new FakeInput();
        var scene = new GameplayScene(
            data, new IPlayerInput[] { input }, new NullAudioManager(), new RunMode(true, false), new HighScoreTable(),
            new HighScoreStore(Path.Combine(_folder, "scores.json")));
        for (var i = 0; i < 60 * 3; i++)
        {
            scene.Update(Step);
        }

        scene.Players[0].TakeDamage(10000f);
        scene.Update(Step);
        Assert.True(scene.IsDying);
        Assert.False(scene.IsGameOver);

        // A second later it is still "YOU DIED"; the results come after.
        for (var i = 0; i < 60; i++)
        {
            scene.Update(Step);
        }

        Assert.True(scene.IsDying);
        for (var i = 0; i < 90; i++)
        {
            scene.Update(Step);
        }

        Assert.True(scene.IsGameOver);
        Assert.False(scene.IsDying);
    }

    // Kills the only player and waits through "YOU DIED" and the results fading in.
    private static void Die(GameplayScene scene)
    {
        scene.Players[0].TakeDamage(10000f);
        for (var i = 0; i < 60 * 4 && !scene.IsGameOver; i++)
        {
            scene.Update(Step);
        }

        Assert.True(scene.IsGameOver);
        for (var i = 0; i < 60; i++)
        {
            scene.Update(Step);
        }
    }

    private static void Press(InitialsEntry entry, FakeInput input, Action<FakeInput> set)
    {
        set(input);
        entry.Update(input);
        input.MenuUpPressed = false;
        input.MenuDownPressed = false;
        input.MenuLeftPressed = false;
        input.MenuRightPressed = false;
        input.ConfirmPressed = false;
    }

    private static HighScoreEntry Entry(int score, string initials = "XYZ")
    {
        return new HighScoreEntry
        {
            Score = score, Wave = 3, Kills = 10, Seconds = 60, Players = 1, Date = "2026-10-15", Initials = initials,
        };
    }
}
