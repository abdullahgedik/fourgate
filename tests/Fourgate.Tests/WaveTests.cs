using Fourgate.Core.Audio;
using Fourgate.Core.Data;
using Fourgate.Core.Rng;
using Fourgate.Entities;
using Fourgate.Systems;
using Microsoft.Xna.Framework;

namespace Fourgate.Tests;

public class WaveTests
{
    private const float Step = 1f / 60f;

    private static readonly WavesConfig Waves = TestData.Waves();

    [Theory]
    [InlineData(1, 8.24f)]
    [InlineData(10, 32f)]
    [InlineData(50, 216f)]
    [InlineData(500, 11106f)]
    public void Budget_FollowsTheCurve_ForAnyWave(int wave, float expected)
    {
        Assert.Equal(expected, WaveMath.Budget(wave, Waves), 1);
    }

    [Fact]
    public void SpawnInterval_Shrinks_ThenStaysAtTheMinimum()
    {
        Assert.Equal(1.2f, WaveMath.SpawnInterval(1, Waves), 3);
        Assert.Equal(0.93f, WaveMath.SpawnInterval(10, Waves), 3);
        Assert.Equal(0.35f, WaveMath.SpawnInterval(100, Waves), 3);
        Assert.Equal(0.35f, WaveMath.SpawnInterval(100000, Waves), 3);
    }

    [Fact]
    public void GroupSize_Grows_ThenStaysAtTheMaximum()
    {
        Assert.Equal(2, WaveMath.GroupSize(1, Waves));
        Assert.Equal(3, WaveMath.GroupSize(8, Waves));
        Assert.Equal(6, WaveMath.GroupSize(1000, Waves));
    }

    [Fact]
    public void Compose_SpendsTheBudgetOnWholeEnemies()
    {
        var result = new List<EnemyConfig>();

        WaveMath.Compose(1, 8.24f, new[] { TestData.Zombie() }, 0.5f, new GameRandom(1), result);

        Assert.Equal(8, result.Count);
    }

    [Fact]
    public void Compose_UsesOnlyUnlockedTypes()
    {
        var enemies = new[] { TestData.Zombie(), TestData.Zombie("tank", cost: 6, unlockWave: 8) };
        var result = new List<EnemyConfig>();

        WaveMath.Compose(3, 100f, enemies, 0.5f, new GameRandom(1), result);

        Assert.All(result, enemy => Assert.Equal("normal", enemy.Id));
    }

    [Fact]
    public void Compose_NeverOverspends_AndLeavesLessThanTheCheapestCost()
    {
        var enemies = new[]
        {
            TestData.Zombie(),
            TestData.Zombie("fast", cost: 1.5f, unlockWave: 3),
            TestData.Zombie("tank", cost: 6, unlockWave: 8),
        };
        var result = new List<EnemyConfig>();

        WaveMath.Compose(20, 66f, enemies, 0.5f, new GameRandom(7), result);

        var spent = result.Sum(enemy => enemy.Cost);
        Assert.InRange(spent, 65.01f, 66f);
        Assert.Contains(result, enemy => enemy.Id == "tank");
    }

    [Fact]
    public void Compose_FavorsNewerTypes()
    {
        var enemies = new[] { TestData.Zombie(), TestData.Zombie("newer", cost: 1, unlockWave: 5) };
        var result = new List<EnemyConfig>();

        WaveMath.Compose(10, 2000f, enemies, 2f, new GameRandom(3), result);

        // Weights are 1 and 3, so about three quarters should be the newer type.
        var newer = result.Count(enemy => enemy.Id == "newer");
        Assert.InRange(newer / (float)result.Count, 0.68f, 0.82f);
    }

    [Fact]
    public void Compose_SameSeed_GivesTheSameWave()
    {
        var enemies = new[] { TestData.Zombie(), TestData.Zombie("fast", cost: 1.5f, unlockWave: 3) };
        var first = new List<EnemyConfig>();
        var second = new List<EnemyConfig>();

        WaveMath.Compose(12, 40f, enemies, 0.5f, new GameRandom(99), first);
        WaveMath.Compose(12, 40f, enemies, 0.5f, new GameRandom(99), second);

        Assert.Equal(first, second);
    }

    [Fact]
    public void Run_StartsWithARest_ThenSpawnsTheWholeWave()
    {
        var game = new Game();

        Assert.Equal(WavePhase.Rest, game.Waves.Phase);
        Assert.Equal(1, game.Waves.Wave);

        game.Run(seconds: 4.9f);
        Assert.Equal(WavePhase.Rest, game.Waves.Phase);
        Assert.Equal(0, game.Enemies.Count);

        game.Run(seconds: 0.2f);
        Assert.Equal(WavePhase.Active, game.Waves.Phase);
        Assert.Equal(8, game.Waves.EnemiesRemaining);

        game.Run(seconds: 10f);
        Assert.Equal(8, game.Enemies.Count);
        Assert.True(game.Waves.Spawner.IsFinished);
    }

    [Fact]
    public void Door_GlowsBeforeItsGroupEmerges()
    {
        var game = new Game();
        game.Run(seconds: 5.1f);

        // The first group has been announced but is still behind its door.
        Assert.Equal(0, game.Enemies.Count);
        Assert.Contains(Enumerable.Range(0, 4), door => game.Waves.Spawner.GetDoorGlow(door) > 0f);

        game.Run(seconds: 0.8f);
        Assert.Equal(2, game.Enemies.Count);
    }

    [Fact]
    public void Enemies_EmergeJustInsideADoor()
    {
        var game = new Game();
        game.Run(seconds: 6f);

        var arena = game.Arena;
        for (var i = 0; i < game.Enemies.Count; i++)
        {
            var position = game.Enemies[i].Position;
            var edgeDistance = Math.Min(
                Math.Min(position.X - arena.Left, arena.Right - position.X),
                Math.Min(position.Y - arena.Top, arena.Bottom - position.Y));
            Assert.InRange(edgeDistance, 19f, 60f);
        }
    }

    [Fact]
    public void Wave_EndsWhenEveryEnemyIsDead_AndTheNextOneFollowsTheRest()
    {
        var game = new Game();
        game.Run(seconds: 20f);
        Assert.Equal(WavePhase.Active, game.Waves.Phase);

        game.KillAll();
        game.Run(seconds: 0.1f);
        Assert.Equal(WavePhase.Rest, game.Waves.Phase);
        Assert.Equal(2, game.Waves.Wave);
        Assert.InRange(game.Waves.RestSecondsLeft, 4.8f, 5f);

        game.Run(seconds: 5.1f);
        Assert.Equal(WavePhase.Active, game.Waves.Phase);

        // Wave 2 budget is 10.56, so ten zombies.
        Assert.Equal(10, game.Waves.EnemiesRemaining);
    }

    [Fact]
    public void Wave_DoesNotEndWhileEnemiesAreStillWaitingToSpawn()
    {
        var game = new Game();
        game.Run(seconds: 6f);

        game.KillAll();
        game.Run(seconds: 0.1f);

        Assert.Equal(WavePhase.Active, game.Waves.Phase);
        Assert.Equal(1, game.Waves.Wave);
    }

    [Fact]
    public void MaxAlive_HoldsFurtherGroupsBehindTheirDoors()
    {
        var game = new Game(maxAlive: 4);
        game.Run(seconds: 30f);

        Assert.Equal(4, game.Enemies.Count);
        Assert.Equal(8, game.Waves.EnemiesRemaining);

        game.KillAll();
        game.Run(seconds: 5f);
        Assert.Equal(4, game.Enemies.Count);
        Assert.Equal(4, game.Waves.EnemiesRemaining);
    }

    [Fact]
    public void StartWave_BeginsTheRunAtThatWave()
    {
        var game = new Game(startWave: 10);

        game.Run(seconds: 5.1f);

        Assert.Equal(10, game.Waves.Wave);
        Assert.Equal(32, game.Waves.EnemiesRemaining);
    }

    /// <summary>A wave system wired to real enemies, with no players.</summary>
    private sealed class Game
    {
        private readonly List<Player> _players = new();

        public Game(int maxAlive = 60, int startWave = 1)
        {
            var config = TestData.Game(maxAlive: maxAlive, startWave: startWave);
            var data = new GameData(
                config, new[] { TestData.Pistol() }, new[] { TestData.Zombie() }, TestData.Waves());
            var audio = new NullAudioManager();

            Arena = new Arena(config.Arena);
            Enemies = new EnemySystem(audio);
            Waves = new WaveSystem(data, Enemies, Arena, new GameRandom(5), audio);
        }

        public Arena Arena { get; }

        public EnemySystem Enemies { get; }

        public WaveSystem Waves { get; }

        public void Run(float seconds)
        {
            var steps = (int)MathF.Round(seconds / Step);
            for (var i = 0; i < steps; i++)
            {
                Waves.Update(Step);
                Enemies.Update(Step, _players, Arena);
            }
        }

        public void KillAll()
        {
            for (var i = 0; i < Enemies.Count; i++)
            {
                Enemies[i].Hit(100000, 0);
            }
        }
    }
}
