using Fourgate.Core.Rng;
using Fourgate.Systems;
using Microsoft.Xna.Framework;

namespace Fourgate.Tests;

public class ParticleTests
{
    private const float Step = 1f / 60f;

    [Fact]
    public void Explosion_BurstsOut_ThenFadesAway()
    {
        var particles = new ParticleSystem(new GameRandom(1));

        particles.Explosion(new Vector2(500, 500), 160f);
        Assert.True(particles.Count > 30);

        for (var i = 0; i < 60 * 2; i++)
        {
            particles.Update(Step);
        }

        Assert.Equal(0, particles.Count);
    }

    [Fact]
    public void BiggerBlasts_ThrowMoreParticles()
    {
        var small = new ParticleSystem(new GameRandom(1));
        var big = new ParticleSystem(new GameRandom(1));

        small.Explosion(Vector2.Zero, 60f);
        big.Explosion(Vector2.Zero, 200f);

        Assert.True(big.Count > small.Count);
    }

    [Fact]
    public void FirePool_KeepsBurning_WhileItIsFed_AndBurnsLowerAsItFades()
    {
        var full = new ParticleSystem(new GameRandom(3));
        var fading = new ParticleSystem(new GameRandom(3));
        for (var i = 0; i < 60; i++)
        {
            full.FirePool(Vector2.Zero, 120f, 1f, Step);
            full.Update(Step);
            fading.FirePool(Vector2.Zero, 120f, 0.2f, Step);
            fading.Update(Step);
        }

        Assert.True(full.Count > 10);
        Assert.True(fading.Count < full.Count / 2);
    }

    [Fact]
    public void Buffer_NeverOverflows_UnderAFlood()
    {
        var particles = new ParticleSystem(new GameRandom(5));

        for (var i = 0; i < 400; i++)
        {
            particles.Explosion(Vector2.Zero, 400f);
        }

        Assert.InRange(particles.Count, 1, 4000);
    }

    [Fact]
    public void Flames_RiseOffTheFloor_AsTheyLive()
    {
        var particles = new ParticleSystem(new GameRandom(2));
        particles.FirePool(new Vector2(500, 500), 100f, 1f, 0.5f);
        for (var i = 0; i < 10; i++)
        {
            particles.Update(Step);
        }

        var drawn = new RecordingRenderer();
        particles.Draw(drawn);

        Assert.NotEmpty(drawn.Particles);
        Assert.All(drawn.Particles, p => Assert.True(p.Lift > 0f));
    }

    [Fact]
    public void HitPuffs_StartAtShotHeight_WithoutRisingYet()
    {
        var particles = new ParticleSystem(new GameRandom(2));
        particles.HitPuff(new Vector2(500, 500), Vector2.UnitX);

        var drawn = new RecordingRenderer();
        particles.Draw(drawn);

        Assert.Equal(5, drawn.Particles.Count);
        Assert.All(drawn.Particles, p => Assert.True(p.BaseHeight > 0f && p.Lift == 0f));
    }
}
