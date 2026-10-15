using Fourgate.Core.Data;
using Fourgate.Core.Rendering;
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
    public void BodyHits_StartWhereTheBodyWasStruck_WithoutRisingYet()
    {
        var particles = new ParticleSystem(new GameRandom(2));
        particles.BodyHit(HitEffect.Light, new Vector2(500, 500), Vector2.UnitX, 26f);

        var drawn = new RecordingRenderer();
        particles.Draw(drawn);

        Assert.Equal(6, drawn.Particles.Count);
        Assert.All(drawn.Particles, p => Assert.True(p.BaseHeight == 26f && p.Lift == 0f));
    }

    [Fact]
    public void BloodDrops_ArcUp_FallBack_AndLieOnTheFloorAsSplats_ThenFade()
    {
        var particles = new ParticleSystem(new GameRandom(4));
        particles.BodyHit(HitEffect.Heavy, new Vector2(500, 500), Vector2.UnitX, 26f);

        // Up first...
        Run(particles, 0.05f);
        var drawn = new RecordingRenderer();
        particles.Draw(drawn);
        Assert.Contains(drawn.Particles, p => p.Lift > 0f);

        // ...then on the floor, drawn flat in the floor pass and no longer in the air.
        Run(particles, 0.8f);
        var floor = new RecordingRenderer();
        particles.DrawFloor(floor);
        Assert.Equal(7, floor.Circles.Count);
        Assert.All(floor.Circles, c => Assert.True(c.X > 500f));
        var air = new RecordingRenderer();
        particles.Draw(air);
        Assert.Empty(air.Particles);

        Run(particles, 3f);
        Assert.Equal(0, particles.Count);
    }

    [Fact]
    public void HitEffects_GrowWithTheGun_AndTheRailgunBurnsWithoutBlood()
    {
        int Spawned(HitEffect effect)
        {
            var particles = new ParticleSystem(new GameRandom(1));
            particles.BodyHit(effect, Vector2.Zero, Vector2.UnitX, 26f);
            return particles.Count;
        }

        Assert.True(Spawned(HitEffect.Pellet) < Spawned(HitEffect.Light));
        Assert.True(Spawned(HitEffect.Light) < Spawned(HitEffect.Heavy));
        Assert.True(Spawned(HitEffect.Heavy) < Spawned(HitEffect.Sniper));

        var energy = new ParticleSystem(new GameRandom(1));
        energy.BodyHit(HitEffect.Energy, new Vector2(500, 500), Vector2.UnitX, 26f);
        Run(energy, 1f);
        var floor = new RecordingRenderer();
        energy.DrawFloor(floor);
        Assert.Empty(floor.Circles);
    }

    [Fact]
    public void ShippedGuns_EachHaveTheirOwnHitEffect()
    {
        var data = DataLoader.LoadAll(DataLoader.DefaultDirectory);

        Assert.Equal(HitEffect.Light, data.GetWeapon("pistol").Projectile!.HitEffect);
        Assert.Equal(HitEffect.Light, data.GetWeapon("smg").Projectile!.HitEffect);
        Assert.Equal(HitEffect.Pellet, data.GetWeapon("shotgun").Projectile!.HitEffect);
        Assert.Equal(HitEffect.Heavy, data.GetWeapon("marksman").Projectile!.HitEffect);
        Assert.Equal(HitEffect.Heavy, data.GetWeapon("assault").Projectile!.HitEffect);
        Assert.Equal(HitEffect.Sniper, data.GetWeapon("sniper").Projectile!.HitEffect);
        Assert.Equal(HitEffect.Energy, data.GetWeapon("railgun").Beam!.HitEffect);
    }

    [Fact]
    public void DoorSparks_GrowWithTheWarning_AndDriftIntoTheArena()
    {
        int After(float strength, out ParticleSystem particles)
        {
            particles = new ParticleSystem(new GameRandom(6));
            for (var i = 0; i < 30; i++)
            {
                particles.DoorSparks(
                    new Vector2(1300, 0), new Vector2(1700, 0), Vector2.UnitY, 90f, strength, Color.Yellow, Color.Red, Step);
            }

            return particles.Count;
        }

        Assert.Equal(0, After(0f, out _));
        Assert.True(After(1f, out var full) > After(0.3f, out _));

        Run(full, 0.3f);
        var drawn = new RecordingRenderer();
        full.Draw(drawn);
        Assert.All(drawn.Particles, p => Assert.True(p.Ground.Y > 0f && p.Ground.X > 1200f && p.Ground.X < 1800f));
    }

    [Fact]
    public void WithAStainLayer_LandedBlood_BecomesLastingStains_AndLeavesTheParticleBuffer()
    {
        var stains = new FloorStains(Vector2.Zero, 3000f);
        var particles = new ParticleSystem(new GameRandom(4)) { Stains = stains };
        particles.BodyHit(HitEffect.Heavy, new Vector2(500, 500), Vector2.UnitX, 26f);

        Run(particles, 1f);

        // Seven drops landed as stains; only the mist (already gone) and nothing else remains.
        Assert.Equal(7, stains.TotalAdded);
        Assert.Equal(7, stains.PendingCount);
        Assert.All(Enumerable.Range(0, stains.PendingCount), i => Assert.True(stains.GetPending(i).Position.X > 500f));
        Assert.Equal(0, particles.Count);

        stains.ClearPending();
        Assert.Equal(0, stains.PendingCount);
        Assert.Equal(7, stains.TotalAdded);
    }

    private static void Run(ParticleSystem particles, float seconds)
    {
        for (var i = 0; i < (int)MathF.Round(seconds / Step); i++)
        {
            particles.Update(Step);
        }
    }
}
