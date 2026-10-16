using Fourgate.Core.Audio;
using Fourgate.Core.Data;
using Fourgate.Core.Rng;
using Fourgate.Entities;
using Fourgate.Systems;
using Fourgate.Weapons;
using Microsoft.Xna.Framework;

namespace Fourgate.Tests;

public class PacingTests
{
    private const float Step = 1f / 60f;

    private static readonly Arena TestArena = new(TestData.Arena());
    private static readonly CatchUpConfig CatchUp = new() { NearDistance = 800, FarDistance = 1100, SpeedMult = 2f };

    // One 260x260 pillar in the middle of the test arena: x and y 1370-1630.
    private static readonly Arena PillarArena = new(new ArenaConfig
    {
        X = 0, Y = 0, Size = 3000, DoorWidth = 420, WallThickness = 24,
        Pillars = new List<PillarConfig> { new() { CenterX = 1500, CenterY = 1500, Width = 260, Height = 260 } },
    });

    [Fact]
    public void CatchUp_IsFullFarAway_NoneUpClose_AndEasesInBetween()
    {
        Assert.Equal(1f, CatchUp.MultiplierAt(300f));
        Assert.Equal(1f, CatchUp.MultiplierAt(800f));
        Assert.Equal(1.5f, CatchUp.MultiplierAt(950f), 3);
        Assert.Equal(2f, CatchUp.MultiplierAt(1100f), 3);
        Assert.Equal(2f, CatchUp.MultiplierAt(5000f), 3);
    }

    [Fact]
    public void FarEnemy_WalksFaster_ButNearOneKeepsItsOwnSpeed()
    {
        var far = WalkedInOneSecond(startDistance: 1400f);
        var near = WalkedInOneSecond(startDistance: 600f);

        Assert.Equal(100f, near, 1);
        Assert.InRange(far, 195f, 200.5f);
    }

    [Fact]
    public void ShippedGame_LetsDistantEnemiesCatchUp_OnlyOffScreen()
    {
        var catchUp = DataLoader.LoadAll(DataLoader.DefaultDirectory).Game.CatchUp;

        Assert.NotNull(catchUp);
        Assert.True(catchUp.SpeedMult > 1f);

        // The 1920x1080 view at zoom 1.5 shows 640 units either side of the player.
        Assert.True(catchUp.NearDistance >= 700f);
    }

    [Fact]
    public void Blast_SparesAnEnemyBehindAPillar_ButHitsOneInTheOpen()
    {
        var explosives = new ExplosiveSystem(
            PillarArena, TestData.Game().FriendlyFire, new GameRandom(1), new NullAudioManager());
        var behind = new PracticeTarget(new Vector2(1660, 1500), 20);
        var open = new PracticeTarget(new Vector2(1300, 1300), 20);
        explosives.AddTarget(behind);
        explosives.AddTarget(open);

        explosives.Detonate(new Vector2(1340, 1500), 100, 400, 0, new List<Player>());

        Assert.Equal(0, behind.HitCount);
        Assert.Equal(1, open.HitCount);
    }

    [Fact]
    public void Blast_SparesAPlayerBehindAPillar()
    {
        var explosives = new ExplosiveSystem(
            PillarArena, TestData.Game().FriendlyFire, new GameRandom(1), new NullAudioManager());
        var sheltered = new Player(0, new FakeInput(), TestData.Game().Player, new NullAudioManager(), new Vector2(1660, 1500));
        var exposed = new Player(1, new FakeInput(), TestData.Game().Player, new NullAudioManager(), new Vector2(1340, 1300));

        explosives.Detonate(new Vector2(1340, 1500), 100, 400, 0, new List<Player> { sheltered, exposed });

        Assert.Equal(sheltered.MaxHp, sheltered.Hp);
        Assert.True(exposed.Hp < exposed.MaxHp);
    }

    [Fact]
    public void ShippedShotgun_CarriesMoreAsItsMagazineGrows()
    {
        var shotgun = DataLoader.LoadAll(DataLoader.DefaultDirectory).GetWeapon("shotgun");
        var weapon = new Weapon(shotgun, TestData.Game(), new NullAudioManager());
        var carriedAtOne = weapon.ReserveMax;

        weapon.SetLevel(10);

        Assert.True(weapon.ReserveMax > carriedAtOne);
        Assert.True(weapon.ReserveMax >= weapon.Stats.MagSize * 4);
    }

    [Fact]
    public void ShippedGuns_NeverCarryFewerMagazinesThanAtLevelOne()
    {
        var data = DataLoader.LoadAll(DataLoader.DefaultDirectory);
        foreach (var config in data.Weapons)
        {
            if (config.Ammo.InfiniteReserve || !config.Ammo.UsesMagazine || config.Ammo.MagSize <= 0)
            {
                continue;
            }

            var weapon = new Weapon(config, TestData.Game(), new NullAudioManager());
            var magazinesAtOne = weapon.ReserveMax / (float)weapon.Stats.MagSize;
            for (var level = 2; level <= WeaponConfig.MaxLevel; level++)
            {
                weapon.SetLevel(level);
                Assert.True(
                    weapon.ReserveMax / (float)weapon.Stats.MagSize >= magazinesAtOne - 0.001f,
                    $"{config.Id} at level {level} carries fewer magazines than at level 1");
            }
        }
    }

    // A 100-speed enemy that starts this far to the right of a lone player, after one second.
    private static float WalkedInOneSecond(float startDistance)
    {
        var enemies = new EnemySystem(new NullAudioManager()) { CatchUp = CatchUp };
        var player = new Player(0, new FakeInput(), TestData.Game().Player, new NullAudioManager(), new Vector2(200, 1500));
        var start = new Vector2(200 + startDistance, 1500);
        var enemy = enemies.Spawn(Walker(), start);
        var players = new List<Player> { player };
        for (var i = 0; i < 60; i++)
        {
            enemies.Update(Step, players, TestArena);
        }

        return start.X - enemy.Position.X;
    }

    private static EnemyConfig Walker()
    {
        return new EnemyConfig
        {
            Id = "walker", DisplayName = "Walker", Hp = 100, Speed = 100, Radius = 20, ContactDamage = 10,
            ContactCooldown = 0.8f, Xp = 10, ScoreValue = 10, Cost = 1, UnlockWave = 1, DropChance = 0f,
        };
    }
}
