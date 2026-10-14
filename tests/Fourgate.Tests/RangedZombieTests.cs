using Fourgate.Core.Audio;
using Fourgate.Core.Data;
using Fourgate.Core.Rendering;
using Fourgate.Entities;
using Fourgate.Entities.Enemies;
using Fourgate.Systems;
using Microsoft.Xna.Framework;

namespace Fourgate.Tests;

public class RangedZombieTests
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
    public void RangedZombie_StopsInRange_AndWindsUpBeforeThrowing()
    {
        var world = new World(TestArena);
        var player = world.AddPlayer(new Vector2(1500, 1000));
        var zombie = world.Spawn(new Vector2(1000, 1000));

        // Walks from 500 away to inside its 450 range, then stops and starts the wind-up.
        world.Run(1f);
        Assert.True(zombie.IsWindingUp);
        var stoppedAt = zombie.Position;
        Assert.InRange(Vector2.Distance(stoppedAt, player.Position), 400f, 450f);
        Assert.Equal(0, world.Fireballs.FireballCount);

        // Nothing is thrown until the full telegraph has played.
        world.Run(0.6f);
        Assert.Equal(1, world.Fireballs.FireballCount);
        Assert.Equal(stoppedAt, zombie.Position);
    }

    [Fact]
    public void Fireball_HurtsThePlayer_ItWasThrownAt()
    {
        var world = new World(TestArena);
        var player = world.AddPlayer(new Vector2(1440, 1000));
        world.Spawn(new Vector2(1000, 1000));

        world.Run(3f);

        Assert.True(player.Hp < 100f);
    }

    [Fact]
    public void RangedZombie_KeepsWalking_WhileThePlayerCannotSeeIt()
    {
        var world = new World(TestArena);
        var player = world.AddPlayer(new Vector2(1400, 1000));
        player.SetView(new Vector2(1300, 900), new Vector2(1500, 1100));
        var zombie = world.Spawn(new Vector2(1000, 1000));

        world.Run(0.5f);

        Assert.False(zombie.IsWindingUp);
        Assert.True(zombie.Position.X > 1020f);
    }

    [Fact]
    public void RangedZombie_DoesNotShootThroughAPillar()
    {
        var world = new World(PillarArena);
        world.AddPlayer(new Vector2(1700, 1500));
        var zombie = world.Spawn(new Vector2(1300, 1500));

        world.Run(0.3f);

        Assert.False(zombie.IsWindingUp);
    }

    [Fact]
    public void Fireball_GoesOutOnAPillar()
    {
        var fireballs = new EnemyShotSystem();
        var player = new Player(0, new FakeInput(), TestData.Game().Player, new NullAudioManager(), new Vector2(1800, 1500));

        fireballs.Fire(new Vector2(1300, 1500), Vector2.UnitX, Attack());
        for (var i = 0; i < 180; i++)
        {
            fireballs.Update(Step, new List<Player> { player }, PillarArena);
        }

        Assert.Equal(0, fireballs.FireballCount);
        Assert.Equal(100f, player.Hp);
    }

    [Fact]
    public void ShippedRangedZombie_HasATelegraphedFireball_FromWaveFive()
    {
        var ranged = DataLoader.LoadAll(DataLoader.DefaultDirectory).GetEnemy("ranged");

        Assert.Equal(5, ranged.UnlockWave);
        Assert.NotNull(ranged.Attack);
        Assert.Equal(EnemyAttackType.Fireball, ranged.Attack!.Type);
        Assert.True(ranged.Attack.TelegraphSeconds >= 0.6f);
        Assert.Equal(ShapeKind.Diamond, ranged.Shape);
    }

    [Fact]
    public void Attack_WithoutATelegraph_IsRejected()
    {
        const string json = """
            { "enemies": [ { "id": "x", "displayName": "X", "hp": 1, "speed": 1, "radius": 1, "contactDamage": 0,
              "contactCooldown": 1, "xp": 1, "scoreValue": 1, "cost": 1, "unlockWave": 1, "dropChance": 0,
              "attack": { "type": "fireball", "damage": 10, "range": 450, "telegraphSeconds": 0, "cooldownSeconds": 1,
                "projectileSpeed": 380, "projectileRadius": 12, "projectileRange": 700 } } ] }
            """;

        var error = Assert.Throws<DataLoadException>(() => DataLoader.ParseEnemies(json, "enemies.json"));

        Assert.Equal("enemies[0].attack.telegraphSeconds", error.Field);
    }

    private static EnemyAttackConfig Attack()
    {
        return new EnemyAttackConfig
        {
            Type = EnemyAttackType.Fireball, Damage = 10, Range = 450, TelegraphSeconds = 0.6f, CooldownSeconds = 1.6f,
            ProjectileSpeed = 380, ProjectileRadius = 12, ProjectileRange = 700,
        };
    }

    private sealed class World
    {
        private readonly Arena _arena;
        private readonly EnemySystem _enemies;
        private readonly List<Player> _players = new();

        public World(Arena arena)
        {
            _arena = arena;
            _enemies = new EnemySystem(new NullAudioManager()) { Fireballs = Fireballs };
        }

        public EnemyShotSystem Fireballs { get; } = new();

        public Player AddPlayer(Vector2 position)
        {
            var player = new Player(_players.Count, new FakeInput(), TestData.Game().Player, new NullAudioManager(), position);
            _players.Add(player);
            return player;
        }

        public Enemy Spawn(Vector2 position)
        {
            var config = new EnemyConfig
            {
                Id = "ranged", DisplayName = "Ranged", Hp = 60, Speed = 70, Radius = 20, ContactDamage = 0,
                ContactCooldown = 0.8f, Xp = 20, ScoreValue = 20, Cost = 2.5f, UnlockWave = 1, DropChance = 0f,
                Attack = Attack(),
            };
            return _enemies.Spawn(config, position);
        }

        public void Run(float seconds)
        {
            var steps = (int)MathF.Round(seconds / Step);
            for (var i = 0; i < steps; i++)
            {
                _enemies.Update(Step, _players, _arena);
                Fireballs.Update(Step, _players, _arena);
            }
        }
    }
}
