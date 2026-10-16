using Fourgate.Core.Audio;
using Fourgate.Core.Data;
using Fourgate.Core.Rng;
using Fourgate.Entities;
using Fourgate.Entities.Enemies;
using Fourgate.Systems;
using Fourgate.Weapons;
using Microsoft.Xna.Framework;

namespace Fourgate.Tests;

public class RocketTests
{
    private const float Step = 1f / 60f;

    private static readonly Arena TestArena = new(TestData.Arena());
    private static readonly Vector2 Muzzle = new(1500, 1500);
    private static readonly ProjectileConfig Rocket = new() { Speed = 1400, Radius = 8, Range = 1600 };
    private static readonly List<Player> Nobody = new();

    [Fact]
    public void Rocket_ExplodesOnTheFirstEnemy_AndTheBlastHitsItsNeighbours()
    {
        var world = new World();
        var struck = world.Spawn(Muzzle + new Vector2(600, 0));
        var beside = world.Spawn(Muzzle + new Vector2(600, 100));
        var outside = world.Spawn(Muzzle + new Vector2(600, 300));

        world.Fire(new RocketPayload(140));
        world.Run(1f);

        Assert.Equal(880f, struck.Hp, 0);
        Assert.Equal(880f, beside.Hp, 0);
        Assert.Equal(1000f, outside.Hp);
    }

    [Fact]
    public void Rocket_DoesNotHitDirectly_OnTopOfItsBlast()
    {
        var world = new World();
        var struck = world.Spawn(Muzzle + new Vector2(600, 0));

        world.Fire(new RocketPayload(140));
        world.Run(1f);

        // One blast only: 120, not 120 for the impact plus 120 for the blast.
        Assert.Equal(880f, struck.Hp, 0);
    }

    [Fact]
    public void Rocket_ExplodesAtAWall()
    {
        var world = new World();
        var nearWall = world.Spawn(new Vector2(2930, 1500));

        world.Fire(new RocketPayload(140), from: new Vector2(2500, 1450), direction: Vector2.UnitX);
        world.Run(1f);

        Assert.Equal(880f, nearWall.Hp, 0);
    }

    [Fact]
    public void Rocket_ExplodesAtTheEndOfItsRange()
    {
        var world = new World();
        var atRangeEnd = world.Spawn(Muzzle + new Vector2(1600, 90));

        world.Fire(new RocketPayload(140), from: Muzzle - new Vector2(1000, 0));
        world.Fire(new RocketPayload(140));
        world.Run(2f);

        Assert.Equal(880f, atRangeEnd.Hp, 0);
    }

    [Fact]
    public void RingBlast_FollowsTheMainBlast_ARoundTheImpact()
    {
        var world = new World();
        var struck = world.Spawn(Muzzle + new Vector2(600, 0));
        var payload = new RocketPayload(140, 6, 120, 0.4f, 0.5f, 0.15f);

        world.Fire(payload);
        world.Run(0.45f);
        Assert.Equal(880f, struck.Hp, 0);
        Assert.Equal(6, world.Explosives.QueuedBlastCount);

        world.Run(0.2f);
        Assert.Equal(0, world.Explosives.QueuedBlastCount);

        // The ring lies 120 from the struck enemy and each ring blast reaches 70, so none of
        // them reach its center; the ring hits what stands around it instead.
        Assert.Equal(880f, struck.Hp, 0);
    }

    [Fact]
    public void RingBlasts_GoOffOneByOne()
    {
        var world = new World();
        world.Spawn(Muzzle + new Vector2(600, 0));

        world.Fire(new RocketPayload(140, 6, 120, 0.4f, 0.5f, 0.15f, 0.07f));

        // The rocket arrives after about 0.4 s; the first ring blast 0.15 s later.
        world.Run(0.45f);
        Assert.Equal(6, world.Explosives.QueuedBlastCount);

        var remaining = new List<int>();
        for (var i = 0; i < 40; i++)
        {
            world.Run(Step);
            remaining.Add(world.Explosives.QueuedBlastCount);
        }

        // Every count from 6 down to 0 shows up, so no two blasts went off on the same frame.
        for (var count = 5; count >= 0; count--)
        {
            Assert.Contains(count, remaining);
        }
    }

    [Fact]
    public void RingBlasts_DoNotAlwaysGoAroundInOrder()
    {
        // Over a few rockets with different seeds, the firing order must not always follow the ring.
        var orders = new HashSet<string>();
        for (var seed = 1; seed <= 6; seed++)
        {
            var explosives = new ExplosiveSystem(
                TestArena, TestData.Game().FriendlyFire, new GameRandom(seed), new NullAudioManager());
            // Wide targets all round the ring, since the ring itself starts at a random angle.
            var targets = new List<PracticeTarget>();
            for (var i = 0; i < 12; i++)
            {
                var angle = MathF.Tau * i / 12f;
                var target = new PracticeTarget(new Vector2(2000, 2000) + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * 400, 110);
                targets.Add(target);
                explosives.AddTarget(target);
            }

            explosives.QueueRocketBlast(new Vector2(2000, 2000), 100, new RocketPayload(10, 6, 400, 0.5f, 1f, 0f, 0.1f), 0);
            var order = new List<int>();
            for (var frame = 0; frame < 60; frame++)
            {
                explosives.Update(Step, Nobody);
                for (var i = 0; i < targets.Count; i++)
                {
                    if (targets[i].HitCount > 0 && !order.Contains(i))
                    {
                        order.Add(i);
                    }
                }
            }

            orders.Add(string.Join(",", order));
        }

        Assert.True(orders.Count > 1);
    }

    [Fact]
    public void RingBlast_HitsEnemiesAroundTheImpact()
    {
        var world = new World();
        world.Spawn(Muzzle + new Vector2(600, 0));
        // Only the far side of the impact, so none of them stands in the rocket's flight path.
        var ring = new List<Enemy>();
        for (var i = -3; i <= 3; i++)
        {
            var angle = MathF.Tau * i / 12f;
            ring.Add(world.Spawn(Muzzle + new Vector2(600, 0) + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * 230));
        }

        world.Fire(new RocketPayload(140, 6, 200, 0.4f, 0.5f, 0.15f));
        world.Run(1f);

        // Out of reach of the main blast, but some of them catch a ring blast (48 damage).
        Assert.Contains(ring, enemy => enemy.Hp < 1000f);
        Assert.All(ring, enemy => Assert.True(enemy.Hp >= 1000f - 48f * 2f - 0.01f));
    }

    [Fact]
    public void Rocket_HurtsTheShooter_ByTheSelfShare()
    {
        var world = new World();
        world.Spawn(Muzzle + new Vector2(80, 0));
        var shooter = new Player(0, new FakeInput(), TestData.Game().Player, new NullAudioManager(), Muzzle - new Vector2(20, 0));

        world.Fire(new RocketPayload(140));
        world.Run(0.5f, new List<Player> { shooter });

        Assert.Equal(40f, shooter.Hp);
    }

    [Fact]
    public void Weapon_FiresARocket_WithItsPayload()
    {
        var config = DataLoader.LoadAll(DataLoader.DefaultDirectory).GetWeapon("rocket");
        var weapon = new Weapon(config, TestData.Game(), new NullAudioManager());
        var shots = new FakeShotHandler();

        weapon.Update(Step, new FakeInput { FirePressed = true, FireHeld = true }, Muzzle, Vector2.UnitX, 0, shots);

        Assert.NotNull(shots.LastRocket);
        Assert.Equal(config.Explosion!.Radius, shots.LastRocket!.Value.BlastRadius);
        Assert.Equal(0, shots.LastRocket.Value.RingCount);
        Assert.True(weapon.IsReloading);
    }

    [Fact]
    public void ShippedRocketLauncher_GetsTheRingAtLevelTen()
    {
        var config = DataLoader.LoadAll(DataLoader.DefaultDirectory).GetWeapon("rocket");

        Assert.Equal(8, config.Slot);
        Assert.Equal(14, config.UnlockWave);
        Assert.Equal(0, WeaponStats.Compute(config, 9).RingCount);
        Assert.Equal(6, WeaponStats.Compute(config, 10).RingCount);
    }

    private sealed class World
    {
        private readonly EnemySystem _enemies = new(new NullAudioManager());

        public World()
        {
            Explosives = new ExplosiveSystem(
                TestArena, TestData.Game().FriendlyFire, new GameRandom(3), new NullAudioManager());
            Explosives.AddSource(_enemies);
            Shots = new ShotSystem(TestArena) { Explosives = Explosives };
            Shots.AddSource(_enemies);
        }

        public ExplosiveSystem Explosives { get; }

        public ShotSystem Shots { get; }

        public Enemy Spawn(Vector2 position)
        {
            var zombie = TestData.Zombie();
            var config = new EnemyConfig
            {
                Id = zombie.Id,
                DisplayName = zombie.DisplayName,
                Hp = 1000,
                Speed = zombie.Speed,
                Radius = zombie.Radius,
                ContactDamage = zombie.ContactDamage,
                ContactCooldown = zombie.ContactCooldown,
                Xp = zombie.Xp,
                ScoreValue = zombie.ScoreValue,
                Cost = zombie.Cost,
                UnlockWave = zombie.UnlockWave,
                DropChance = zombie.DropChance,
            };
            var enemy = _enemies.Spawn(config, position);
            _enemies.Update(Step, Nobody, TestArena);
            return enemy;
        }

        public void Fire(RocketPayload payload, Vector2? from = null, Vector2? direction = null)
        {
            Shots.FireProjectile(from ?? Muzzle, direction ?? Vector2.UnitX, Rocket, 120, 0, rocket: payload);
        }

        public void Run(float seconds, List<Player>? players = null)
        {
            var everyone = players ?? Nobody;
            var steps = (int)MathF.Round(seconds / Step);
            for (var i = 0; i < steps; i++)
            {
                _enemies.Update(Step, Nobody, TestArena);
                Shots.Update(Step);
                Explosives.Update(Step, everyone);
            }
        }
    }

    [Fact]
    public void FlyingRocket_LeavesASmokeTrail()
    {
        var particles = new ParticleSystem(new GameRandom(1));
        var shots = new ShotSystem(new Arena(TestData.Arena())) { Particles = particles };
        var rocket = new ProjectileConfig { Speed = 1400, Radius = 8, Model = "rocket" };

        shots.FireProjectile(new Vector2(500, 1500), Vector2.UnitX, rocket, 100, 0, rocket: new RocketPayload(170));
        for (var i = 0; i < 20; i++)
        {
            shots.Update(1f / 60f);
        }

        Assert.Equal(1, shots.BulletCount);
        Assert.True(particles.Count > 10);
    }

}
