using Fourgate.Core.Audio;
using Fourgate.Core.Data;
using Fourgate.Core.Input;
using Fourgate.Core.Persistence;
using Fourgate.Core.Rendering;
using Fourgate.Core.Rng;
using Fourgate.Entities;
using Fourgate.Scenes;
using Fourgate.Systems;
using Microsoft.Xna.Framework;

namespace Fourgate.Tests;

public class FeedbackTests
{
    private const float Step = 1f / 60f;

    [Fact]
    public void Shake_BuildsUp_MovesOnlyThePicture_AndSettles()
    {
        var camera = new Camera2D(new Rectangle(0, 0, 1920, 1080), TestData.Game().Camera);
        camera.SnapTo(new Vector2(1500, 1500), camera.ViewCenter);
        var before = camera.ScreenToWorld(new Vector2(100, 100));

        camera.AddTrauma(1f);
        camera.UpdateShake(Step, 1f, -1f);
        Assert.True(camera.ShakeOffset.Length() > 10f);
        Assert.Equal(before, camera.ScreenToWorld(new Vector2(100, 100)));

        for (var i = 0; i < 60; i++)
        {
            camera.UpdateShake(Step, 1f, 1f);
        }

        Assert.Equal(0f, camera.Trauma);
        Assert.Equal(Vector2.Zero, camera.ShakeOffset);
    }

    [Fact]
    public void BulletHit_LeavesAPuff_AndAWallHitLeavesDust()
    {
        var particles = new ParticleSystem(new GameRandom(1));
        var shots = new ShotSystem(new Arena(TestData.Arena())) { Particles = particles };
        shots.AddTarget(new PracticeTarget(new Vector2(1500, 1000), 20));

        shots.FireProjectile(new Vector2(1000, 1000), Vector2.UnitX, Projectile(), 10, 0);
        for (var i = 0; i < 10; i++)
        {
            shots.Update(Step);
        }

        Assert.Equal(5, particles.Count);

        shots.FireProjectile(new Vector2(2900, 500), Vector2.UnitX, Projectile(), 10, 0);
        for (var i = 0; i < 3; i++)
        {
            shots.Update(Step);
        }

        Assert.Equal(9, particles.Count);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Run_HurtShake_FollowsTheSetting(bool shakeOn)
    {
        var data = DataLoader.LoadAll(DataLoader.DefaultDirectory);
        var settings = new GameSettings { ScreenShake = shakeOn };
        var scene = new GameplayScene(
            data, new IPlayerInput[] { new FakeInput() }, new NullAudioManager(), settings: settings);
        scene.Update(Step);

        scene.Players[0].TakeDamage(10f);
        scene.Update(Step);

        Assert.Equal(shakeOn, scene.ViewTrauma(0) > 0f);

        // The red flash shows either way; it is not shake.
        Assert.True(scene.IsHurtFlashing(0));
    }

    [Fact]
    public void Run_SmallBurnTicks_DoNotShakeOrFlash()
    {
        var data = DataLoader.LoadAll(DataLoader.DefaultDirectory);
        var scene = new GameplayScene(
            data, new IPlayerInput[] { new FakeInput() }, new NullAudioManager(), settings: new GameSettings());
        scene.Update(Step);

        scene.Players[0].TakeDamage(0.2f, silent: true);
        scene.Update(Step);

        Assert.Equal(0f, scene.ViewTrauma(0));
        Assert.False(scene.IsHurtFlashing(0));
    }

    private static ProjectileConfig Projectile()
    {
        return new ProjectileConfig { Speed = 6000, Radius = 4, Range = 1100 };
    }
}
