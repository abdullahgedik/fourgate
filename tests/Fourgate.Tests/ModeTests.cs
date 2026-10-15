using Fourgate.Core.Audio;
using Fourgate.Core.Data;
using Fourgate.Core.Input;
using Fourgate.Core.Rng;
using Fourgate.Entities;
using Fourgate.Entities.Pickups;
using Fourgate.Scenes;
using Fourgate.Systems;
using Microsoft.Xna.Framework;

namespace Fourgate.Tests;

public class ModeTests
{
    private const float Step = 1f / 60f;

    private static readonly ModesConfig Modes = new()
    {
        Fast = new FastModeConfig { UnlockWaveMultiplier = 0.5f, BudgetMultiplier = 1.5f, SpawnIntervalMultiplier = 0.75f },
        Nightmare = new NightmareModeConfig
        {
            DifficultyWaveOffset = 14, BudgetMultiplier = 1.3f, EnemySpeedMultiplier = 1.15f,
            HealthCrateChanceMultiplier = 0.6f, AllWeaponsAtStart = true,
        },
    };

    [Theory]
    [InlineData(1, 1)]
    [InlineData(3, 2)]
    [InlineData(14, 7)]
    [InlineData(18, 9)]
    public void Fast_HalvesUnlockWaves_RoundingUp(int normal, int fast)
    {
        var rules = new ModeRules(Modes, new RunMode(Fast: true, Nightmare: false));

        Assert.Equal(fast, rules.UnlockWave(normal));
    }

    [Fact]
    public void FastAndNightmare_Stack()
    {
        var both = new ModeRules(Modes, new RunMode(true, true));

        Assert.Equal(1.5f * 1.3f, both.BudgetMultiplier, 4);
        Assert.Equal(0.5f, both.UnlockWaveMultiplier);
        Assert.Equal(14, both.DifficultyWaveOffset);
        Assert.True(both.AllWeaponsAtStart);
        Assert.Equal("NIGHTMARE + FAST", both.Mode.Name);
        Assert.Equal(1f, ModeRules.Normal.BudgetMultiplier);
        Assert.Equal(0, ModeRules.Normal.DifficultyWaveOffset);
    }

    [Fact]
    public void Fast_BringsRangedZombiesInEarlier()
    {
        var shipped = DataLoader.LoadAll(DataLoader.DefaultDirectory);

        var normal = StartWave(shipped, 3, default);
        var fast = StartWave(shipped, 3, new RunMode(true, false));

        Assert.DoesNotContain(normal.Composition, e => e.Id == "ranged");
        Assert.Contains(fast.Composition, e => e.Id == "ranged");
        Assert.True(Cost(fast.Composition) > Cost(normal.Composition) * 1.3f);
    }

    [Fact]
    public void Nightmare_StartsWithTheWavesOfWaveFifteen_ButKeepsTheWaveCount()
    {
        var shipped = DataLoader.LoadAll(DataLoader.DefaultDirectory);

        var nightmare = StartWave(shipped, 1, new RunMode(false, true));

        Assert.Equal(1, nightmare.Wave);
        Assert.Contains(nightmare.Composition, e => e.Id == "elite");
        // Wave 15's budget, ×1.3, all spent (less than the cheapest enemy left over).
        var budget = WaveMath.Budget(15, shipped.Waves) * shipped.Modes.Nightmare.BudgetMultiplier;
        Assert.InRange(Cost(nightmare.Composition), budget - 1f, budget);
    }

    [Fact]
    public void Nightmare_LeavesBossWavesWhereTheyAre()
    {
        var shipped = DataLoader.LoadAll(DataLoader.DefaultDirectory);

        var nine = StartWave(shipped, 9, new RunMode(false, true));
        var ten = StartWave(shipped, 10, new RunMode(false, true));

        Assert.DoesNotContain(nine.Composition, e => e.IsBoss);
        Assert.Contains(ten.Composition, e => e.IsBoss);
    }

    [Fact]
    public void Nightmare_HandsEveryWeaponToEveryPlayer_AtTheStart()
    {
        var shipped = DataLoader.LoadAll(DataLoader.DefaultDirectory);

        var nightmare = new GameplayScene(
            shipped, new IPlayerInput[] { new FakeInput(), new FakeInput() }, new NullAudioManager(), new RunMode(false, true));
        var normal = new GameplayScene(shipped, new IPlayerInput[] { new FakeInput() }, new NullAudioManager());

        Assert.All(nightmare.Players, p => Assert.Equal(shipped.Weapons.Count, p.Weapons.Count));
        Assert.Single(normal.Players[0].Weapons);
    }

    [Fact]
    public void Nightmare_MakesHealthCratesRarer()
    {
        var crates = TestData.Game().Crates;
        var roll = crates.HealthChanceNormal * 0.8f;

        Assert.Equal(CrateType.Health, DropSystem.ChooseType(crates, false, 1f, roll));
        Assert.Equal(CrateType.Ammo, DropSystem.ChooseType(crates, false, 1f, roll, healthChanceMultiplier: 0.6f));
    }

    [Fact]
    public void Nightmare_MakesZombiesFaster()
    {
        var arena = new Arena(TestData.Arena());
        var enemies = new EnemySystem(new NullAudioManager()) { SpeedMultiplier = 1.15f };
        var player = new Player(0, new FakeInput(), TestData.Game().Player, new NullAudioManager(), new Vector2(1500, 1500));
        var zombie = enemies.Spawn(TestData.Zombie(), new Vector2(500, 1500));

        for (var i = 0; i < 60; i++)
        {
            enemies.Update(Step, new List<Player> { player }, arena);
        }

        // 110 a second at normal speed.
        Assert.InRange(zombie.Position.X, 500f + 110f * 1.15f - 1f, 500f + 110f * 1.15f + 1f);
    }

    [Fact]
    public void ShippedModes_MatchTheDesign()
    {
        var modes = DataLoader.LoadAll(DataLoader.DefaultDirectory).Modes;

        Assert.True(modes.Fast.UnlockWaveMultiplier < 1f);
        Assert.True(modes.Fast.BudgetMultiplier > 1f);
        Assert.True(modes.Fast.SpawnIntervalMultiplier < 1f);
        Assert.True(modes.Nightmare.DifficultyWaveOffset > 0);
        Assert.True(modes.Nightmare.EnemySpeedMultiplier > 1f);
        Assert.True(modes.Nightmare.HealthCrateChanceMultiplier < 1f);
        Assert.True(modes.Nightmare.AllWeaponsAtStart);
    }

    [Fact]
    public void BadModeValue_IsRejected()
    {
        const string json = """
            { "fast": { "unlockWaveMultiplier": 0, "budgetMultiplier": 1.5, "spawnIntervalMultiplier": 0.75 },
              "nightmare": { "difficultyWaveOffset": 14, "budgetMultiplier": 1.3, "enemySpeedMultiplier": 1.15,
                "healthCrateChanceMultiplier": 0.6, "allWeaponsAtStart": true } }
            """;

        var error = Assert.Throws<DataLoadException>(() => DataLoader.ParseModes(json, "modes.json"));

        Assert.Equal("fast.unlockWaveMultiplier", error.Field);
    }

    [Fact]
    public void StartScreen_SwitchesTheToggles_AndRemembersThem()
    {
        var input = new FakeInput { MenuDownPressed = true };
        var scene = new ModeSelectScene(input, TestData.Game());
        scene.Update(Step);

        // On the toggles row: Enter switches Fast on, right then Enter switches Nightmare on.
        input.MenuDownPressed = false;
        input.ConfirmPressed = true;
        scene.Update(Step);
        input.ConfirmPressed = false;
        input.MenuRightPressed = true;
        scene.Update(Step);
        input.MenuRightPressed = false;
        input.ConfirmPressed = true;
        scene.Update(Step);

        Assert.Equal(new RunMode(true, true), scene.Mode);
        Assert.Null(scene.ChosenPlayers);

        // Back up to the cards: Enter starts.
        input.ConfirmPressed = false;
        input.MenuUpPressed = true;
        scene.Update(Step);
        input.MenuUpPressed = false;
        input.ConfirmPressed = true;
        scene.Update(Step);
        Assert.Equal(1, scene.ChosenPlayers);

        var again = new ModeSelectScene(new FakeInput(), TestData.Game(), 1, scene.Mode);
        Assert.Equal(new RunMode(true, true), again.Mode);
    }

    private static float Cost(List<EnemyConfig> enemies)
    {
        var total = 0f;
        foreach (var enemy in enemies)
        {
            total += enemy.Cost;
        }

        return total;
    }

    private static (int Wave, List<EnemyConfig> Composition) StartWave(GameData shipped, int wave, RunMode mode)
    {
        var config = TestData.Game(startWave: wave);
        var data = new GameData(
            config, shipped.Weapons, shipped.Enemies, shipped.Waves, shipped.Upgrades, shipped.Bosses, shipped.Modes);
        var enemies = new EnemySystem(new NullAudioManager());
        var waves = new WaveSystem(data, enemies, new Arena(config.Arena), new GameRandom(7), new NullAudioManager())
        {
            Rules = new ModeRules(shipped.Modes, mode),
        };

        while (waves.Phase != WavePhase.Active)
        {
            waves.Update(Step);
        }

        var composition = new List<EnemyConfig>(waves.Composition);
        return (waves.Wave, composition);
    }
}
