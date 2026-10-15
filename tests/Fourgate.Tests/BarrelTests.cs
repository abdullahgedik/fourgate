using Fourgate.Core.Audio;
using Fourgate.Core.Data;
using Fourgate.Core.Rng;
using Fourgate.Entities;
using Fourgate.Entities.Enemies;
using Fourgate.Systems;
using Fourgate.Weapons;
using Microsoft.Xna.Framework;

namespace Fourgate.Tests;

public class BarrelTests
{
    private const float Step = 1f / 60f;

    private static readonly Arena TestArena = new(TestData.Arena());
    private static readonly Vector2 Spot = new(2000, 2000);
    private static readonly ProjectileConfig Bullet = new() { Speed = 6000, Radius = 4, Range = 1100 };
    private static readonly List<Player> Nobody = new();

    [Fact]
    public void ZombiesWalkingPast_DoNotSetABarrelOff()
    {
        var world = new World();
        world.Spawn(Spot + new Vector2(10, 0));
        world.Mines.Place(Barrel(Spot));

        world.Run(1f);

        Assert.Equal(1, world.Mines.Count);
    }

    [Fact]
    public void ShootingABarrel_BlowsItUp_AndTheBulletStopsAtIt()
    {
        var world = new World();
        var nearby = world.Spawn(Spot + new Vector2(0, 120));
        var behind = world.Spawn(Spot + new Vector2(300, 0));
        world.Mines.Place(Barrel(Spot));

        world.Shots.FireProjectile(Spot - new Vector2(400, 0), Vector2.UnitX, Bullet, 15, 0);
        world.Run(0.3f);

        Assert.Equal(0, world.Mines.Count);
        Assert.Equal(800f, nearby.Hp, 0);
        Assert.Equal(1000f, behind.Hp);
    }

    [Fact]
    public void AnotherExplosion_SetsABarrelOff()
    {
        var world = new World();
        world.Mines.Place(Barrel(Spot));

        world.Explosives.Detonate(Spot + new Vector2(100, 0), 60, 110, 0, Nobody);
        world.Run(0.1f);

        Assert.Equal(0, world.Mines.Count);
    }

    [Fact]
    public void FlamesAndFire_SetABarrelOff()
    {
        var world = new World();
        world.Mines.Place(Barrel(Spot));

        world.Hazards.SpawnFire(Spot + new Vector2(60, 0), 50, 2, 10, 0, 0);
        world.Run(0.1f);

        Assert.Equal(0, world.Mines.Count);
    }

    [Fact]
    public void AMinesBlast_SetsBarrelsOff()
    {
        var world = new World();
        world.Spawn(Spot + new Vector2(-200, 0));
        world.Mines.Place(new MineShot(Spot - new Vector2(200, 0), 150, 120, 45, 0, 0, false, 0, "mine"));
        world.Mines.Place(Barrel(Spot - new Vector2(100, 0)));

        world.Run(0.2f);

        Assert.Equal(0, world.Mines.Count);
    }

    [Fact]
    public void WithChainDetonation_BarrelsSetEachOtherOff()
    {
        var world = new World();
        world.Mines.Place(Barrel(Spot, chain: true));
        world.Mines.Place(Barrel(Spot + new Vector2(140, 0), chain: true));
        world.Mines.Place(Barrel(Spot + new Vector2(280, 0), chain: true));
        world.Mines.Place(Barrel(Spot + new Vector2(800, 0), chain: true));

        world.Explosives.Detonate(Spot - new Vector2(100, 0), 60, 100, 0, Nobody);
        world.Run(0.2f);

        Assert.Equal(1, world.Mines.Count);
    }

    [Fact]
    public void DelayedChain_SetsOffTheNextBarrel_AMomentLater_OneAfterAnother()
    {
        var world = new World();
        world.Mines.Place(Barrel(Spot, chain: true) with { ChainDelay = 0.2f });
        world.Mines.Place(Barrel(Spot + new Vector2(140, 0), chain: true) with { ChainDelay = 0.2f });
        world.Mines.Place(Barrel(Spot + new Vector2(280, 0), chain: true) with { ChainDelay = 0.2f });

        // A grenade sets off the first one at once; it lights the second one's fuse, which lights the third.
        world.Explosives.Detonate(Spot - new Vector2(100, 0), 60, 110, 0, Nobody);
        world.Run(0.05f);
        Assert.Equal(2, world.Mines.Count);
        Assert.True(world.Mines[0].IsFused || world.Mines[1].IsFused);
        Assert.False(world.Mines[0].IsFused && world.Mines[1].IsFused);

        world.Run(0.3f);
        Assert.Equal(1, world.Mines.Count);

        world.Run(0.3f);
        Assert.Equal(0, world.Mines.Count);
    }

    [Fact]
    public void LeaveFire_LeavesAFirePoolWhereTheBarrelWent()
    {
        var world = new World();
        world.Mines.Place(Barrel(Spot) with { FireRadius = 130, FireSeconds = 4, FireDamagePerSecond = 30 });

        world.Explosives.Detonate(Spot + new Vector2(100, 0), 60, 110, 0, Nobody);
        world.Run(0.1f);

        Assert.Equal(1, world.Hazards.FirePoolCount);
    }

    [Fact]
    public void ShippedBarrel_IsASolidChainingWall_StrongerWithHarmlessFireAtLevelTen()
    {
        var config = DataLoader.LoadAll(DataLoader.DefaultDirectory).GetWeapon("barrel");

        var one = WeaponStats.Compute(config, 1);
        var nine = WeaponStats.Compute(config, 9);
        var ten = WeaponStats.Compute(config, 10);

        Assert.Equal(WeaponCategory.Equipment, config.Category);
        Assert.Equal(16, config.UnlockWave);
        Assert.Equal(10, config.Ammo.ReserveMax);
        Assert.True(one.Automatic);
        Assert.True(one.Shootable);
        Assert.True(one.Solid);
        Assert.True(one.EnemyHitsToDetonate > 1);
        Assert.True(one.ChainDetonation);
        Assert.Equal(0f, one.TriggerRadius);
        Assert.Equal(0, one.MaxPlaced);
        Assert.Equal(0f, nine.LeaveFireRadius);
        Assert.True(ten.Damage > nine.Damage * 1.2f);
        Assert.True(ten.BlastRadius > nine.BlastRadius * 1.2f);
        Assert.True(ten.LeaveFireRadius > 0f);
        Assert.False(ten.LeaveFireHurtsPlayers);
    }

    [Fact]
    public void GriddedBarrel_SnapsToTheCellItWasAimedAt()
    {
        var world = new World();

        Assert.True(world.Mines.Place(GridBarrel(new Vector2(1000, 1010))));

        // 64-unit cells from the arena corner: x 960-1024, y 960-1024.
        Assert.Equal(new Vector2(992, 992), world.Mines[0].Position);
    }

    [Fact]
    public void GriddedBarrel_IsRefused_OnATakenCell()
    {
        var world = new World();

        Assert.True(world.Mines.Place(GridBarrel(new Vector2(1000, 1000))));
        Assert.False(world.Mines.Place(GridBarrel(new Vector2(1020, 970))));
        Assert.Equal(1, world.Mines.Count);
    }

    [Fact]
    public void GriddedBarrel_IsRefused_OnTopOfAPlayer()
    {
        var world = new World();
        var player = new Player(0, new FakeInput(), TestData.Game().Player, new NullAudioManager(), new Vector2(992, 992));
        world.Run(Step, new List<Player> { player });

        Assert.False(world.Mines.Place(GridBarrel(new Vector2(1000, 1000))));
    }

    [Fact]
    public void Barrels_AreWalls_ForPlayersAndZombies()
    {
        var world = new World();
        world.Mines.Place(GridBarrel(new Vector2(1000, 1000)));
        var barrel = world.Mines[0];

        // A circle moved into the middle of the barrel is pushed back out of it.
        var pushed = world.Arena.ResolveMovement(barrel.Position + new Vector2(10, 0), 20);

        Assert.True(Math.Abs(pushed.X - barrel.Position.X) >= barrel.Radius + 20 - 0.01f);
    }

    [Fact]
    public void Zombie_CannotWalkThroughABarrel()
    {
        var world = new World();
        world.Mines.Place(GridBarrel(new Vector2(1000, 1000)));
        var player = new Player(0, new FakeInput(), TestData.Game().Player, new NullAudioManager(), new Vector2(1300, 992));
        var zombie = world.Spawn(new Vector2(800, 992));

        world.Run(6f, new List<Player> { player });

        // Held at the barrel's left side (barrel 962-1022, zombie radius 20).
        Assert.True(zombie.Position.X <= 942.5f);
    }

    [Fact]
    public void Zombie_BlockedByABarrel_HitsItUntilItGoesOff()
    {
        var world = new World();
        world.Mines.Place(GridBarrel(new Vector2(1000, 1000)) with { EnemyHitsToDetonate = 3 });
        var player = new Player(0, new FakeInput(), TestData.Game().Player, new NullAudioManager(), new Vector2(1300, 992));
        var players = new List<Player> { player };
        var zombie = world.Spawn(new Vector2(800, 992));

        // Reaches the barrel after about 1.3 s, then hits it every 0.8 s: two hits by 2.5 s.
        world.Run(2.5f, players);
        Assert.Equal(1, world.Mines.Count);
        Assert.Equal(100f, player.Hp);

        world.Run(3.5f, players);
        Assert.Equal(0, world.Mines.Count);
        Assert.True(zombie.Hp < 1000f);
    }

    [Fact]
    public void Zombies_LeaveABarrelAlone_WhenItTakesNoZombieHits()
    {
        var world = new World();
        world.Mines.Place(GridBarrel(new Vector2(1000, 1000)));
        var player = new Player(0, new FakeInput(), TestData.Game().Player, new NullAudioManager(), new Vector2(1300, 992));
        world.Spawn(new Vector2(800, 992));

        world.Run(10f, new List<Player> { player });

        Assert.Equal(1, world.Mines.Count);
    }

    [Fact]
    public void HoldingTheButton_PlacesBarrelsInEveryNewCell_AndRefundsTakenOnes()
    {
        var world = new World();
        var config = DataLoader.LoadAll(DataLoader.DefaultDirectory).GetWeapon("barrel");
        var weapon = new Weapon(config, TestData.Game(), new NullAudioManager());
        var handler = new RouterToMines(world.Mines);
        var hold = new WeaponTrigger(true, true, false, float.PositiveInfinity);

        // Aim at one cell for a second: one barrel, the rest refunded.
        for (var i = 0; i < 60; i++)
        {
            weapon.Update(Step, hold with { Pressed = i == 0 }, new Vector2(1000, 1000), Vector2.UnitX, 0, handler);
        }

        Assert.Equal(1, world.Mines.Count);
        Assert.Equal(9, weapon.Reserve);

        // Sweep the aim down the floor: a new barrel in each new cell.
        for (var i = 0; i < 60; i++)
        {
            weapon.Update(Step, hold, new Vector2(1000, 1000 + i * 10), Vector2.UnitX, 0, handler);
        }

        Assert.True(world.Mines.Count >= 5);
        Assert.Equal(10 - world.Mines.Count, weapon.Reserve);
    }

    [Fact]
    public void PlacedWeapon_ThatNothingCouldSetOff_IsRejected()
    {
        const string json = """
            {
              "weapons": [
                {
                  "id": "dud", "displayName": "Dud", "category": "equipment", "slot": 5, "unlockWave": 1,
                  "type": "placed", "damage": 10, "fireRate": 1, "automatic": false,
                  "ammo": { "usesMagazine": false, "reserveMax": 4, "infiniteReserve": false },
                  "placed": { },
                  "explosion": { "radius": 100 }
                }
              ]
            }
            """;

        var error = Assert.Throws<DataLoadException>(() => DataLoader.ParseWeapons(json, "weapons.json"));

        Assert.Equal("weapons[0].placed", error.Field);
    }

    private static MineShot Barrel(Vector2 position, bool chain = false)
    {
        return new MineShot(position, 200, 160, 0, 0, 0, chain, 0, "barrel", Shootable: true);
    }

    private static MineShot GridBarrel(Vector2 aimedAt)
    {
        return new MineShot(aimedAt, 200, 160, 0, 0, 0, true, 0, "barrel", Shootable: true, Solid: true, GridSize: 64);
    }

    /// <summary>Sends placed weapons straight to the mine system.</summary>
    private sealed class RouterToMines : IShotHandler
    {
        private readonly MineSystem _mines;

        public RouterToMines(MineSystem mines)
        {
            _mines = mines;
        }

        public void FireProjectile(
            Vector2 origin, Vector2 direction, ProjectileConfig projectile, float damage, int ownerIndex,
            float? range = null, int pierce = 0, int ricochets = 0, float ricochetRadius = 0f,
            float ricochetDamageMult = 1f, bool overkill = false, RocketPayload? rocket = null, int wallBounces = 0)
        {
        }

        public void ThrowExplosive(in ExplosiveShot shot)
        {
        }

        public bool PlaceMine(in MineShot shot)
        {
            return _mines.Place(shot);
        }

        public void EmitFlame(in FlameShot shot)
        {
        }

        public void FireBeam(in BeamShot shot)
        {
        }
    }

    private sealed class World
    {
        private readonly EnemySystem _enemies = new(new NullAudioManager());

        public World()
        {
            Hazards = new HazardSystem(TestData.Game().FriendlyFire);
            Explosives = new ExplosiveSystem(
                TestArena, TestData.Game().FriendlyFire, new GameRandom(1), new NullAudioManager(), Hazards);
            Arena = new Arena(TestData.Arena());
            Mines = new MineSystem(Explosives, new GameRandom(1), new NullAudioManager(), Hazards) { Arena = Arena };
            Arena.Obstacles = Mines;
            Shots = new ShotSystem(TestArena) { Explosives = Explosives };

            Explosives.AddSource(_enemies);
            Mines.AddSource(_enemies);
            Shots.AddSource(_enemies);
            Hazards.AddSource(_enemies);

            Shots.AddSource(Mines);
            Explosives.AddSource(Mines);
            Hazards.AddSource(Mines);
        }

        public HazardSystem Hazards { get; }

        public Arena Arena { get; }

        public ExplosiveSystem Explosives { get; }

        public MineSystem Mines { get; }

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

        public void Run(float seconds, List<Player>? players = null)
        {
            var everyone = players ?? Nobody;
            var steps = Math.Max(1, (int)MathF.Round(seconds / Step));
            for (var i = 0; i < steps; i++)
            {
                _enemies.Update(Step, everyone, Arena);
                Shots.Update(Step);
                Explosives.Update(Step, everyone);
                Hazards.Update(Step, everyone);
                Mines.Update(Step, everyone);
            }
        }
    }
}
