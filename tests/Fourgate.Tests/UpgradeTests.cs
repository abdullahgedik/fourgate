using Fourgate.Core.Audio;
using Fourgate.Core.Data;
using Fourgate.Core.Rng;
using Fourgate.Entities;
using Fourgate.Entities.Pickups;
using Fourgate.Systems;
using Fourgate.Weapons;
using Microsoft.Xna.Framework;

namespace Fourgate.Tests;

public class UpgradeTests
{
    private const float Step = 1f / 60f;

    private static readonly Arena TestArena = new(TestData.Arena());
    private static readonly Vector2 Center = new(1500, 1500);

    [Fact]
    public void Offer_HasThreeDistinctCards()
    {
        var upgrades = CreateSystem();
        var players = PlayersWithPicks(1);

        Assert.True(upgrades.OpenOffers(players));

        var offer = upgrades.GetOffer(0);
        Assert.Equal(3, offer.Count);
        Assert.Equal(3, offer.Select(card => card.Upgrade!.Id).Distinct().Count());
    }

    [Fact]
    public void Offer_IsNotOpened_WithoutABankedPick()
    {
        var upgrades = CreateSystem();
        var players = new List<Player> { CreatePlayer(new FakeInput()) };

        Assert.False(upgrades.OpenOffers(players));
        Assert.Empty(upgrades.GetOffer(0));
    }

    [Fact]
    public void Offer_StaysTheSame_UntilACardIsChosen()
    {
        var upgrades = CreateSystem();
        var players = PlayersWithPicks(1);
        upgrades.OpenOffers(players);
        var first = upgrades.GetOffer(0).ToList();

        upgrades.OpenOffers(players);

        Assert.Equal(first, upgrades.GetOffer(0));
    }

    [Fact]
    public void Offer_LeavesOutMaxedUpgrades()
    {
        var config = TestData.Upgrades();
        var upgrades = new UpgradeSystem(config, TestData.Unlocks(), new GameRandom(3), new NullAudioManager());
        var players = PlayersWithPicks(1);
        MaxOut(players[0], config, "maxHp");
        MaxOut(players[0], config, "moveSpeed");

        upgrades.OpenOffers(players);

        var ids = upgrades.GetOffer(0).Select(card => card.Upgrade!.Id).OrderBy(id => id).ToList();
        Assert.Equal(new[] { "ammoCapacity", "dashCooldown", "pickupRange" }, ids);
    }

    [Fact]
    public void Offer_ShowsFewerCards_WhenFewUpgradesAreLeft()
    {
        var config = TestData.Upgrades();
        var upgrades = new UpgradeSystem(config, TestData.Unlocks(), new GameRandom(3), new NullAudioManager());
        var players = PlayersWithPicks(1);
        foreach (var id in new[] { "maxHp", "moveSpeed", "dashCooldown", "ammoCapacity" })
        {
            MaxOut(players[0], config, id);
        }

        upgrades.OpenOffers(players);

        var card = Assert.Single(upgrades.GetOffer(0));
        Assert.Equal("pickupRange", card.Upgrade!.Id);
    }

    [Fact]
    public void Offer_FallsBackToAFullHeal_WhenEverythingIsMaxed()
    {
        var config = TestData.Upgrades();
        var upgrades = new UpgradeSystem(config, TestData.Unlocks(), new GameRandom(3), new NullAudioManager());
        var players = PlayersWithPicks(1);
        foreach (var upgrade in config.Upgrades)
        {
            MaxOut(players[0], config, upgrade.Id);
        }

        players[0].TakeDamage(90);
        upgrades.OpenOffers(players);
        var card = Assert.Single(upgrades.GetOffer(0));
        Assert.Null(card.Upgrade);

        upgrades.Choose(players[0], 0, 0);

        Assert.Equal(players[0].MaxHp, players[0].Hp);
        Assert.Equal(0, players[0].Progression.BankedPicks);
    }

    [Fact]
    public void Choose_AppliesTheCard_SpendsThePick_AndClosesTheOffer()
    {
        var upgrades = CreateSystem();
        var players = PlayersWithPicks(1);
        upgrades.OpenOffers(players);
        var chosen = upgrades.GetOffer(0)[1].Upgrade!;

        upgrades.Choose(players[0], 0, 1);

        Assert.Equal(1, players[0].GetUpgradeLevel(chosen.Id));
        Assert.Equal(0, players[0].Progression.BankedPicks);
        Assert.False(upgrades.AnyOfferOpen);
    }

    [Fact]
    public void SeveralPicks_AreOfferedOneAfterAnother()
    {
        var upgrades = CreateSystem();
        var players = PlayersWithPicks(1, picks: 2);

        upgrades.OpenOffers(players);
        upgrades.Choose(players[0], 0, 0);
        Assert.True(upgrades.OpenOffers(players));

        upgrades.Choose(players[0], 0, 0);
        Assert.False(upgrades.OpenOffers(players));
    }

    [Fact]
    public void Players_PickIndependently()
    {
        var upgrades = CreateSystem();
        var players = PlayersWithPicks(2);
        upgrades.OpenOffers(players);

        upgrades.Choose(players[0], 0, 0);

        Assert.Empty(upgrades.GetOffer(0));
        Assert.Equal(3, upgrades.GetOffer(1).Count);
        Assert.True(upgrades.AnyOfferOpen);
        Assert.Equal(1, players[1].Progression.BankedPicks);
    }

    [Fact]
    public void SameSeed_DealsTheSameCards()
    {
        var first = new UpgradeSystem(TestData.Upgrades(), TestData.Unlocks(), new GameRandom(21), new NullAudioManager());
        var second = new UpgradeSystem(TestData.Upgrades(), TestData.Unlocks(), new GameRandom(21), new NullAudioManager());
        first.OpenOffers(PlayersWithPicks(1));
        second.OpenOffers(PlayersWithPicks(1));

        Assert.Equal(
            first.GetOffer(0).Select(card => card.Upgrade!.Id),
            second.GetOffer(0).Select(card => card.Upgrade!.Id));
    }

    [Fact]
    public void MaxHpUpgrade_RaisesMaxHp_AndHealsTheSameAmount()
    {
        var player = CreatePlayer(new FakeInput());
        player.TakeDamage(50);

        player.ApplyUpgrade(TestData.Upgrade("maxHp", UpgradeStat.MaxHp, 20, 5));

        Assert.Equal(120f, player.MaxHp);
        Assert.Equal(70f, player.Hp);
    }

    [Fact]
    public void MoveSpeedUpgrade_StacksPerLevel()
    {
        var input = new FakeInput { Move = Vector2.UnitX };
        var player = CreatePlayer(input);
        var upgrade = TestData.Upgrade("moveSpeed", UpgradeStat.MoveSpeed, 0.06f, 5);
        player.ApplyUpgrade(upgrade);
        player.ApplyUpgrade(upgrade);

        for (var i = 0; i < 60; i++)
        {
            player.Update(Step, TestArena, new FakeShotHandler());
        }

        // 300 x 1.12 over one second.
        Assert.Equal(1836f, player.Position.X, 0);
    }

    [Fact]
    public void DashCooldownUpgrade_ShortensTheCooldown()
    {
        var input = new FakeInput { DashPressed = true };
        var player = CreatePlayer(input);
        player.ApplyUpgrade(TestData.Upgrade("dashCooldown", UpgradeStat.DashCooldown, 0.10f, 5));

        player.Update(Step, TestArena, new FakeShotHandler());

        Assert.Equal(1.8f, player.DashCooldown, 3);
        Assert.InRange(player.DashCooldownLeft, 1.75f, 1.8f);
    }

    [Fact]
    public void AmmoCapacityUpgrade_RaisesReserveMax_ForCurrentAndFutureWeapons()
    {
        var player = CreatePlayer(new FakeInput());
        var owned = LimitedWeapon(240);
        player.GiveWeapon(owned);

        player.ApplyUpgrade(TestData.Upgrade("ammoCapacity", UpgradeStat.AmmoCapacity, 0.10f, 5));
        var later = LimitedWeapon(48);
        player.GiveWeapon(later);

        Assert.Equal(264, owned.ReserveMax);
        Assert.Equal(53, later.ReserveMax);
        Assert.True(owned.NeedsAmmo);
    }

    [Fact]
    public void PickupRangeUpgrade_ReachesCratesFurtherAway()
    {
        var drops = new DropSystem(TestData.Crates(), new GameRandom(1), new NullAudioManager(), TestData.Unlocks());
        var player = CreatePlayer(new FakeInput());
        var players = new List<Player> { player };
        drops.Spawn(CrateType.Upgrade, Center + new Vector2(46, 0));

        drops.Update(Step, players);
        Assert.Equal(1, drops.Count);

        // 40 x 1.2 = 48.
        player.ApplyUpgrade(TestData.Upgrade("pickupRange", UpgradeStat.PickupRange, 0.20f, 3));
        drops.Update(Step, players);
        Assert.Equal(0, drops.Count);
    }

    [Fact]
    public void Screen_PlaysThePickBeforeApplyingIt_ThenDealsTheNextPick_ThenFadesOut()
    {
        var upgrades = CreateSystem();
        var input = new FakeInput();
        var player = CreatePlayer(input);
        player.Progression.AddPick();
        player.Progression.AddPick();
        var players = new List<Player> { player };
        var cameras = new List<Fourgate.Core.Rendering.Camera2D>
        {
            new(new Rectangle(0, 0, 1920, 1080), TestData.Game().Camera),
        };
        var screen = new Fourgate.UI.UpgradeScreen(upgrades);

        void Frames(int count)
        {
            for (var i = 0; i < count; i++)
            {
                if (upgrades.OpenOffers(players))
                {
                    screen.Update(Step, players, cameras);
                }
                else
                {
                    screen.Tick(Step);
                }
            }
        }

        // Cards come in; input is ignored until they have arrived.
        Frames(1);
        input.ConfirmPressed = true;
        Frames(1);
        input.ConfirmPressed = false;
        Assert.Equal(2, player.Progression.BankedPicks);

        Frames(30);
        input.ConfirmPressed = true;
        Frames(1);
        input.ConfirmPressed = false;

        // Chosen, but it only takes effect once the card has played out its moment.
        Assert.Equal(2, player.Progression.BankedPicks);
        Frames(30);
        Assert.Equal(1, player.Progression.BankedPicks);
        Assert.True(upgrades.AnyOfferOpen);

        // The second pick, then the screen fades out after play resumes.
        Frames(30);
        input.ConfirmPressed = true;
        Frames(1);
        input.ConfirmPressed = false;
        Frames(30);
        Assert.Equal(0, player.Progression.BankedPicks);
        Assert.False(upgrades.AnyOfferOpen);
        Assert.True(screen.IsAnimating);
        Frames(30);
        Assert.False(screen.IsAnimating);
    }

    private static UpgradeSystem CreateSystem()
    {
        return new UpgradeSystem(TestData.Upgrades(), TestData.Unlocks(), new GameRandom(8), new NullAudioManager());
    }

    private static List<Player> PlayersWithPicks(int count, int picks = 1)
    {
        var players = new List<Player>();
        for (var i = 0; i < count; i++)
        {
            var player = new Player(i, new FakeInput(), TestData.Game().Player, new NullAudioManager(), Center);
            for (var p = 0; p < picks; p++)
            {
                player.Progression.AddPick();
            }

            players.Add(player);
        }

        return players;
    }

    private static Player CreatePlayer(FakeInput input)
    {
        return new Player(0, input, TestData.Game().Player, new NullAudioManager(), Center);
    }

    private static Weapon LimitedWeapon(int reserveMax)
    {
        return new Weapon(
            TestData.Pistol(reserveMax: reserveMax, infiniteReserve: false), TestData.Game(), new NullAudioManager());
    }

    private static void MaxOut(Player player, UpgradesConfig config, string id)
    {
        var upgrade = config.Upgrades.Single(u => u.Id == id);
        for (var i = 0; i < upgrade.MaxLevel; i++)
        {
            player.ApplyUpgrade(upgrade);
        }
    }
}
