using Fourgate.Core.Audio;
using Fourgate.Core.Data;
using Fourgate.Core.Rng;
using Fourgate.Entities;
using Fourgate.Systems;
using Fourgate.Weapons;
using Microsoft.Xna.Framework;

namespace Fourgate.Tests;

public class FriendlyFireTests
{
    private const float Step = 1f / 60f;

    private static readonly Arena TestArena = new(TestData.Arena());
    private static readonly Vector2 Spot = new(2000, 2000);

    private static readonly FriendlyFireConfig Capped = new()
    {
        Enabled = true,
        DamageToOther = 0.5f,
        SelfExplosiveDamage = 0.5f,
        MaxDamagePerExplosion = 15,
        MaxDamagePerSecond = 8,
        CanKill = false,
    };

    [Fact]
    public void OwnExplosion_IsCappedPerBlast()
    {
        var thrower = CreatePlayer(0);
        var explosives = CreateExplosives();

        explosives.Detonate(Spot, 300, 120, 0, new List<Player> { thrower });

        Assert.Equal(85f, thrower.Hp);
    }

    [Fact]
    public void SmallExplosion_StaysBelowTheCap()
    {
        var thrower = CreatePlayer(0);
        var explosives = CreateExplosives();

        explosives.Detonate(Spot, 20, 120, 0, new List<Player> { thrower });

        Assert.Equal(90f, thrower.Hp);
    }

    [Fact]
    public void OtherPlayer_TakesTheSameShareAndCap()
    {
        var thrower = CreatePlayer(0, Spot + new Vector2(1000, 0));
        var partner = CreatePlayer(1);
        var explosives = CreateExplosives();

        explosives.Detonate(Spot, 300, 120, 0, new List<Player> { thrower, partner });

        Assert.Equal(100f, thrower.Hp);
        Assert.Equal(85f, partner.Hp);
    }

    [Fact]
    public void OwnExplosions_NeverKill()
    {
        var thrower = CreatePlayer(0);
        var explosives = CreateExplosives();
        var players = new List<Player> { thrower };

        for (var i = 0; i < 20; i++)
        {
            explosives.Detonate(Spot, 300, 120, 0, players);
        }

        Assert.True(thrower.IsAlive);
        Assert.Equal(1f, thrower.Hp);
    }

    [Fact]
    public void EnemyDamage_CanStillKill_AfterOwnExplosionsLeftOneHp()
    {
        var thrower = CreatePlayer(0);
        var explosives = CreateExplosives();
        for (var i = 0; i < 20; i++)
        {
            explosives.Detonate(Spot, 300, 120, 0, new List<Player> { thrower });
        }

        thrower.TakeDamage(10);

        Assert.False(thrower.IsAlive);
    }

    [Fact]
    public void OwnFire_IsCappedPerSecond_AndNeverKills()
    {
        var thrower = CreatePlayer(0);
        var hazards = new HazardSystem(Capped);
        var players = new List<Player> { thrower };

        // Half of 100 per second is 50, capped to 8 per second.
        hazards.SpawnFire(Spot, 90, 30, 100, 0, 0);
        for (var i = 0; i < 60; i++)
        {
            hazards.Update(Step, players);
        }

        Assert.Equal(92f, thrower.Hp, 0);

        for (var i = 0; i < 60 * 20; i++)
        {
            hazards.Update(Step, players);
        }

        Assert.True(thrower.IsAlive);
        Assert.Equal(1f, thrower.Hp, 3);
    }

    [Fact]
    public void LevelTenMineTripleBlast_CostsThreeCappedHits()
    {
        var owner = CreatePlayer(0);
        var explosives = CreateExplosives();
        var mines = new MineSystem(explosives, new GameRandom(2), new NullAudioManager());
        var dummy = new PracticeTarget(Spot + new Vector2(30, 0), 20);
        mines.AddTarget(dummy);
        var players = new List<Player> { owner };

        mines.Place(new MineShot(Spot, 300, 120, 45, 0, 0, false, 0, "mine", 3, 0.15f, 30));
        for (var i = 0; i < 60; i++)
        {
            mines.Update(Step, players);
        }

        Assert.Equal(55f, owner.Hp);
    }

    [Fact]
    public void WhenAllowedToKill_OwnExplosionsCanFinishALowHpPlayer()
    {
        var lethal = new FriendlyFireConfig
        {
            Enabled = true, DamageToOther = 0.5f, SelfExplosiveDamage = 0.5f, MaxDamagePerExplosion = 30, CanKill = true,
        };
        var explosives = new ExplosiveSystem(TestArena, lethal, new GameRandom(1), new NullAudioManager());
        var partner = CreatePlayer(1);
        partner.TakeDamage(80);

        explosives.Detonate(Spot, 300, 120, 0, new List<Player> { partner });

        Assert.False(partner.IsAlive);
    }

    [Fact]
    public void ShippedRules_AreCapped_AndAPlayerAtFullHpSurvivesATripleBlast()
    {
        var friendlyFire = DataLoader.LoadAll(DataLoader.DefaultDirectory).Game.FriendlyFire;

        Assert.True(friendlyFire.MaxDamagePerExplosion * 3 < TestData.Game().Player.MaxHp);
        Assert.True(friendlyFire.MaxDamagePerExplosion > 0f);
        Assert.True(friendlyFire.MaxDamagePerSecond > 0f);
        Assert.Equal(friendlyFire.SelfExplosiveDamage, friendlyFire.DamageToOther);
    }

    private static ExplosiveSystem CreateExplosives()
    {
        return new ExplosiveSystem(TestArena, Capped, new GameRandom(1), new NullAudioManager());
    }

    private static Player CreatePlayer(int index, Vector2? position = null)
    {
        return new Player(index, new FakeInput(), TestData.Game().Player, new NullAudioManager(), position ?? Spot);
    }
}
