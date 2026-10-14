using Fourgate.Core.Audio;
using Fourgate.Core.Data;
using Fourgate.Core.Rng;
using Fourgate.Entities;
using Fourgate.Systems;
using Fourgate.Weapons;
using Microsoft.Xna.Framework;

namespace Fourgate.Tests;

public class RailgunTests
{
    private const float Step = 1f / 60f;

    private static readonly Arena TestArena = new(TestData.Arena());

    // One 260x260 pillar in the middle: x and y 1370-1630.
    private static readonly Arena PillarArena = new(new ArenaConfig
    {
        X = 0, Y = 0, Size = 3000, DoorWidth = 420, WallThickness = 24,
        Pillars = new List<PillarConfig> { new() { CenterX = 1500, CenterY = 1500, Width = 260, Height = 260 } },
    });

    [Fact]
    public void Beam_HitsEveryTargetAlongTheLine_AtOnce()
    {
        var shots = new ShotSystem(TestArena);
        var near = AddTarget(shots, 600, 1000);
        var middle = AddTarget(shots, 1200, 1000);
        var far = AddTarget(shots, 2000, 1000);
        var beside = AddTarget(shots, 1200, 1100);

        shots.FireBeam(Beam(new Vector2(300, 1000), Vector2.UnitX));

        Assert.Equal(1, near.HitCount);
        Assert.Equal(1, middle.HitCount);
        Assert.Equal(1, far.HitCount);
        Assert.Equal(0, beside.HitCount);
        Assert.Equal(1, shots.BeamCount);
    }

    [Fact]
    public void Beam_StopsAtAPillar()
    {
        var shots = new ShotSystem(PillarArena);
        var before = AddTarget(shots, 1200, 1500);
        var behind = AddTarget(shots, 1800, 1500);

        shots.FireBeam(Beam(new Vector2(1000, 1500), Vector2.UnitX));

        Assert.Equal(1, before.HitCount);
        Assert.Equal(0, behind.HitCount);
    }

    [Fact]
    public void Beam_DoesNotReachPastItsRange()
    {
        var shots = new ShotSystem(TestArena);
        var outOfReach = AddTarget(shots, 1500, 1000);

        shots.FireBeam(Beam(new Vector2(300, 1000), Vector2.UnitX) with { Range = 1000 });

        Assert.Equal(0, outOfReach.HitCount);
    }

    [Fact]
    public void Beam_FadesOutAfterAMoment()
    {
        var shots = new ShotSystem(TestArena);
        shots.FireBeam(Beam(new Vector2(300, 1000), Vector2.UnitX));

        for (var i = 0; i < 60; i++)
        {
            shots.Update(Step);
        }

        Assert.Equal(0, shots.BeamCount);
    }

    [Fact]
    public void Beam_WithAnEndBlast_ExplodesWhereItsRangeEnds()
    {
        var explosives = new ExplosiveSystem(
            TestArena, TestData.Game().FriendlyFire, new GameRandom(1), new NullAudioManager(),
            new HazardSystem(TestData.Game().FriendlyFire));
        var shots = new ShotSystem(TestArena) { Explosives = explosives };
        var atTheEnd = new PracticeTarget(new Vector2(2750, 1000), 20);
        explosives.AddTarget(atTheEnd);

        shots.FireBeam(Beam(new Vector2(300, 1000), Vector2.UnitX) with { EndBlastRadius = 130, EndBlastDamage = 100 });
        for (var i = 0; i < 5; i++)
        {
            explosives.Update(Step, new List<Player>());
        }

        Assert.Equal(1, atTheEnd.HitCount);
    }

    [Fact]
    public void ShippedRailgun_IsABeamGunOnKeyNine_WithDualBeamAndEndBlastAtLevelTen()
    {
        var config = DataLoader.LoadAll(DataLoader.DefaultDirectory).GetWeapon("railgun");
        var handler = new FakeShotHandler();

        var one = WeaponStats.Compute(config, 1);
        var ten = WeaponStats.Compute(config, 10);

        Assert.Equal(WeaponType.Beam, config.Type);
        Assert.Equal(WeaponCategory.Gun, config.Category);
        Assert.Equal(9, config.Slot);
        Assert.Equal(18, config.UnlockWave);
        Assert.False(one.DualWield);
        Assert.Equal(0f, one.EndBlastRadius);
        Assert.True(ten.DualWield);
        Assert.True(ten.EndBlastRadius > 0f);

        var weapon = new Weapon(config, TestData.Game(), new NullAudioManager());
        weapon.SetLevel(10);
        weapon.Update(Step, new WeaponTrigger(true, true, false, 500f), new Vector2(1000, 1000), Vector2.UnitX, 0, handler);

        Assert.Equal(2, handler.Beams.Count);
        Assert.NotEqual(handler.Beams[0].Origin, handler.Beams[1].Origin);
        Assert.Equal(handler.Beams[0].Direction, handler.Beams[1].Direction);
        Assert.True(handler.Beams[0].EndBlastDamage > 0f);
    }

    private static BeamShot Beam(Vector2 origin, Vector2 direction)
    {
        return new BeamShot(origin, direction, 200, 2400, 10, 0, 0);
    }

    private static PracticeTarget AddTarget(ShotSystem shots, float x, float y)
    {
        var target = new PracticeTarget(new Vector2(x, y), 20);
        shots.AddTarget(target);
        return target;
    }
}
