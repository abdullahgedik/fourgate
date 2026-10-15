using Fourgate.Core.Audio;
using Fourgate.Core.Data;
using Fourgate.Entities;
using Fourgate.Systems;
using Microsoft.Xna.Framework;

namespace Fourgate.Tests;

public class EnemySystemTests
{
    private const float Step = 1f / 60f;

    private static readonly Arena TestArena = new(TestData.Arena());
    private static readonly EnemyConfig Zombie = TestData.Zombie();

    [Fact]
    public void Enemy_WalksTowardThePlayer_AtItsSpeed()
    {
        var system = new EnemySystem(new NullAudioManager());
        var players = Players(new Vector2(1500, 1500));
        var enemy = system.Spawn(Zombie, new Vector2(500, 1500));

        Run(system, players, 60);

        Assert.Equal(610f, enemy.Position.X, 0);
        Assert.Equal(1500f, enemy.Position.Y, 0);
    }

    [Fact]
    public void Enemy_StopsWhenTouchingThePlayer()
    {
        var system = new EnemySystem(new NullAudioManager());
        var players = Players(new Vector2(1500, 1500));
        var enemy = system.Spawn(Zombie, new Vector2(1400, 1500));

        Run(system, players, 300);

        // Player radius 20 + zombie radius 20.
        Assert.Equal(40f, Vector2.Distance(enemy.Position, players[0].Position), 1);
    }

    [Fact]
    public void Enemy_ChasesTheNearestPlayer()
    {
        var system = new EnemySystem(new NullAudioManager());
        var players = Players(new Vector2(500, 1500), new Vector2(2500, 1500));
        var enemy = system.Spawn(Zombie, new Vector2(1800, 1500));

        Run(system, players, 60);

        Assert.True(enemy.Position.X > 1800);
    }

    [Fact]
    public void Enemies_SpawnedOnTheSameSpot_SpreadApart()
    {
        var system = new EnemySystem(new NullAudioManager());
        var players = Players(new Vector2(1500, 1500));
        for (var i = 0; i < 8; i++)
        {
            system.Spawn(Zombie, new Vector2(500, 500));
        }

        Run(system, players, 120);

        for (var a = 0; a < system.Count; a++)
        {
            for (var b = a + 1; b < system.Count; b++)
            {
                var distance = Vector2.Distance(system[a].Position, system[b].Position);
                Assert.True(distance > 30f, $"Enemies {a} and {b} are only {distance} apart.");
            }
        }
    }

    [Fact]
    public void Enemies_CrowdingThePlayer_DoNotOverlapIt()
    {
        var system = new EnemySystem(new NullAudioManager());
        var players = Players(new Vector2(1500, 1500));
        for (var i = 0; i < 12; i++)
        {
            system.Spawn(Zombie, new Vector2(1300 + i * 5, 1400 + i * 17));
        }

        Run(system, players, 300);

        for (var i = 0; i < system.Count; i++)
        {
            Assert.True(Vector2.Distance(system[i].Position, players[0].Position) > 39f);
        }
    }

    [Fact]
    public void Enemy_DiesWhenItsHpRunsOut_AndIsRemoved()
    {
        var system = new EnemySystem(new NullAudioManager());
        var players = Players(new Vector2(1500, 1500));
        var enemy = system.Spawn(Zombie, new Vector2(500, 1500));

        enemy.Hit(15, 0);
        enemy.Hit(15, 0);
        enemy.Hit(15, 0);
        Assert.True(enemy.IsAlive);

        enemy.Hit(15, 0);
        Assert.False(enemy.IsAlive);

        Run(system, players, 1);
        Assert.Equal(0, system.Count);
        Assert.Equal(1, system.KillCount);
    }

    [Fact]
    public void Bullets_KillAnEnemy_AfterEnoughHits()
    {
        var enemies = new EnemySystem(new NullAudioManager());
        var shots = new ShotSystem(TestArena);
        shots.AddSource(enemies);
        var players = Players(new Vector2(1500, 1500));
        enemies.Spawn(Zombie, new Vector2(2000, 1500));
        var projectile = new ProjectileConfig { Speed = 6000, Radius = 4, Range = 1100 };

        // 60 hp against 15 damage: four shots, one every 10 frames.
        for (var shot = 0; shot < 4; shot++)
        {
            Assert.Equal(1, enemies.Count);
            shots.FireProjectile(new Vector2(1540, 1500), Vector2.UnitX, projectile, 15, 0);
            for (var frame = 0; frame < 10; frame++)
            {
                enemies.Update(Step, players, TestArena);
                shots.Update(Step);
            }
        }

        Assert.Equal(0, enemies.Count);
        Assert.Equal(1, enemies.KillCount);
    }

    [Fact]
    public void Bullet_HitsTheFrontEnemyOfAGroup()
    {
        var enemies = new EnemySystem(new NullAudioManager());
        var shots = new ShotSystem(TestArena);
        shots.AddSource(enemies);
        var players = Players(new Vector2(1500, 1500));
        var front = enemies.Spawn(Zombie, new Vector2(1900, 1500));
        var back = enemies.Spawn(Zombie, new Vector2(2100, 1500));
        enemies.Update(Step, players, TestArena);

        shots.FireProjectile(
            new Vector2(1540, 1500), Vector2.UnitX, new ProjectileConfig { Speed = 6000, Radius = 4, Range = 1100 }, 15, 0);
        for (var frame = 0; frame < 10; frame++)
        {
            shots.Update(Step);
        }

        Assert.Equal(45f, front.Hp);
        Assert.Equal(60f, back.Hp);
    }

    private static List<Player> Players(params Vector2[] positions)
    {
        var players = new List<Player>();
        for (var i = 0; i < positions.Length; i++)
        {
            players.Add(new Player(i, new FakeInput(), TestData.Game().Player, new NullAudioManager(), positions[i]));
        }

        return players;
    }

    private static void Run(EnemySystem system, List<Player> players, int steps)
    {
        for (var i = 0; i < steps; i++)
        {
            system.Update(Step, players, TestArena);
        }
    }

    [Fact]
    public void WithNobodyLeft_ZombiesWander_OnlyWhenAskedTo()
    {
        var arena = new Arena(TestData.Arena());
        var nobody = new List<Player>();
        var still = new EnemySystem(new NullAudioManager());
        var wandering = new EnemySystem(new NullAudioManager()) { WanderWhenAlone = true };
        var a = still.Spawn(TestData.Zombie(), new Vector2(1500, 1500));
        var b = wandering.Spawn(TestData.Zombie(), new Vector2(1500, 1500));
        var c = wandering.Spawn(TestData.Zombie(), new Vector2(1500, 600));

        for (var i = 0; i < 60 * 8; i++)
        {
            still.Update(1f / 60f, nobody, arena);
            wandering.Update(1f / 60f, nobody, arena);
        }

        Assert.Equal(new Vector2(1500, 1500), a.Position);
        Assert.True(Vector2.Distance(b.Position, new Vector2(1500, 1500)) > 20f || Vector2.Distance(c.Position, new Vector2(1500, 600)) > 20f);
    }
}
