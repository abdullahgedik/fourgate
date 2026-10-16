using Fourgate.Core.Audio;
using Fourgate.Core.Data;
using Fourgate.Core.Rng;
using Fourgate.Entities;
using Fourgate.Entities.Enemies;
using Fourgate.Systems;
using Fourgate.Weapons;
using Microsoft.Xna.Framework;

namespace Fourgate.Tests;

public class FlamethrowerTests
{
    private const float Step = 1f / 60f;

    private static readonly Arena TestArena = new(TestData.Arena());
    private static readonly Vector2 Nozzle = new(1500, 1500);
    private static readonly List<Player> Nobody = new();

    [Fact]
    public void HoldingFire_SpraysFlames_AndUsesFuel()
    {
        var flamer = CreateFlamethrower(level: 1);
        var shots = new FakeShotHandler();
        var input = new FakeInput { FireHeld = true };

        Run(flamer, input, shots, 60);

        // 30 flames per second, one fuel each.
        Assert.Equal(30, shots.Flames.Count);
        Assert.Equal(flamer.ReserveMax - 30, flamer.Reserve);
        Assert.False(flamer.UsesMagazine);
    }

    [Fact]
    public void FlamesTogether_DealTheWeaponsDamagePerSecond()
    {
        var flamer = CreateFlamethrower(level: 1);
        var shots = new FakeShotHandler();

        Run(flamer, new FakeInput { FireHeld = true }, shots, 60);

        Assert.Equal(flamer.Stats.Damage, shots.Flames.Sum(f => f.Damage), 1);
    }

    [Fact]
    public void Flame_StopsAtTheFirstEnemy_WithoutPassThrough()
    {
        var world = new World();
        var front = world.Spawn(Nozzle + new Vector2(100, 0));
        var back = world.Spawn(Nozzle + new Vector2(170, 0));

        world.Flames.Emit(Flame(damage: 10, passThrough: false));
        world.Run(0.5f);

        Assert.Equal(990f, front.Hp, 3);
        Assert.Equal(1000f, back.Hp);
    }

    [Fact]
    public void Flame_BurnsEveryEnemyOnItsWay_WithPassThrough()
    {
        var world = new World();
        var front = world.Spawn(Nozzle + new Vector2(100, 0));
        var back = world.Spawn(Nozzle + new Vector2(170, 0));

        world.Flames.Emit(Flame(damage: 10, passThrough: true));
        world.Run(0.5f);

        Assert.Equal(990f, front.Hp, 3);
        Assert.Equal(990f, back.Hp, 3);
    }

    [Fact]
    public void Flame_BurnsEachEnemyOnlyOnce()
    {
        var world = new World();
        var enemy = world.Spawn(Nozzle + new Vector2(60, 0));

        world.Flames.Emit(Flame(damage: 10, passThrough: true));
        world.Run(0.5f);

        Assert.Equal(990f, enemy.Hp, 3);
    }

    [Fact]
    public void Flame_DiesOutAtItsRange()
    {
        var world = new World();
        var beyond = world.Spawn(Nozzle + new Vector2(300, 0));

        world.Flames.Emit(Flame(damage: 10, passThrough: true));
        world.Run(1f);

        Assert.Equal(1000f, beyond.Hp);
        Assert.Equal(0, world.Flames.Count);
    }

    [Fact]
    public void FlameDamage_DoesNotFlashLikeABulletHit()
    {
        var world = new World();
        var enemy = world.Spawn(Nozzle + new Vector2(60, 0));

        world.Flames.Emit(Flame(damage: 10, passThrough: false));
        world.Run(0.2f);

        Assert.False(enemy.ConsumeHit());
    }

    [Fact]
    public void FireTrail_LeavesPoolsAlongTheWholePath()
    {
        var world = new World();

        // 220 units of path with a pool every 80: at 80 and 160.
        world.Flames.Emit(Flame(damage: 10, passThrough: true) with
        {
            TrailRadius = 40, TrailSeconds = 2, TrailDamagePerSecond = 12, TrailSpacing = 80,
        });
        world.Run(0.5f);

        Assert.Equal(2, world.Hazards.FirePoolCount);
    }

    [Fact]
    public void FireTrail_NeverBurnsPlayers()
    {
        var world = new World();
        var player = new Player(0, new FakeInput(), TestData.Game().Player, new NullAudioManager(), Nozzle + new Vector2(80, 0));
        var partner = new Player(1, new FakeInput(), TestData.Game().Player, new NullAudioManager(), Nozzle + new Vector2(160, 0));
        var players = new List<Player> { player, partner };

        world.Flames.Emit(Flame(damage: 10, passThrough: true) with
        {
            TrailRadius = 40, TrailSeconds = 2, TrailDamagePerSecond = 1000, TrailSpacing = 80,
        });
        world.Run(0.5f);
        for (var i = 0; i < 60; i++)
        {
            world.Hazards.Update(Step, players);
        }

        Assert.Equal(100f, player.Hp);
        Assert.Equal(100f, partner.Hp);
    }

    [Fact]
    public void FireTrail_DoesNotStartOnTopOfTheNozzle()
    {
        var world = new World();
        var player = new Player(0, new FakeInput(), TestData.Game().Player, new NullAudioManager(), Nozzle - new Vector2(20, 0));

        world.Flames.Emit(Flame(damage: 10, passThrough: true) with
        {
            TrailRadius = 40, TrailSeconds = 2, TrailDamagePerSecond = 1000, TrailSpacing = 80,
        });
        world.Run(0.5f);
        for (var i = 0; i < 60; i++)
        {
            world.Hazards.Update(Step, new List<Player> { player });
        }

        Assert.Equal(100f, player.Hp);
    }

    [Fact]
    public void FireTrail_IsLeftOnlyEveryInterval()
    {
        var flamer = CreateFlamethrower(level: 10);
        var shots = new FakeShotHandler();

        Run(flamer, new FakeInput { FireHeld = true }, shots, 60);

        // About 30 flames in one second, a trail flame every quarter second.
        var trail = shots.Flames.Count(f => f.TrailRadius > 0f);
        Assert.InRange(trail, 4, 5);
    }

    [Fact]
    public void ShippedFlamethrower_PassesThroughFromLevelOne_AndAddsTheTrailAtLevelTen()
    {
        var config = DataLoader.LoadAll(DataLoader.DefaultDirectory).GetWeapon("flamethrower");

        var one = WeaponStats.Compute(config, 1);
        var nine = WeaponStats.Compute(config, 9);
        var ten = WeaponStats.Compute(config, 10);

        Assert.Equal(WeaponType.Stream, config.Type);
        Assert.Equal(7, config.Slot);
        Assert.True(one.PassThrough);
        Assert.Equal(nine.Range, ten.Range, 2);
        Assert.Equal(0f, nine.TrailInterval);
        Assert.True(ten.TrailInterval > 0f);
    }

    private static Weapon CreateFlamethrower(int level)
    {
        var config = DataLoader.LoadAll(DataLoader.DefaultDirectory).GetWeapon("flamethrower");
        var weapon = new Weapon(config, TestData.Game(), new NullAudioManager(), new GameRandom(1));
        weapon.SetLevel(level);
        return weapon;
    }

    private static FlameShot Flame(float damage, bool passThrough)
    {
        return new FlameShot(Nozzle, Vector2.UnitX, 650, 220, 8, 26, damage, 0, passThrough);
    }

    private static void Run(Weapon weapon, FakeInput input, FakeShotHandler shots, int steps)
    {
        for (var i = 0; i < steps; i++)
        {
            weapon.Update(Step, input, Nozzle, Vector2.UnitX, 0, shots);
        }
    }

    private sealed class World
    {
        private readonly EnemySystem _enemies = new(new NullAudioManager());

        public World()
        {
            Hazards = new HazardSystem(TestData.Game().FriendlyFire);
            Flames = new FlameSystem(TestArena, Hazards);
            Flames.AddSource(_enemies);
        }

        public HazardSystem Hazards { get; }

        public FlameSystem Flames { get; }

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

        public void Run(float seconds)
        {
            var steps = (int)MathF.Round(seconds / Step);
            for (var i = 0; i < steps; i++)
            {
                _enemies.Update(Step, Nobody, TestArena);
                Flames.Update(Step);
            }
        }
    }
}
