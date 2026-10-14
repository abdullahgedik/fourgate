using Fourgate.Core.Audio;
using Fourgate.Core.Data;
using Fourgate.Core.Rendering;
using Fourgate.Core.Rng;
using Fourgate.Entities;
using Fourgate.Systems;
using Microsoft.Xna.Framework;

namespace Fourgate.Tests;

public class TankZombieTests
{
    private const float Step = 1f / 60f;

    private static readonly Arena TestArena = new(TestData.Arena());

    [Fact]
    public void ShippedTank_IsSlowBigAndTough_AndHitsHard()
    {
        var data = DataLoader.LoadAll(DataLoader.DefaultDirectory);
        var normal = data.GetEnemy("normal");
        var tank = data.GetEnemy("tank");

        Assert.Equal(8, tank.UnlockWave);
        Assert.True(tank.Hp >= normal.Hp * 4f);
        Assert.True(tank.Speed < normal.Speed);
        Assert.True(tank.Radius > normal.Radius);
        Assert.True(tank.ContactDamage > normal.ContactDamage);
        Assert.InRange(tank.KnockbackResist, 0.5f, 0.95f);
        Assert.Equal(ShapeKind.Square, tank.Shape);
    }

    [Fact]
    public void Tank_IsPushedBackLess_ThanANormalZombie()
    {
        var data = DataLoader.LoadAll(DataLoader.DefaultDirectory);
        var enemies = new EnemySystem(new NullAudioManager());
        var normal = enemies.Spawn(data.GetEnemy("normal"), new Vector2(1000, 1000));
        var tank = enemies.Spawn(data.GetEnemy("tank"), new Vector2(1000, 2000));

        normal.Knock(new Vector2(40, 0));
        tank.Knock(new Vector2(40, 0));
        enemies.Update(Step, new List<Player>(), TestArena);

        Assert.True(tank.Position.X - 1000f < (normal.Position.X - 1000f) * 0.5f);
    }

    [Fact]
    public void ShippedWaves_BringTanksFromWaveEight()
    {
        var data = DataLoader.LoadAll(DataLoader.DefaultDirectory);
        var result = new List<EnemyConfig>();

        WaveMath.Compose(7, WaveMath.Budget(7, data.Waves), data.Enemies, data.Waves.NewerTypeBias, new GameRandom(1), result);
        Assert.DoesNotContain(result, enemy => enemy.Id == "tank");

        var seen = false;
        for (var seed = 1; seed <= 5 && !seen; seed++)
        {
            result.Clear();
            WaveMath.Compose(10, WaveMath.Budget(10, data.Waves), data.Enemies, data.Waves.NewerTypeBias, new GameRandom(seed), result);
            seen = result.Exists(enemy => enemy.Id == "tank");
        }

        Assert.True(seen);
    }
}
