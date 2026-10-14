using Fourgate.Core.Audio;
using Fourgate.Core.Data;
using Fourgate.Entities;
using Fourgate.Weapons;
using Microsoft.Xna.Framework;

namespace Fourgate.Tests;

public class EquipmentTests
{
    private const float Step = 1f / 60f;

    private static readonly Arena TestArena = new(TestData.Arena());
    private static readonly Vector2 Center = new(1500, 1500);

    private readonly FakeInput _input = new();
    private readonly FakeShotHandler _shots = new();

    [Fact]
    public void Equipment_BecomesActive_WithoutReplacingTheGunInHand()
    {
        var player = CreatePlayer();
        player.GiveWeapon(Create(TestData.Pistol()));
        player.GiveWeapon(Create(Grenade()));

        Assert.Equal("pistol", player.CurrentWeapon!.Config.Id);
        Assert.Equal("grenade", player.ActiveEquipment!.Config.Id);
    }

    [Fact]
    public void EquipmentButton_ThrowsTheActiveEquipment_AndTheGunStaysInHand()
    {
        var player = PlayerWithPistolAndGrenade();

        _input.EquipmentPressed = true;
        _input.EquipmentHeld = true;
        Tick(player);

        Assert.Single(_shots.Throws);
        Assert.Equal(0, _shots.Count);
        Assert.Equal("pistol", player.CurrentWeapon!.Config.Id);
        Assert.Equal(9, player.ActiveEquipment!.Reserve);
    }

    [Fact]
    public void FireButton_NeverUsesEquipment()
    {
        var player = PlayerWithPistolAndGrenade();

        _input.FirePressed = true;
        _input.FireHeld = true;
        Tick(player);

        Assert.Equal(1, _shots.Count);
        Assert.Empty(_shots.Throws);
    }

    [Fact]
    public void ShootingAndThrowing_WorkInTheSameMoment()
    {
        var player = PlayerWithPistolAndGrenade();

        _input.FirePressed = true;
        _input.FireHeld = true;
        _input.EquipmentPressed = true;
        _input.EquipmentHeld = true;
        Tick(player);

        Assert.Equal(1, _shots.Count);
        Assert.Single(_shots.Throws);
    }

    [Fact]
    public void Throwing_DoesNotInterruptAReload()
    {
        var player = CreatePlayer();
        var pistol = Create(TestData.Pistol(magSize: 1));
        player.GiveWeapon(pistol);
        player.GiveWeapon(Create(Grenade()));

        _input.FirePressed = true;
        _input.FireHeld = true;
        Tick(player);
        _input.FirePressed = false;
        _input.FireHeld = false;
        Assert.True(pistol.IsReloading);

        _input.EquipmentPressed = true;
        _input.EquipmentHeld = true;
        Tick(player);

        Assert.Single(_shots.Throws);
        Assert.True(pistol.IsReloading);
    }

    [Fact]
    public void EquipmentCycle_MovesThroughEquipmentOnly_AndWrapsAround()
    {
        var player = CreatePlayer();
        player.GiveWeapon(Create(TestData.Pistol()));
        player.GiveWeapon(Create(TestData.Pistol(id: "smg", slot: 2)));
        player.GiveWeapon(Create(Grenade()));
        player.GiveWeapon(Create(Grenade("molotov", 2)));
        player.GiveWeapon(Create(Grenade("mine", 3)));

        Assert.Equal("molotov", CycleEquipment(player, 1));
        Assert.Equal("mine", CycleEquipment(player, 1));
        Assert.Equal("grenade", CycleEquipment(player, 1));
        Assert.Equal("mine", CycleEquipment(player, -1));
        Assert.Equal("pistol", player.CurrentWeapon!.Config.Id);
    }

    [Fact]
    public void GunCycle_SkipsEquipment()
    {
        var player = CreatePlayer();
        player.GiveWeapon(Create(TestData.Pistol()));
        player.GiveWeapon(Create(Grenade()));
        player.GiveWeapon(Create(TestData.Pistol(id: "smg", slot: 2)));

        _input.WeaponCycle = 1;
        Tick(player);
        _input.WeaponCycle = 0;
        Assert.Equal("smg", player.CurrentWeapon!.Config.Id);

        _input.WeaponCycle = 1;
        Tick(player);
        Assert.Equal("pistol", player.CurrentWeapon!.Config.Id);
    }

    [Fact]
    public void NumberKey_SelectsTheGunWithThatSlot_NotTheEquipmentWithTheSameNumber()
    {
        var player = CreatePlayer();
        player.GiveWeapon(Create(TestData.Pistol(id: "smg", slot: 2)));
        player.GiveWeapon(Create(TestData.Pistol()));
        player.GiveWeapon(Create(Grenade()));

        _input.WeaponSlotPressed = 1;
        Tick(player);

        Assert.Equal("pistol", player.CurrentWeapon!.Config.Id);
        Assert.Equal("grenade", player.ActiveEquipment!.Config.Id);
    }

    [Fact]
    public void Inventory_ListsGunsBeforeEquipment()
    {
        var player = CreatePlayer();
        player.GiveWeapon(Create(Grenade()));
        player.GiveWeapon(Create(TestData.Pistol(id: "smg", slot: 2)));
        player.GiveWeapon(Create(TestData.Pistol()));

        Assert.Equal(new[] { "pistol", "smg", "grenade" }, player.Weapons.Select(w => w.Config.Id));
    }

    [Fact]
    public void SameSlotNumber_IsAllowedOnceForGunsAndOnceForEquipment()
    {
        const string json = """
            {
              "weapons": [
                {
                  "id": "pistol", "displayName": "Pistol", "slot": 1, "unlockWave": 1, "type": "projectile",
                  "damage": 15, "fireRate": 3, "automatic": false,
                  "ammo": { "magSize": 12, "reserveMax": 0, "reloadTime": 0.8, "infiniteReserve": true },
                  "projectile": { "speed": 6000, "radius": 4, "range": 1100 }
                },
                {
                  "id": "grenade", "displayName": "Grenade", "category": "equipment", "slot": 1, "unlockWave": 5,
                  "type": "thrown", "damage": 60, "fireRate": 1.25, "automatic": false,
                  "ammo": { "usesMagazine": false, "reserveMax": 10, "infiniteReserve": false },
                  "thrown": { "speed": 900, "maxRange": 650, "radius": 10 },
                  "explosion": { "radius": 110 }
                }
              ]
            }
            """;

        var weapons = DataLoader.ParseWeapons(json, "weapons.json");

        Assert.Equal(2, weapons.Count);
    }

    [Fact]
    public void GunSlot_AboveNine_IsRejected()
    {
        const string json = """
            {
              "weapons": [
                {
                  "id": "pistol", "displayName": "Pistol", "slot": 10, "unlockWave": 1, "type": "projectile",
                  "damage": 15, "fireRate": 3, "automatic": false,
                  "ammo": { "magSize": 12, "reserveMax": 0, "reloadTime": 0.8, "infiniteReserve": true },
                  "projectile": { "speed": 6000, "radius": 4, "range": 1100 }
                }
              ]
            }
            """;

        var error = Assert.Throws<DataLoadException>(() => DataLoader.ParseWeapons(json, "weapons.json"));

        Assert.Equal("weapons[0].slot", error.Field);
    }

    [Fact]
    public void ShippedGrenade_IsEquipment_AndGunSlotsAreUniqueKeys()
    {
        var weapons = DataLoader.LoadAll(DataLoader.DefaultDirectory).Weapons;

        Assert.Equal(WeaponCategory.Equipment, weapons.Single(w => w.Id == "grenade").Category);
        var gunSlots = weapons.Where(w => w.Category == WeaponCategory.Gun).Select(w => w.Slot).ToList();
        Assert.All(gunSlots, slot => Assert.InRange(slot, 1, 9));
        Assert.Equal(gunSlots.Count, gunSlots.Distinct().Count());
    }

    private static WeaponConfig Grenade(string id = "grenade", int slot = 1)
    {
        return new WeaponConfig
        {
            Id = id,
            DisplayName = id,
            Category = WeaponCategory.Equipment,
            Slot = slot,
            UnlockWave = 5,
            Type = WeaponType.Thrown,
            Damage = 60,
            FireRate = 1.25f,
            Automatic = false,
            Ammo = new WeaponAmmoConfig { UsesMagazine = false, ReserveMax = 10, InfiniteReserve = false },
            Thrown = new ThrownConfig { Speed = 900, MaxRange = 650, Radius = 10 },
            Explosion = new ExplosionConfig { Radius = 110 },
        };
    }

    private static Weapon Create(WeaponConfig config)
    {
        return new Weapon(config, TestData.Game(), new NullAudioManager());
    }

    private Player CreatePlayer()
    {
        return new Player(0, _input, TestData.Game().Player, new NullAudioManager(), Center);
    }

    private Player PlayerWithPistolAndGrenade()
    {
        var player = CreatePlayer();
        player.GiveWeapon(Create(TestData.Pistol()));
        player.GiveWeapon(Create(Grenade()));
        return player;
    }

    private void Tick(Player player)
    {
        player.Update(Step, TestArena, _shots);
    }

    private string CycleEquipment(Player player, int direction)
    {
        _input.EquipmentCycle = direction;
        Tick(player);
        _input.EquipmentCycle = 0;
        return player.ActiveEquipment!.Config.Id;
    }
}
