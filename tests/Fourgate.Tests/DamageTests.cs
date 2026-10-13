using Fourgate.Core.Audio;
using Fourgate.Entities;
using Fourgate.Systems;
using Microsoft.Xna.Framework;

namespace Fourgate.Tests;

public class DamageTests
{
    private const float Step = 1f / 60f;

    private static readonly Arena TestArena = new(TestData.Arena());
    private static readonly FakeShotHandler Shots = new();
    private static readonly Vector2 Center = new(1500, 1500);

    [Fact]
    public void Player_StartsAtFullHp()
    {
        var player = CreatePlayer(new FakeInput());

        Assert.Equal(100f, player.Hp);
        Assert.True(player.IsAlive);
    }

    [Fact]
    public void TakeDamage_ReducesHp()
    {
        var player = CreatePlayer(new FakeInput());

        Assert.True(player.TakeDamage(30));

        Assert.Equal(70f, player.Hp);
    }

    [Fact]
    public void TakeDamage_BeyondHp_KillsWithoutGoingNegative()
    {
        var player = CreatePlayer(new FakeInput());

        player.TakeDamage(250);

        Assert.Equal(0f, player.Hp);
        Assert.False(player.IsAlive);
        Assert.False(player.TakeDamage(10));
    }

    [Fact]
    public void DashIFrames_BlockDamage()
    {
        var input = new FakeInput { Move = Vector2.UnitX, DashPressed = true };
        var player = CreatePlayer(input);
        player.Update(Step, TestArena, Shots);

        Assert.False(player.TakeDamage(30));

        Assert.Equal(100f, player.Hp);
    }

    [Fact]
    public void DeadPlayer_DoesNotMoveOrShoot()
    {
        var shots = new FakeShotHandler();
        var input = new FakeInput { Move = Vector2.UnitX, FirePressed = true, FireHeld = true };
        var player = CreatePlayer(input);
        player.GiveWeapon(new Weapons.Weapon(TestData.Pistol(), TestData.Game(), new NullAudioManager()));
        player.TakeDamage(1000);

        player.Update(Step, TestArena, shots);

        Assert.Equal(Center, player.Position);
        Assert.Equal(0, shots.Count);
    }

    [Fact]
    public void TouchingEnemy_DealsContactDamage_OncePerCooldown()
    {
        var enemies = new EnemySystem(new NullAudioManager());
        var players = new List<Player> { CreatePlayer(new FakeInput()) };
        enemies.Spawn(TestData.Zombie(), Center + new Vector2(40, 0));

        // First touch lands at once; the 0.8 s cooldown allows one more within a second.
        Run(enemies, players, 1);
        Assert.Equal(90f, players[0].Hp);

        Run(enemies, players, 40);
        Assert.Equal(90f, players[0].Hp);

        Run(enemies, players, 19);
        Assert.Equal(80f, players[0].Hp);
    }

    [Fact]
    public void EnemyOutOfReach_DealsNoDamage()
    {
        var enemies = new EnemySystem(new NullAudioManager());
        var players = new List<Player> { CreatePlayer(new FakeInput()) };
        enemies.Spawn(TestData.Zombie(), Center + new Vector2(400, 0));

        Run(enemies, players, 60);

        Assert.Equal(100f, players[0].Hp);
    }

    [Fact]
    public void SeveralEnemies_EachAttackOnTheirOwnCooldown()
    {
        var enemies = new EnemySystem(new NullAudioManager());
        var players = new List<Player> { CreatePlayer(new FakeInput()) };
        enemies.Spawn(TestData.Zombie(), Center + new Vector2(40, 0));
        enemies.Spawn(TestData.Zombie(), Center + new Vector2(-40, 0));
        enemies.Spawn(TestData.Zombie(), Center + new Vector2(0, 40));

        Run(enemies, players, 1);

        Assert.Equal(70f, players[0].Hp);
    }

    [Fact]
    public void EnoughContact_KillsThePlayer()
    {
        var enemies = new EnemySystem(new NullAudioManager());
        var players = new List<Player> { CreatePlayer(new FakeInput()) };
        enemies.Spawn(TestData.Zombie(), Center + new Vector2(40, 0));

        // Ten hits at one per 0.8 s.
        Run(enemies, players, 60 * 8);

        Assert.False(players[0].IsAlive);
    }

    [Fact]
    public void Enemies_IgnoreDeadPlayers_AndChaseTheLivingOne()
    {
        var enemies = new EnemySystem(new NullAudioManager());
        var dead = CreatePlayer(new FakeInput(), new Vector2(1000, 1500));
        var alive = CreatePlayer(new FakeInput(), new Vector2(2500, 1500));
        dead.TakeDamage(1000);
        var enemy = enemies.Spawn(TestData.Zombie(), new Vector2(1200, 1500));

        Run(enemies, new List<Player> { dead, alive }, 60);

        Assert.True(enemy.Position.X > 1200);
    }

    [Fact]
    public void Enemies_StandStill_WhenNobodyIsAlive()
    {
        var enemies = new EnemySystem(new NullAudioManager());
        var player = CreatePlayer(new FakeInput());
        player.TakeDamage(1000);
        var enemy = enemies.Spawn(TestData.Zombie(), new Vector2(500, 500));

        Run(enemies, new List<Player> { player }, 60);

        Assert.Equal(new Vector2(500, 500), enemy.Position);
    }

    private static Player CreatePlayer(FakeInput input, Vector2? position = null)
    {
        return new Player(0, input, TestData.Game().Player, new NullAudioManager(), position ?? Center);
    }

    private static void Run(EnemySystem enemies, List<Player> players, int steps)
    {
        for (var i = 0; i < steps; i++)
        {
            enemies.Update(Step, players, TestArena);
        }
    }
}
