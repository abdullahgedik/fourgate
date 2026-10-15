using Fourgate.Core.Audio;
using Fourgate.Core.Data;
using Fourgate.Core.Rng;
using Fourgate.Entities;
using Fourgate.Entities.Enemies;
using Fourgate.Systems;
using Fourgate.Weapons;
using Microsoft.Xna.Framework;

namespace Fourgate.Tests;

public class MineTests
{
    private const float Step = 1f / 60f;

    private static readonly Arena TestArena = new(TestData.Arena());
    private static readonly Vector2 Spot = new(2000, 2000);
    private static readonly List<Player> Nobody = new();

    [Fact]
    public void Mine_DoesNotGoOff_BeforeItIsArmed()
    {
        var world = new World();
        var enemy = world.Spawn(Spot + new Vector2(30, 0));

        world.Mines.Place(Mine(Spot, armSeconds: 0.75f));
        world.Run(0.5f);

        Assert.True(enemy.IsAlive);
        Assert.Equal(1, world.Mines.Count);

        world.Run(0.5f);
        Assert.False(enemy.IsAlive);
        Assert.Equal(0, world.Mines.Count);
    }

    [Fact]
    public void ArmedMine_WaitsUntilAnEnemyComesClose()
    {
        var world = new World();
        var enemy = world.Spawn(Spot + new Vector2(200, 0));
        world.Mines.Place(Mine(Spot, armSeconds: 0f));

        world.Run(1f);
        Assert.Equal(1, world.Mines.Count);

        enemy.Position = Spot + new Vector2(40, 0);
        world.Run(0.1f);
        Assert.Equal(0, world.Mines.Count);
    }

    [Fact]
    public void Blast_HitsEverythingInItsRadius()
    {
        var world = new World();
        var trigger = world.Spawn(Spot + new Vector2(30, 0));
        var inBlast = world.Spawn(Spot + new Vector2(0, 100));
        var outside = world.Spawn(Spot + new Vector2(-200, 0));
        world.Mines.Place(Mine(Spot, armSeconds: 0f));

        world.Run(0.05f);

        Assert.False(trigger.IsAlive);
        Assert.False(inBlast.IsAlive);
        Assert.True(outside.IsAlive);
    }

    [Fact]
    public void PlayersNeverSetMinesOff_ButAreHurtByTheBlast()
    {
        var world = new World();
        var owner = new Player(0, new FakeInput(), TestData.Game().Player, new NullAudioManager(), Spot);
        var players = new List<Player> { owner };
        world.Mines.Place(Mine(Spot, armSeconds: 0f));

        world.Run(1f, players);
        Assert.Equal(1, world.Mines.Count);
        Assert.Equal(100f, owner.Hp);

        world.Spawn(Spot + new Vector2(30, 0));
        world.Run(0.05f, players);
        Assert.Equal(25f, owner.Hp);
    }

    [Fact]
    public void PlacingOverTheCap_RemovesTheOldestMine()
    {
        var world = new World();
        for (var i = 0; i < 6; i++)
        {
            world.Mines.Place(Mine(new Vector2(500 + i * 100, 500), maxPlaced: 5));
        }

        Assert.Equal(5, world.Mines.Count);
        for (var i = 0; i < world.Mines.Count; i++)
        {
            Assert.NotEqual(500f, world.Mines[i].Position.X);
        }
    }

    [Fact]
    public void WithoutACap_EveryMineStaysDown()
    {
        var world = new World();
        for (var i = 0; i < 20; i++)
        {
            world.Mines.Place(Mine(new Vector2(200 + i * 100, 500), maxPlaced: 0));
        }

        Assert.Equal(20, world.Mines.Count);
    }

    [Fact]
    public void Cap_CountsEachPlayerSeparately()
    {
        var world = new World();
        for (var i = 0; i < 5; i++)
        {
            world.Mines.Place(Mine(new Vector2(500 + i * 100, 500), maxPlaced: 5, owner: 0));
            world.Mines.Place(Mine(new Vector2(500 + i * 100, 900), maxPlaced: 5, owner: 1));
        }

        Assert.Equal(10, world.Mines.Count);
    }

    [Fact]
    public void ChainDetonation_SetsOffMinesInTheBlast_EvenUnarmedOnes()
    {
        var world = new World();
        world.Spawn(Spot + new Vector2(-30, 0));
        world.Mines.Place(Mine(Spot, armSeconds: 0f, chain: true));
        world.Mines.Place(Mine(Spot + new Vector2(100, 0), armSeconds: 5f, chain: true));
        world.Mines.Place(Mine(Spot + new Vector2(200, 0), armSeconds: 5f, chain: true));
        world.Mines.Place(Mine(Spot + new Vector2(600, 0), armSeconds: 5f, chain: true));

        world.Run(0.05f);

        // The chain runs 0 -> 100 -> 200; the mine at 600 is out of every blast.
        Assert.Equal(1, world.Mines.Count);
        Assert.Equal(Spot.X + 600, world.Mines[0].Position.X);
    }

    [Fact]
    public void WithoutChainDetonation_OtherMinesStayDown()
    {
        var world = new World();
        world.Spawn(Spot + new Vector2(-30, 0));
        world.Mines.Place(Mine(Spot, armSeconds: 0f));
        world.Mines.Place(Mine(Spot + new Vector2(100, 0), armSeconds: 5f));

        world.Run(0.05f);

        Assert.Equal(1, world.Mines.Count);
    }

    [Fact]
    public void MultiBlast_ExplodesThreeTimes_ShortlyAfterEachOther()
    {
        var world = new World();
        var tough = world.Spawn(Spot + new Vector2(30, 0), hp: 1000);
        world.Mines.Place(Mine(Spot, armSeconds: 0f) with { BlastCount = 3, BlastInterval = 0.15f, BlastSpread = 30 });

        world.Run(0.05f);
        Assert.Equal(850f, tough.Hp, 0);
        Assert.Equal(0, world.Mines.Count);
        Assert.Equal(2, world.Mines.PendingBlastCount);

        world.Run(0.15f);
        Assert.Equal(700f, tough.Hp, 0);

        world.Run(0.2f);
        Assert.Equal(550f, tough.Hp, 0);
        Assert.Equal(0, world.Mines.PendingBlastCount);
    }

    [Fact]
    public void MultiBlast_LaterBlasts_StayWithinTheSpread()
    {
        var world = new World();
        var farEdge = world.Spawn(Spot + new Vector2(140, 0), hp: 1000);
        var nearEdge = world.Spawn(Spot + new Vector2(-30, 0), hp: 1000);
        world.Mines.Place(Mine(Spot, armSeconds: 0f) with { BlastCount = 3, BlastInterval = 0.15f, BlastSpread = 30 });

        world.Run(1f);

        // The near one is inside all three blasts; the far one, 140 out, is outside the first
        // and can only be reached by a blast shifted toward it.
        Assert.Equal(550f, nearEdge.Hp, 0);
        Assert.True(farEdge.Hp >= 700f);
    }

    [Fact]
    public void LevelTen_ArmsInstantly_AndExplodesThreeTimes()
    {
        var mine = DataLoader.LoadAll(DataLoader.DefaultDirectory).GetWeapon("mine");

        var nine = WeaponStats.Compute(mine, 9);
        var ten = WeaponStats.Compute(mine, 10);

        Assert.Equal(WeaponCategory.Equipment, mine.Category);
        Assert.Equal(WeaponType.Placed, mine.Type);
        Assert.True(nine.ArmSeconds > 0f);
        Assert.Equal(1, nine.BlastCount);
        Assert.Equal(0f, ten.ArmSeconds);
        Assert.False(ten.ChainDetonation);
        Assert.Equal(3, ten.BlastCount);

        // The shipped mine has no cap on how many can be down.
        Assert.Equal(0, nine.MaxPlaced);
        Assert.Equal(0, ten.MaxPlaced);
    }

    [Fact]
    public void Weapon_ThrowsAMine_ToTheAimPoint_AndUsesOneFromTheCount()
    {
        var config = DataLoader.LoadAll(DataLoader.DefaultDirectory).GetWeapon("mine");
        var weapon = new Weapon(config, TestData.Game(), new NullAudioManager());
        var shots = new FakeShotHandler();

        weapon.Update(Step, new WeaponTrigger(true, true, false, 100), Spot, Vector2.UnitX, 0, shots);

        var thrown = Assert.Single(shots.Mines);
        Assert.Equal(Spot + new Vector2(100, 0), thrown.Position);
        Assert.Equal(Spot, thrown.From);
        Assert.True(thrown.ThrowSpeed > 0f);
        Assert.Equal("mine", thrown.WeaponId);
        Assert.Equal(config.Ammo.ReserveMax - 1, weapon.Reserve);
    }

    [Fact]
    public void Weapon_AimedBeyondReach_ThrowsTheMineOnlyAsFarAsItCan()
    {
        var config = DataLoader.LoadAll(DataLoader.DefaultDirectory).GetWeapon("mine");
        var weapon = new Weapon(config, TestData.Game(), new NullAudioManager());
        var shots = new FakeShotHandler();

        weapon.Update(Step, new WeaponTrigger(true, true, false, 5000), Spot, Vector2.UnitX, 0, shots);

        Assert.Equal(Spot + new Vector2(config.Thrown!.MaxRange, 0), Assert.Single(shots.Mines).Position);
    }

    [Fact]
    public void ThrownMine_FliesToItsSpot_AndOnlyStartsArmingOnceItLands()
    {
        var world = new World();
        var target = Spot + new Vector2(400, 0);

        // A zombie right under its path, and one where it lands.
        var underPath = world.Spawn(Spot + new Vector2(200, 30));
        var atLanding = world.Spawn(target + new Vector2(30, 0));

        // 400 units at 800 per second: half a second in the air, then 0.4 s to arm.
        world.Mines.Place(new MineShot(target, 150, 50, 45, 0.4f, 0, false, 0, "mine", From: Spot, ThrowSpeed: 800));
        world.Run(0.25f);
        var mine = world.Mines[0];
        Assert.True(mine.IsFlying);
        Assert.False(mine.IsArmed);
        Assert.True(Vector2.Distance(mine.Position, Spot + new Vector2(200, 0)) < 20f);
        Assert.True(underPath.IsAlive);

        world.Run(0.5f);
        Assert.False(mine.IsFlying);
        Assert.Equal(target, mine.Position);
        Assert.True(atLanding.IsAlive);

        world.Run(0.3f);
        Assert.False(atLanding.IsAlive);
        Assert.True(underPath.IsAlive);
    }

    private static MineShot Mine(
        Vector2 position, float armSeconds = 0.75f, int maxPlaced = 5, bool chain = false, int owner = 0)
    {
        return new MineShot(position, 150, 120, 45, armSeconds, maxPlaced, chain, owner, "mine");
    }

    private sealed class World
    {
        private readonly EnemySystem _enemies = new(new NullAudioManager());

        public World()
        {
            var explosives = new ExplosiveSystem(
                TestArena, TestData.Game().FriendlyFire, new GameRandom(1), new NullAudioManager());
            explosives.AddSource(_enemies);
            Mines = new MineSystem(explosives, new GameRandom(1), new NullAudioManager());
            Mines.AddSource(_enemies);
        }

        public MineSystem Mines { get; }

        public Enemy Spawn(Vector2 position, float hp = 80)
        {
            var zombie = TestData.Zombie();
            var config = new EnemyConfig
            {
                Id = zombie.Id,
                DisplayName = zombie.DisplayName,
                Hp = hp,
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
            var steps = (int)MathF.Round(seconds / Step);
            for (var i = 0; i < steps; i++)
            {
                // Nobody for the enemies to chase, so they stay where the test put them.
                _enemies.Update(Step, Nobody, TestArena);
                Mines.Update(Step, everyone);
            }
        }
    }
}
