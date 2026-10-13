using Fourgate.Core.Audio;
using Fourgate.Core.Data;
using Fourgate.Core.Rng;
using Fourgate.Entities;
using Fourgate.Entities.Pickups;
using Fourgate.Systems;
using Fourgate.Weapons;
using Microsoft.Xna.Framework;

namespace Fourgate.Tests;

public class WeaponHandlingTests
{
    private const float Step = 1f / 60f;

    private static readonly Arena TestArena = new(TestData.Arena());
    private static readonly Vector2 Center = new(1500, 1500);
    private static readonly WeaponConfig PistolConfig = TestData.Pistol();
    private static readonly WeaponConfig SmgConfig = Limited("smg", 2);
    private static readonly WeaponConfig ShotgunConfig = Limited("shotgun", 3);

    private readonly FakeInput _input = new();
    private readonly FakeShotHandler _shots = new();

    [Fact]
    public void Inventory_IsKeptInSlotOrder_WhateverThePickupOrder()
    {
        var player = CreatePlayer();
        player.GiveWeapon(Create(ShotgunConfig));
        player.GiveWeapon(Create(PistolConfig));
        player.GiveWeapon(Create(SmgConfig));

        Assert.Equal(new[] { "pistol", "smg", "shotgun" }, player.Weapons.Select(w => w.Config.Id));
    }

    [Fact]
    public void FirstWeapon_IsSelected_AndLaterPickupsDoNotSwitch()
    {
        var player = CreatePlayer();
        player.GiveWeapon(Create(PistolConfig));
        player.GiveWeapon(Create(SmgConfig));

        Assert.Equal("pistol", player.CurrentWeapon!.Config.Id);
    }

    [Fact]
    public void Cycle_MovesThroughTheWeapons_AndWrapsAround()
    {
        var player = PlayerWithThreeWeapons();

        Assert.Equal("smg", Cycle(player, 1));
        Assert.Equal("shotgun", Cycle(player, 1));
        Assert.Equal("pistol", Cycle(player, 1));
        Assert.Equal("shotgun", Cycle(player, -1));
    }

    [Fact]
    public void SlotKey_SelectsThatWeapon_AndIgnoresSlotsNotCarried()
    {
        var player = PlayerWithThreeWeapons();

        Assert.Equal("shotgun", PressSlot(player, 3));
        Assert.Equal("shotgun", PressSlot(player, 7));
        Assert.Equal("pistol", PressSlot(player, 1));
    }

    [Fact]
    public void Switching_CancelsTheReload_AndItsProgressIsLost()
    {
        var player = CreatePlayer();
        var pistol = Create(TestData.Pistol(magSize: 1));
        player.GiveWeapon(pistol);
        player.GiveWeapon(Create(SmgConfig));

        // Fire the only round so the pistol starts reloading, then switch away and back.
        _input.FirePressed = true;
        _input.FireHeld = true;
        Tick(player);
        _input.FirePressed = false;
        _input.FireHeld = false;
        Assert.True(pistol.IsReloading);

        Cycle(player, 1);
        Assert.False(pistol.IsReloading);
        Assert.Equal(0, pistol.Magazine);

        // Coming back to an empty weapon starts a fresh reload.
        Cycle(player, 1);
        Assert.True(pistol.IsReloading);
        Assert.InRange(pistol.ReloadProgress, 0f, 0.1f);
    }

    [Fact]
    public void OnlyTheWeaponInHand_Fires()
    {
        var player = PlayerWithThreeWeapons();
        Cycle(player, 1);

        _input.FireHeld = true;
        Tick(player);

        Assert.Equal(1, _shots.Count);
        Assert.Equal(11, player.Weapons[1].Magazine);
        Assert.Equal(12, player.Weapons[0].Magazine);
    }

    [Fact]
    public void WeaponCrate_GivesItsWeapon_OnlyToAPlayerWhoLacksIt()
    {
        var unlocks = TestData.Unlocks();
        var drops = new DropSystem(TestData.Crates(), new GameRandom(1), new NullAudioManager(), unlocks);
        var player = CreatePlayer();
        var players = new List<Player> { player };

        drops.SpawnWeaponCrate(SmgConfig, Center);
        drops.Update(Step, players);
        Assert.True(player.OwnsWeapon("smg"));
        Assert.Equal(0, drops.Count);

        drops.SpawnWeaponCrate(SmgConfig, Center);
        drops.Update(Step, players);
        Assert.Equal(1, drops.Count);
        Assert.Single(player.Weapons);
    }

    [Fact]
    public void WeaponCrate_NeverExpires()
    {
        var drops = new DropSystem(TestData.Crates(), new GameRandom(1), new NullAudioManager(), TestData.Unlocks());
        drops.SpawnWeaponCrate(SmgConfig, new Vector2(100, 100));
        var nobody = new List<Player>();

        for (var i = 0; i < 60 * 120; i++)
        {
            drops.Update(Step, nobody);
        }

        Assert.Equal(1, drops.Count);
        Assert.True(drops[0].IsVisible(5f));
    }

    [Fact]
    public void AmmoCrate_GivesAnUnlockedWeaponFirst_ThenAmmo()
    {
        var unlocks = TestData.Unlocks();
        unlocks.Unlock(PistolConfig);
        unlocks.Unlock(SmgConfig);
        var drops = new DropSystem(TestData.Crates(), new GameRandom(1), new NullAudioManager(), unlocks);
        var player = CreatePlayer();
        unlocks.Grant(player, PistolConfig);
        var players = new List<Player> { player };

        drops.Spawn(CrateType.Ammo, Center);
        drops.Update(Step, players);
        Assert.True(player.OwnsWeapon("smg"));
        Assert.Equal(0, drops.Count);

        // The SMG arrived full, so a second ammo crate has nothing to give.
        drops.Spawn(CrateType.Ammo, Center);
        drops.Update(Step, players);
        Assert.Equal(1, drops.Count);
    }

    [Fact]
    public void AmmoCrates_Drop_WhileAPlayerStillLacksAnUnlockedWeapon()
    {
        var unlocks = TestData.Unlocks();
        unlocks.Unlock(PistolConfig);
        unlocks.Unlock(SmgConfig);
        var drops = new DropSystem(TestData.Crates(), new GameRandom(4), new NullAudioManager(), unlocks);
        var player = CreatePlayer();
        unlocks.Grant(player, PistolConfig);
        var death = new Entities.Enemies.EnemyDeath(TestData.Zombie(dropChance: 1f), new Vector2(700, 900), 0);

        for (var i = 0; i < 50; i++)
        {
            drops.OnEnemyKilled(death, new List<Player> { player });
        }

        var ammo = 0;
        for (var i = 0; i < drops.Count; i++)
        {
            if (drops[i].Type == CrateType.Ammo)
            {
                ammo++;
            }
        }

        Assert.True(ammo > 20);
    }

    [Fact]
    public void Unlocks_AreKeptInSlotOrder_AndReportTheLowestMissingWeapon()
    {
        var unlocks = TestData.Unlocks();
        Assert.True(unlocks.Unlock(ShotgunConfig));
        Assert.True(unlocks.Unlock(PistolConfig));
        Assert.True(unlocks.Unlock(SmgConfig));
        Assert.False(unlocks.Unlock(SmgConfig));
        var player = CreatePlayer();
        unlocks.Grant(player, PistolConfig);

        Assert.Equal(new[] { "pistol", "smg", "shotgun" }, unlocks.Unlocked.Select(w => w.Id));
        Assert.Equal("smg", unlocks.FirstMissingFor(player)!.Id);
    }

    [Fact]
    public void Spread_ScattersShotsInsideTheCone()
    {
        var config = TestData.Pistol(automatic: true, magSize: 500, fireRate: 60, spreadDegrees: 10);
        var weapon = new Weapon(config, TestData.Game(), new NullAudioManager(), new GameRandom(5));
        _input.FireHeld = true;

        var smallest = float.MaxValue;
        var largest = float.MinValue;
        for (var i = 0; i < 300; i++)
        {
            weapon.Update(Step, _input, Vector2.Zero, Vector2.UnitX, 0, _shots);
            var angle = MathHelper.ToDegrees(MathF.Atan2(_shots.LastDirection.Y, _shots.LastDirection.X));
            smallest = Math.Min(smallest, angle);
            largest = Math.Max(largest, angle);
            Assert.Equal(1f, _shots.LastDirection.Length(), 3);
        }

        // Half the 10 degree cone to either side, and the shots actually use it.
        Assert.InRange(smallest, -5f, -3.5f);
        Assert.InRange(largest, 3.5f, 5f);
    }

    [Fact]
    public void OneShotKills_KillsWithASingleBullet()
    {
        var enemies = new EnemySystem(new NullAudioManager());
        var shots = new ShotSystem(TestArena) { OneShotKills = true };
        shots.AddSource(enemies);
        var players = new List<Player> { CreatePlayer() };
        var enemy = enemies.Spawn(TestData.Zombie(), new Vector2(2000, 1500));
        enemies.Update(Step, players, TestArena);

        shots.FireProjectile(
            new Vector2(1540, 1500), Vector2.UnitX, new ProjectileConfig { Speed = 6000, Radius = 4, Range = 1100 }, 1, 0);
        for (var i = 0; i < 10; i++)
        {
            shots.Update(Step);
        }

        Assert.False(enemy.IsAlive);
    }

    private static WeaponConfig Limited(string id, int slot)
    {
        return TestData.Pistol(automatic: true, reserveMax: 100, infiniteReserve: false, id: id, slot: slot);
    }

    private static Weapon Create(WeaponConfig config)
    {
        return new Weapon(config, TestData.Game(), new NullAudioManager());
    }

    private Player CreatePlayer()
    {
        return new Player(0, _input, TestData.Game().Player, new NullAudioManager(), Center);
    }

    private Player PlayerWithThreeWeapons()
    {
        var player = CreatePlayer();
        player.GiveWeapon(Create(PistolConfig));
        player.GiveWeapon(Create(SmgConfig));
        player.GiveWeapon(Create(ShotgunConfig));
        return player;
    }

    private void Tick(Player player)
    {
        player.Update(Step, TestArena, _shots);
    }

    private string Cycle(Player player, int direction)
    {
        _input.WeaponCycle = direction;
        Tick(player);
        _input.WeaponCycle = 0;
        return player.CurrentWeapon!.Config.Id;
    }

    private string PressSlot(Player player, int slot)
    {
        _input.WeaponSlotPressed = slot;
        Tick(player);
        _input.WeaponSlotPressed = 0;
        return player.CurrentWeapon!.Config.Id;
    }
}
