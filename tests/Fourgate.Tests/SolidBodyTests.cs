using Fourgate.Core.Audio;
using Fourgate.Core.Data;
using Fourgate.Entities;
using Fourgate.Systems;
using Microsoft.Xna.Framework;

namespace Fourgate.Tests;

public class SolidBodyTests
{
    private const float Step = 1f / 60f;

    private static readonly Arena TestArena = new(TestData.Arena());
    private static readonly Vector2 Start = new(1000, 1500);

    [Fact]
    public void WalkingPlayer_IsStoppedByAnEnemy_WhichDoesNotMove()
    {
        var (enemies, enemy, player, input) = Setup();
        input.Move = Vector2.UnitX;

        Run(enemies, player, 60);

        Assert.Equal(enemy.Position.X - enemy.Radius - player.Radius, player.Position.X, 1);
        Assert.Equal(Start.X + 100, enemy.Position.X, 1);
    }

    [Fact]
    public void DashingPlayer_PassesThroughAnEnemy()
    {
        var (enemies, enemy, player, input) = Setup();
        input.Move = Vector2.UnitX;
        input.DashPressed = true;

        Run(enemies, player, 1);
        input.DashPressed = false;
        input.Move = Vector2.Zero;
        Run(enemies, player, 30);

        Assert.True(player.Position.X > enemy.Position.X + enemy.Radius);
        Assert.Equal(Start.X + 100, enemy.Position.X, 1);
    }

    [Fact]
    public void DashPressedWhileRecharging_ShowsNotReady_ThenFades()
    {
        var input = new FakeInput { DashPressed = true };
        var player = new Player(0, input, TestData.Game().Player, new NullAudioManager(), Start);
        player.Update(Step, TestArena, new FakeShotHandler());
        input.DashPressed = false;
        for (var i = 0; i < 20; i++)
        {
            player.Update(Step, TestArena, new FakeShotHandler());
        }

        Assert.Equal(0f, player.DashRefused);

        input.DashPressed = true;
        player.Update(Step, TestArena, new FakeShotHandler());
        Assert.True(player.DashRefused > 0.9f);

        input.DashPressed = false;
        for (var i = 0; i < 60; i++)
        {
            player.Update(Step, TestArena, new FakeShotHandler());
        }

        Assert.Equal(0f, player.DashRefused);
    }

    private static (EnemySystem Enemies, Entities.Enemies.Enemy Enemy, Player Player, FakeInput Input) Setup()
    {
        var enemies = new EnemySystem(new NullAudioManager());
        var enemy = enemies.Spawn(Statue(), Start + new Vector2(100, 0));
        var input = new FakeInput();
        var player = new Player(0, input, TestData.Game().Player, new NullAudioManager(), Start);
        return (enemies, enemy, player, input);
    }

    // Enemies first, then the player moves and is blocked, as in the game.
    private static void Run(EnemySystem enemies, Player player, int steps)
    {
        var players = new List<Player> { player };
        for (var i = 0; i < steps; i++)
        {
            enemies.Update(Step, players, TestArena);
            player.Update(Step, TestArena, new FakeShotHandler());
            enemies.BlockPlayer(player, TestArena);
        }
    }

    // Stands still and never attacks, so only the bodies meet.
    private static EnemyConfig Statue()
    {
        return new EnemyConfig
        {
            Id = "statue", DisplayName = "Statue", Hp = 1000, Speed = 0, Radius = 20, ContactDamage = 0,
            ContactCooldown = 0.8f, Xp = 1, ScoreValue = 1, Cost = 1, UnlockWave = 1, DropChance = 0f,
        };
    }
}
