using Fourgate.Core.Audio;
using Fourgate.Core.Data;
using Fourgate.Weapons;
using Microsoft.Xna.Framework;

namespace Fourgate.Tests;

public class WeaponTests
{
    private const float Step = 1f / 60f;

    private readonly FakeInput _input = new();
    private readonly FakeShotHandler _shots = new();

    [Fact]
    public void FirePress_FiresOneShotAndUsesOneRound()
    {
        var weapon = Create(TestData.Pistol());

        Press(weapon);

        Assert.Equal(1, _shots.Count);
        Assert.Equal(11, weapon.Magazine);
        Assert.Equal(15f, _shots.LastDamage);
    }

    [Fact]
    public void SemiAutomatic_HoldingFire_ShootsOnlyOnce()
    {
        var weapon = Create(TestData.Pistol());

        _input.FirePressed = true;
        _input.FireHeld = true;
        Run(weapon, 1);
        _input.FirePressed = false;
        Run(weapon, 120);

        Assert.Equal(1, _shots.Count);
    }

    [Fact]
    public void Automatic_HoldingFire_ShootsAtTheFireRate()
    {
        var weapon = Create(TestData.Pistol(automatic: true, magSize: 100));

        _input.FireHeld = true;
        Run(weapon, 120);

        // 3 shots per second for 2 seconds: shots at 0, 1/3, 2/3, 1, 4/3 and 5/3 s.
        Assert.Equal(6, _shots.Count);
    }

    [Fact]
    public void SemiAutomatic_ClickDuringCooldown_FiresWhenReady()
    {
        var weapon = Create(TestData.Pistol());
        Press(weapon);

        // Second click 5 frames later, still held: the shot waits for the cooldown.
        Run(weapon, 4);
        _input.FirePressed = true;
        _input.FireHeld = true;
        Run(weapon, 1);
        _input.FirePressed = false;
        Assert.Equal(1, _shots.Count);

        Run(weapon, 20);
        Assert.Equal(2, _shots.Count);
    }

    [Fact]
    public void EmptyMagazine_ReloadsAutomatically()
    {
        var weapon = Create(TestData.Pistol(magSize: 1));

        Press(weapon);
        Assert.Equal(0, weapon.Magazine);
        Assert.True(weapon.IsReloading);

        Run(weapon, 60);
        Assert.False(weapon.IsReloading);
        Assert.Equal(1, weapon.Magazine);
    }

    [Fact]
    public void Reload_TakesTheConfiguredTime()
    {
        var weapon = Create(TestData.Pistol(magSize: 1));
        Press(weapon);

        // 0.8 s is 48 frames; the reload began on the firing frame.
        Run(weapon, 46);
        Assert.True(weapon.IsReloading);
        Assert.InRange(weapon.ReloadProgress, 0.9f, 1f);

        Run(weapon, 2);
        Assert.False(weapon.IsReloading);
    }

    [Fact]
    public void GlobalReloadMultiplier_ScalesTheReloadTime()
    {
        var weapon = Create(TestData.Pistol(magSize: 1), TestData.Game(reloadMultiplier: 0.5f));
        Press(weapon);

        Run(weapon, 26);

        Assert.False(weapon.IsReloading);
    }

    [Fact]
    public void CannotFireWhileReloading()
    {
        var weapon = Create(TestData.Pistol(magSize: 1));
        Press(weapon);

        Run(weapon, 25);
        Press(weapon);

        Assert.Equal(1, _shots.Count);
    }

    [Fact]
    public void ManualReload_RefillsAPartlyUsedMagazine()
    {
        var weapon = Create(TestData.Pistol());
        Press(weapon);

        _input.ReloadPressed = true;
        Run(weapon, 1);
        _input.ReloadPressed = false;
        Assert.True(weapon.IsReloading);

        Run(weapon, 60);
        Assert.Equal(12, weapon.Magazine);
    }

    [Fact]
    public void ManualReload_WithAFullMagazine_DoesNothing()
    {
        var weapon = Create(TestData.Pistol());

        _input.ReloadPressed = true;
        Run(weapon, 1);

        Assert.False(weapon.IsReloading);
    }

    [Fact]
    public void LimitedReserve_ReloadTakesOnlyWhatIsLeft()
    {
        var weapon = Create(TestData.Pistol(magSize: 2, reserveMax: 1, infiniteReserve: false));

        Press(weapon);
        Run(weapon, 30);
        Press(weapon);
        Run(weapon, 60);

        Assert.Equal(1, weapon.Magazine);
        Assert.Equal(0, weapon.Reserve);
    }

    [Fact]
    public void LimitedReserve_WhenEverythingIsEmpty_CannotFireOrReload()
    {
        var weapon = Create(TestData.Pistol(magSize: 1, reserveMax: 0, infiniteReserve: false));
        Press(weapon);

        Run(weapon, 30);
        Press(weapon);

        Assert.Equal(1, _shots.Count);
        Assert.False(weapon.IsReloading);
    }

    [Fact]
    public void CancelReload_LosesTheProgress()
    {
        var weapon = Create(TestData.Pistol(magSize: 1));
        Press(weapon);
        Run(weapon, 20);

        weapon.CancelReload();

        Assert.False(weapon.IsReloading);
        Assert.Equal(0, weapon.Magazine);
    }

    [Fact]
    public void ReloadsDisabled_FiresWithoutEverReloading()
    {
        var weapon = Create(TestData.Pistol(automatic: true, magSize: 1), TestData.Game(reloadsEnabled: false));

        _input.FireHeld = true;
        Run(weapon, 120);

        Assert.Equal(6, _shots.Count);
        Assert.False(weapon.IsReloading);
    }

    [Fact]
    public void ReloadsDisabled_LimitedAmmo_DrainsTheReserve()
    {
        var weapon = Create(
            TestData.Pistol(automatic: true, reserveMax: 2, infiniteReserve: false),
            TestData.Game(reloadsEnabled: false));

        _input.FireHeld = true;
        Run(weapon, 120);

        Assert.Equal(2, _shots.Count);
        Assert.Equal(0, weapon.Reserve);
    }

    [Fact]
    public void Shot_StartsAtTheMuzzleAlongTheAim()
    {
        var weapon = Create(TestData.Pistol());
        _input.FirePressed = true;
        _input.FireHeld = true;

        weapon.Update(Step, _input, new Vector2(100, 200), Vector2.UnitY, 0, _shots);

        Assert.Equal(new Vector2(100, 200), _shots.LastOrigin);
        Assert.Equal(Vector2.UnitY, _shots.LastDirection);
    }

    private static Weapon Create(WeaponConfig config, GameConfig? rules = null)
    {
        return new Weapon(config, rules ?? TestData.Game(), new NullAudioManager());
    }

    /// <summary>Clicks fire for one frame, then releases.</summary>
    private void Press(Weapon weapon)
    {
        _input.FirePressed = true;
        _input.FireHeld = true;
        Run(weapon, 1);
        _input.FirePressed = false;
        _input.FireHeld = false;
        Run(weapon, 1);
    }

    private void Run(Weapon weapon, int steps)
    {
        for (var i = 0; i < steps; i++)
        {
            weapon.Update(Step, _input, Vector2.Zero, Vector2.UnitX, 0, _shots);
        }
    }
}
