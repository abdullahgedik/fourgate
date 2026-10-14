using Fourgate.Core.Audio;
using Fourgate.Core.Data;
using Fourgate.Core.Rng;
using Fourgate.Entities;
using Fourgate.Systems;
using Fourgate.Weapons;
using Microsoft.Xna.Framework;

namespace Fourgate.Tests;

public class PillarTests
{
    private const float Step = 1f / 60f;

    // One 260x260 pillar in the middle of the test arena: x and y 1370-1630.
    private static readonly Arena PillarArena = new(new ArenaConfig
    {
        X = 0, Y = 0, Size = 3000, DoorWidth = 420, WallThickness = 24,
        Pillars = new List<PillarConfig> { new() { CenterX = 1500, CenterY = 1500, Width = 260, Height = 260 } },
    });

    [Fact]
    public void ShippedArena_HasFourPillars_MirroredAcrossTheMiddle()
    {
        var arena = DataLoader.LoadAll(DataLoader.DefaultDirectory).Game.Arena;

        Assert.Equal(4, arena.Pillars.Count);
        foreach (var pillar in arena.Pillars)
        {
            Assert.Contains(arena.Pillars, other =>
                Math.Abs(other.CenterX - (arena.Size - pillar.CenterX)) < 0.01f
                && Math.Abs(other.CenterY - pillar.CenterY) < 0.01f);
            Assert.Contains(arena.Pillars, other =>
                Math.Abs(other.CenterX - pillar.CenterX) < 0.01f
                && Math.Abs(other.CenterY - (arena.Size - pillar.CenterY)) < 0.01f);
        }
    }

    [Fact]
    public void Walkers_ArePushedOutOfAPillar()
    {
        var position = PillarArena.ResolveMovement(new Vector2(1380, 1500), 20);

        Assert.True(position.X <= 1350.01f);
        Assert.True(PillarArena.IsSolid(new Vector2(1500, 1500)));
        Assert.False(PillarArena.IsSolid(new Vector2(1300, 1500)));
    }

    [Fact]
    public void Raycast_StopsAtThePillarFace_WithItsNormal()
    {
        var distance = PillarArena.Raycast(new Vector2(1000, 1500), Vector2.UnitX, out var normal);

        Assert.Equal(370f, distance, 1);
        Assert.Equal(-Vector2.UnitX, normal);
    }

    [Fact]
    public void Bullet_DoesNotPassThroughAPillar()
    {
        var shots = new ShotSystem(PillarArena);
        var behind = new PracticeTarget(new Vector2(1800, 1500), 20);
        shots.AddTarget(behind);

        shots.FireProjectile(new Vector2(1000, 1500), Vector2.UnitX, Projectile, 15, 0);
        Run(shots, 30);

        Assert.Equal(0, behind.HitCount);
        Assert.Equal(0, shots.BulletCount);
    }

    [Fact]
    public void RicochetBullet_BouncesOffAPillar()
    {
        var shots = new ShotSystem(PillarArena);
        var behindTheShooter = new PracticeTarget(new Vector2(1050, 1500), 20);
        shots.AddTarget(behindTheShooter);

        shots.FireProjectile(new Vector2(1200, 1500), Vector2.UnitX, Projectile, 15, 0, ricochets: 1);
        Run(shots, 30);

        Assert.Equal(1, behindTheShooter.HitCount);
    }

    [Fact]
    public void Zombie_WalksAroundAPillar_ToReachThePlayer()
    {
        var enemies = new EnemySystem(new NullAudioManager());
        var player = new Player(0, new FakeInput(), TestData.Game().Player, new NullAudioManager(), new Vector2(1800, 1500));
        var players = new List<Player> { player };
        var zombie = enemies.Spawn(TestData.Zombie(), new Vector2(1150, 1500));

        for (var i = 0; i < 60 * 20; i++)
        {
            enemies.Update(Step, players, PillarArena);
        }

        Assert.Equal(40f, Vector2.Distance(zombie.Position, player.Position), 1);
    }

    [Fact]
    public void BigZombie_ReachesAPlayerHuggingAPillar()
    {
        var enemies = new EnemySystem(new NullAudioManager());
        var player = new Player(0, new FakeInput(), TestData.Game().Player, new NullAudioManager(), new Vector2(1650, 1500));
        var players = new List<Player> { player };
        var big = new EnemyConfig
        {
            Id = "big", DisplayName = "Big", Hp = 60, Speed = 110, Radius = 40, ContactDamage = 10,
            ContactCooldown = 0.8f, Xp = 10, ScoreValue = 10, Cost = 1, UnlockWave = 1, DropChance = 0f,
        };
        var enemy = enemies.Spawn(big, new Vector2(1150, 1500));

        for (var i = 0; i < 60 * 20; i++)
        {
            enemies.Update(Step, players, PillarArena);
        }

        Assert.Equal(60f, Vector2.Distance(enemy.Position, player.Position), 1);
    }

    [Fact]
    public void Barrel_IsRefused_OnACellTouchingAPillar()
    {
        var hazards = new HazardSystem(TestData.Game().FriendlyFire);
        var explosives = new ExplosiveSystem(
            PillarArena, TestData.Game().FriendlyFire, new GameRandom(1), new NullAudioManager(), hazards);
        var mines = new MineSystem(explosives, new GameRandom(1), new NullAudioManager(), hazards) { Arena = PillarArena };

        // Cells are 64 wide from the arena corner: 1344-1408 overlaps the pillar, 1280-1344 does not.
        Assert.False(mines.Place(GridBarrel(new Vector2(1500, 1500))));
        Assert.False(mines.Place(GridBarrel(new Vector2(1380, 1500))));
        Assert.True(mines.Place(GridBarrel(new Vector2(1300, 1500))));
    }

    [Fact]
    public void ThrownExplosive_AimedAtAPillar_LandsBesideIt()
    {
        var landing = PillarArena.PlaceOnFloor(new Vector2(1400, 1500), 10);

        Assert.True(landing.X <= 1360.01f);
    }

    private static readonly ProjectileConfig Projectile = new() { Speed = 6000, Radius = 4, Range = 1100 };

    private static MineShot GridBarrel(Vector2 aimedAt)
    {
        return new MineShot(aimedAt, 200, 160, 0, 0, 0, true, 0, "barrel", Shootable: true, Solid: true, GridSize: 64);
    }

    private static void Run(ShotSystem shots, int steps)
    {
        for (var i = 0; i < steps; i++)
        {
            shots.Update(Step);
        }
    }
}
