using Fourgate.Core.Audio;
using Fourgate.Core.Data;
using Fourgate.Core.Rendering;
using Fourgate.Entities;
using Fourgate.Entities.Enemies;
using Fourgate.Systems;
using Microsoft.Xna.Framework;

namespace Fourgate.Tests;

public class EliteZombieTests
{
    private const float Step = 1f / 60f;

    private static readonly Arena TestArena = new(TestData.Arena());

    [Fact]
    public void Elite_ThrowsAVolley_FannedAcrossItsSpread()
    {
        var fireballs = new EnemyShotSystem();
        var enemies = new EnemySystem(new NullAudioManager()) { Fireballs = fireballs };
        var player = Player(new Vector2(1400, 1000));
        var elite = enemies.Spawn(Elite(strafeSpeed: 0f), new Vector2(1000, 1000));

        // In range at once: a 0.5 s wind-up, then three fireballs.
        Run(enemies, fireballs, player, 0.5f);
        Assert.Equal(0, fireballs.FireballCount);
        Run(enemies, fireballs, player, 0.05f);
        Assert.Equal(3, fireballs.FireballCount);

        // The outer two miss sideways: only the middle one reaches the player head on.
        Run(enemies, fireballs, player, 1.2f);
        Assert.Equal(90f, player.Hp);
        Assert.True(elite.IsAlive);
    }

    [Fact]
    public void Elite_SidestepsBetweenThrows_ButNeverDuringAWindUp()
    {
        var fireballs = new EnemyShotSystem();
        var enemies = new EnemySystem(new NullAudioManager()) { Fireballs = fireballs };
        var player = Player(new Vector2(1400, 1000));
        var elite = enemies.Spawn(Elite(strafeSpeed: 120f), new Vector2(1000, 1000));

        Run(enemies, fireballs, player, 0.25f);
        Assert.True(elite.IsWindingUp);
        Assert.Equal(1000f, elite.Position.Y, 1);

        // After the throw it moves across the line to the player.
        Run(enemies, fireballs, player, 0.75f);
        Assert.True(Math.Abs(elite.Position.Y - 1000f) > 30f);
    }

    [Fact]
    public void ShippedElite_IsAToughFastVolleyThrower_FromWaveTwelve()
    {
        var data = DataLoader.LoadAll(DataLoader.DefaultDirectory);
        var elite = data.GetEnemy("elite");
        var ranged = data.GetEnemy("ranged");

        Assert.Equal(12, elite.UnlockWave);
        Assert.True(elite.Hp > ranged.Hp * 3f);
        Assert.True(elite.Speed > data.GetEnemy("normal").Speed);
        Assert.True(elite.ContactDamage > 0f);
        Assert.NotNull(elite.Attack);
        Assert.Equal(3, elite.Attack!.Count);
        Assert.True(elite.Attack.SpreadDegrees > 0f);
        Assert.True(elite.Attack.StrafeSpeed > 0f);
        Assert.True(elite.Attack.TelegraphSeconds >= 0.5f);
        Assert.Equal(ShapeKind.Hexagon, elite.Shape);
    }

    private static EnemyConfig Elite(float strafeSpeed)
    {
        return new EnemyConfig
        {
            Id = "elite", DisplayName = "Elite", Hp = 300, Speed = 160, Radius = 26, ContactDamage = 25,
            ContactCooldown = 0.8f, Xp = 80, ScoreValue = 80, Cost = 10, UnlockWave = 1, DropChance = 0f,
            Attack = new EnemyAttackConfig
            {
                Type = EnemyAttackType.Fireball, Damage = 10, Range = 500, TelegraphSeconds = 0.5f, CooldownSeconds = 1.4f,
                ProjectileSpeed = 380, ProjectileRadius = 12, ProjectileRange = 750,
                Count = 3, SpreadDegrees = 15, StrafeSpeed = strafeSpeed,
            },
        };
    }

    private static Player Player(Vector2 position)
    {
        return new Player(0, new FakeInput(), TestData.Game().Player, new NullAudioManager(), position);
    }

    private static void Run(EnemySystem enemies, EnemyShotSystem fireballs, Player player, float seconds)
    {
        var players = new List<Player> { player };
        var steps = (int)MathF.Round(seconds / Step);
        for (var i = 0; i < steps; i++)
        {
            enemies.Update(Step, players, TestArena);
            fireballs.Update(Step, players, TestArena);
        }
    }
}
