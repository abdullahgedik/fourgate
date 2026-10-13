using Fourgate.Core.Data;
using Fourgate.Entities;
using Fourgate.Systems;
using Microsoft.Xna.Framework;

namespace Fourgate.Tests;

public class ShotSystemTests
{
    private const float Step = 1f / 60f;

    private static readonly Arena TestArena = new(TestData.Arena());
    private static readonly Vector2 Muzzle = new(1500, 1500);

    [Fact]
    public void Bullet_DisappearsAfterItsRange()
    {
        var system = new ShotSystem(TestArena);
        Fire(system, Muzzle);

        // 1100 units at 6000 units/s is 11 frames, plus one frame shown at the end point.
        Run(system, 11);
        Assert.Equal(1, system.BulletCount);

        Run(system, 1);
        Assert.Equal(0, system.BulletCount);
    }

    [Fact]
    public void Bullet_DisappearsAtAWall()
    {
        var system = new ShotSystem(TestArena);
        Fire(system, new Vector2(2900, 1500));

        Run(system, 2);

        Assert.Equal(0, system.BulletCount);
    }

    [Fact]
    public void Bullet_HitsATargetOnItsPath_AndStopsThere()
    {
        var system = new ShotSystem(TestArena);
        var target = AddTarget(system, 2000, 1500);
        var behind = AddTarget(system, 2400, 1500);
        Fire(system, Muzzle);

        Run(system, 20);

        Assert.Equal(1, target.HitCount);
        Assert.Equal(0, behind.HitCount);
        Assert.Equal(0, system.BulletCount);
    }

    [Fact]
    public void Bullet_HitsOnlyWhenItArrives()
    {
        var system = new ShotSystem(TestArena);
        var target = AddTarget(system, 2500, 1500);
        Fire(system, Muzzle);

        // The target's edge is 976 units away: reached during the 10th frame, not before.
        Run(system, 9);
        Assert.Equal(0, target.HitCount);

        Run(system, 1);
        Assert.Equal(1, target.HitCount);
    }

    [Theory]
    [InlineData(6000)]
    [InlineData(20000)]
    [InlineData(200000)]
    public void Bullet_NeverSkipsOverATarget_HoweverFast(float speed)
    {
        var system = new ShotSystem(TestArena);
        var target = AddTarget(system, 2143, 1500);
        Fire(system, Muzzle, speed);

        Run(system, 20);

        Assert.Equal(1, target.HitCount);
    }

    [Fact]
    public void Bullet_HitsATargetTouchingTheMuzzle()
    {
        var system = new ShotSystem(TestArena);
        var target = AddTarget(system, 1510, 1500);
        Fire(system, Muzzle);

        Run(system, 1);

        Assert.Equal(1, target.HitCount);
    }

    [Theory]
    [InlineData(2000, 1530)] // beside the path, beyond target radius plus bullet radius
    [InlineData(1000, 1500)] // behind the shooter
    [InlineData(2700, 1500)] // beyond the range
    public void Bullet_MissesTargetsOffItsPath(float x, float y)
    {
        var system = new ShotSystem(TestArena);
        var target = AddTarget(system, x, y);
        Fire(system, Muzzle);

        Run(system, 20);

        Assert.Equal(0, target.HitCount);
    }

    [Fact]
    public void Bullet_GrazingATarget_Hits()
    {
        var system = new ShotSystem(TestArena);
        var target = AddTarget(system, 2000, 1522);
        Fire(system, Muzzle);

        Run(system, 20);

        Assert.Equal(1, target.HitCount);
    }

    [Fact]
    public void ManyBullets_AreAllRemoved()
    {
        var system = new ShotSystem(TestArena);
        for (var i = 0; i < 10; i++)
        {
            Fire(system, new Vector2(1500, 100 + i * 200));
        }

        Run(system, 30);

        Assert.Equal(0, system.BulletCount);
    }

    [Fact]
    public void PracticeTarget_FlashesWhenHit_ThenFades()
    {
        var target = new PracticeTarget(new Vector2(2000, 1500), 20);
        Assert.False(target.IsFlashing);

        target.Hit(15, 0);
        Assert.True(target.IsFlashing);

        for (var i = 0; i < 20; i++)
        {
            target.Update(Step);
        }

        Assert.False(target.IsFlashing);
    }

    [Fact]
    public void MovingPracticeTarget_PatrolsBetweenItsEndPoints()
    {
        var target = new PracticeTarget(new Vector2(1000, 1500), new Vector2(1300, 1500), 300, 20);

        for (var i = 0; i < 60; i++)
        {
            target.Update(Step);
        }

        Assert.Equal(1300f, target.Position.X, 1);

        for (var i = 0; i < 60; i++)
        {
            target.Update(Step);
        }

        Assert.Equal(1000f, target.Position.X, 1);
    }

    private static void Fire(ShotSystem system, Vector2 origin, float speed = 6000)
    {
        var projectile = new ProjectileConfig { Speed = speed, Radius = 4, Range = 1100 };
        system.FireProjectile(origin, Vector2.UnitX, projectile, 15, 0);
    }

    private static PracticeTarget AddTarget(ShotSystem system, float x, float y)
    {
        var target = new PracticeTarget(new Vector2(x, y), 20);
        system.AddTarget(target);
        return target;
    }

    private static void Run(ShotSystem system, int steps)
    {
        for (var i = 0; i < steps; i++)
        {
            system.Update(Step);
        }
    }
}
