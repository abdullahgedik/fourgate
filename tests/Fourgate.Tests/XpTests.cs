using Fourgate.Core.Audio;
using Fourgate.Entities;
using Fourgate.Systems;
using Microsoft.Xna.Framework;

namespace Fourgate.Tests;

public class XpTests
{
    private const float Step = 1f / 60f;

    [Theory]
    [InlineData(1, 60)]
    [InlineData(2, 153)]
    [InlineData(3, 264)]
    [InlineData(10, 1343)]
    public void XpToNext_FollowsTheCurve(int level, int expected)
    {
        Assert.Equal(expected, TestData.Xp().XpToNext(level));
    }

    [Fact]
    public void XpToNext_KeepsGrowing_ForVeryHighLevels()
    {
        var config = TestData.Xp();

        Assert.True(config.XpToNext(500) > config.XpToNext(499));
        Assert.True(config.XpToNext(500) > 0);
    }

    [Fact]
    public void AddXp_BelowTheRequirement_DoesNotLevelUp()
    {
        var progression = new PlayerProgression();

        var gained = progression.AddXp(59, TestData.Xp());

        Assert.Equal(0, gained);
        Assert.Equal(1, progression.Level);
        Assert.Equal(59, progression.Xp);
        Assert.Equal(0, progression.BankedPicks);
    }

    [Fact]
    public void AddXp_ReachingTheRequirement_LevelsUpAndBanksAPick()
    {
        var progression = new PlayerProgression();

        var gained = progression.AddXp(70, TestData.Xp());

        Assert.Equal(1, gained);
        Assert.Equal(2, progression.Level);
        Assert.Equal(10, progression.Xp);
        Assert.Equal(1, progression.BankedPicks);
    }

    [Fact]
    public void AddXp_LargeAmount_CanGainSeveralLevelsAtOnce()
    {
        var progression = new PlayerProgression();

        // 60 + 153 + 264 = 477 for three levels, with 23 left over.
        var gained = progression.AddXp(500, TestData.Xp());

        Assert.Equal(3, gained);
        Assert.Equal(4, progression.Level);
        Assert.Equal(23, progression.Xp);
        Assert.Equal(3, progression.BankedPicks);
    }

    [Fact]
    public void AddPick_BanksAnExtraPick()
    {
        var progression = new PlayerProgression();

        progression.AddPick();

        Assert.Equal(1, progression.BankedPicks);
        Assert.Equal(1, progression.Level);
    }

    [Fact]
    public void SharedXp_GoesInFullToEveryPlayer_IncludingDeadOnes()
    {
        var players = Players(2);
        players[1].TakeDamage(1000);
        var xp = new XpSystem(TestData.Xp(shared: true), new NullAudioManager());

        xp.Award(40, 0, players);

        Assert.Equal(40, players[0].Progression.Xp);
        Assert.Equal(40, players[1].Progression.Xp);
    }

    [Fact]
    public void UnsharedXp_GoesOnlyToTheKiller()
    {
        var players = Players(2);
        var xp = new XpSystem(TestData.Xp(shared: false), new NullAudioManager());

        xp.Award(40, 1, players);

        Assert.Equal(0, players[0].Progression.Xp);
        Assert.Equal(40, players[1].Progression.Xp);
    }

    [Fact]
    public void KilledEnemy_IsReportedOnce_WithItsTypeAndKiller()
    {
        var arena = new Arena(TestData.Arena());
        var enemies = new EnemySystem(new NullAudioManager());
        var players = Players(1);
        var enemy = enemies.Spawn(TestData.Zombie(), new Vector2(500, 500));
        enemy.Hit(1000, 0);

        enemies.Update(Step, players, arena);
        var death = Assert.Single(enemies.Deaths);
        Assert.Equal("normal", death.Config.Id);
        Assert.Equal(new Vector2(500, 500), death.Position);
        Assert.Equal(0, death.KilledBy);

        enemies.Update(Step, players, arena);
        Assert.Empty(enemies.Deaths);
    }

    private static List<Player> Players(int count)
    {
        var players = new List<Player>();
        for (var i = 0; i < count; i++)
        {
            players.Add(new Player(
                i, new FakeInput(), TestData.Game().Player, new NullAudioManager(), new Vector2(1500, 1500)));
        }

        return players;
    }
}
