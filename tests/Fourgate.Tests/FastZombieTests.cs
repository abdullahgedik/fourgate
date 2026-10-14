using Fourgate.Core.Audio;
using Fourgate.Core.Data;
using Fourgate.Core.Rendering;
using Fourgate.Core.Rng;
using Fourgate.Entities;
using Fourgate.Systems;
using Microsoft.Xna.Framework;

namespace Fourgate.Tests;

public class FastZombieTests
{
    private const float Step = 1f / 60f;

    private static readonly Arena TestArena = new(TestData.Arena());

    [Fact]
    public void ShippedFastZombie_IsQuickerAndFrailer_ThanTheNormalOne()
    {
        var data = DataLoader.LoadAll(DataLoader.DefaultDirectory);
        var normal = data.GetEnemy("normal");
        var fast = data.GetEnemy("fast");

        Assert.Equal(3, fast.UnlockWave);
        Assert.True(fast.Speed > normal.Speed * 1.5f);
        Assert.True(fast.Hp < normal.Hp);
        Assert.True(fast.Radius < normal.Radius);
        Assert.True(fast.Cost > normal.Cost);
        Assert.Equal(ShapeKind.Triangle, fast.Shape);
        Assert.NotEqual(normal.Color, fast.Color);
    }

    [Fact]
    public void ShippedWaves_HaveNoFastZombiesBeforeWaveThree_AndSomeAfter()
    {
        var data = DataLoader.LoadAll(DataLoader.DefaultDirectory);
        var result = new List<EnemyConfig>();

        WaveMath.Compose(2, WaveMath.Budget(2, data.Waves), data.Enemies, data.Waves.NewerTypeBias, new GameRandom(1), result);
        Assert.DoesNotContain(result, enemy => enemy.Id == "fast");

        result.Clear();
        WaveMath.Compose(6, WaveMath.Budget(6, data.Waves), data.Enemies, data.Waves.NewerTypeBias, new GameRandom(1), result);
        Assert.Contains(result, enemy => enemy.Id == "fast");
        Assert.Contains(result, enemy => enemy.Id == "normal");
    }

    [Fact]
    public void Enemy_FacesThePlayerItWalksToward()
    {
        var enemies = new EnemySystem(new NullAudioManager());
        var player = new Player(0, new FakeInput(), TestData.Game().Player, new NullAudioManager(), new Vector2(1500, 1800));
        var enemy = enemies.Spawn(TestData.Zombie(), new Vector2(1500, 1500));

        enemies.Update(Step, new List<Player> { player }, TestArena);

        Assert.Equal(MathF.PI / 2f, enemy.Facing, 3);
    }

    [Fact]
    public void EnemyColor_ThatIsNotThreeBytes_IsRejected()
    {
        const string json = """
            { "enemies": [ { "id": "x", "displayName": "X", "hp": 1, "speed": 1, "radius": 1, "contactDamage": 1,
              "contactCooldown": 1, "xp": 1, "scoreValue": 1, "cost": 1, "unlockWave": 1, "dropChance": 0,
              "color": [ 300, 0, 0 ] } ] }
            """;

        var error = Assert.Throws<DataLoadException>(() => DataLoader.ParseEnemies(json, "enemies.json"));

        Assert.Equal("enemies[0].color", error.Field);
    }
}
