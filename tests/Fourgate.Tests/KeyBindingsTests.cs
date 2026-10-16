using Fourgate.Core.Input;
using Fourgate.Core.Persistence;
using Fourgate.UI;
using Microsoft.Xna.Framework.Input;

namespace Fourgate.Tests;

public sealed class KeyBindingsTests : IDisposable
{
    private readonly string _folder = Path.Combine(Path.GetTempPath(), "fourgate-keys-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_folder))
        {
            Directory.Delete(_folder, true);
        }
    }

    [Fact]
    public void Defaults_AreWasd_LeftShiftDash_AndNoKeyDoesTwoThings()
    {
        var keys = new KeyBindings();

        Assert.Equal(Keys.W, keys.MoveUp);
        Assert.Equal(Keys.LeftShift, keys.Dash);
        Assert.Equal(Keys.G, keys.Equipment);
        Assert.Equal(KeyBindings.Actions.Count, KeyBindings.Actions.Select(keys.Get).Distinct().Count());
    }

    [Fact]
    public void Assigning_AKeyInUse_SwapsTheTwoActions()
    {
        var keys = new KeyBindings();

        Assert.True(keys.Assign(BindableAction.Dash, Keys.R));

        Assert.Equal(Keys.R, keys.Dash);
        Assert.Equal(Keys.LeftShift, keys.Reload);
    }

    [Theory]
    [InlineData(Keys.Escape)]
    [InlineData(Keys.Enter)]
    [InlineData(Keys.D3)]
    [InlineData(Keys.F7)]
    [InlineData(Keys.None)]
    public void FixedKeys_CannotBeBound(Keys key)
    {
        var keys = new KeyBindings();

        Assert.False(keys.Assign(BindableAction.Dash, key));
        Assert.Equal(Keys.LeftShift, keys.Dash);
    }

    [Fact]
    public void Repair_PutsBrokenBindingsBackOnTheDefaults()
    {
        var doubled = new KeyBindings { Dash = Keys.W };
        doubled.Repair();
        Assert.Equal(Keys.LeftShift, doubled.Dash);

        var fixedKey = new KeyBindings { Reload = Keys.Escape };
        fixedKey.Repair();
        Assert.Equal(Keys.R, fixedKey.Reload);
    }

    [Fact]
    public void KeyNames_ReadWell()
    {
        Assert.Equal("LEFT SHIFT", KeyBindings.NameOf(Keys.LeftShift));
        Assert.Equal("G", KeyBindings.NameOf(Keys.G));
        Assert.Equal("SPACE", KeyBindings.NameOf(Keys.Space));
    }

    [Fact]
    public void Settings_SaveAndLoadTheKeys_AndOldFilesGetTheDefaults()
    {
        var path = Path.Combine(_folder, "settings.json");
        var store = new SettingsStore(path);
        var settings = new GameSettings();
        settings.Controls!.Assign(BindableAction.Dash, Keys.Space);

        store.Save(settings);
        Assert.Equal(Keys.Space, store.Load().Controls!.Dash);

        File.WriteAllText(path, "{ \"musicVolume\": 40 }");
        Assert.Equal(Keys.LeftShift, store.Load().Controls!.Dash);
    }

    [Fact]
    public void ControlsMenu_LearnsTheNextKeyPressed_ForTheChosenAction()
    {
        var keys = new KeyBindings();
        var capture = new FakeKeyCapture(keys);
        var input = new FakeInput();
        var menu = new ControlsMenu(keys, 1920, 1080);
        menu.Open();

        // The first row is MOVE UP; Enter starts listening, the next key is learned.
        input.ConfirmPressed = true;
        menu.Update(input, capture, 1f / 60f);
        input.ConfirmPressed = false;
        capture.KeyPressed = Keys.Up;
        menu.Update(input, capture, 1f / 60f);

        Assert.Equal(Keys.Up, keys.MoveUp);
    }

    [Fact]
    public void ControlsMenu_EscWhileListening_KeepsTheOldKey_AndStaysOpen()
    {
        var keys = new KeyBindings();
        var capture = new FakeKeyCapture(keys);
        var input = new FakeInput();
        var menu = new ControlsMenu(keys, 1920, 1080);
        menu.Open();

        input.ConfirmPressed = true;
        menu.Update(input, capture, 1f / 60f);
        input.ConfirmPressed = false;
        capture.KeyPressed = Keys.Escape;
        input.BackPressed = true;
        menu.Update(input, capture, 1f / 60f);

        Assert.Equal(Keys.W, keys.MoveUp);
        Assert.True(menu.IsOpen);
    }

    private sealed class FakeKeyCapture : IKeyCapture
    {
        public FakeKeyCapture(KeyBindings bindings)
        {
            Bindings = bindings;
        }

        public Keys KeyPressed { get; set; }

        public KeyBindings Bindings { get; }
    }
}
