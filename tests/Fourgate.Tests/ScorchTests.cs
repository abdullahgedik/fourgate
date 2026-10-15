using Fourgate.Core.Data;
using Fourgate.Core.Rendering;
using Fourgate.Entities;
using Fourgate.Systems;
using Microsoft.Xna.Framework;

namespace Fourgate.Tests;

public class ScorchTests
{
    [Fact]
    public void BlastInTheOpen_ScorchesOnlyTheFloor()
    {
        var (scorches, floor) = Create();

        // Between the pillars, far from every wall.
        scorches.AddBlast(new Vector2(1500, 1500), 110f);

        Assert.Equal(2, floor.TotalAdded);
        Assert.Equal(0, scorches.WallMarkCount);
    }

    [Fact]
    public void BlastNearAWall_ScorchesThatWall_AndNotTheOneFarAway()
    {
        var (scorches, floor) = Create();

        scorches.AddBlast(new Vector2(1500, 60), 110f);

        Assert.Equal(2, floor.TotalAdded);
        Assert.Equal(1, scorches.WallMarkCount);
    }

    [Fact]
    public void BlastInACorner_ScorchesBothWalls()
    {
        var (scorches, _) = Create();

        scorches.AddBlast(new Vector2(60, 60), 110f);

        Assert.Equal(2, scorches.WallMarkCount);
    }

    [Fact]
    public void BlastBesideAPillar_ScorchesTheFaceTowardIt_NotTheFarSide()
    {
        var config = DataLoader.LoadAll(DataLoader.DefaultDirectory).Game;
        var arena = new Arena(config.Arena);
        var scorches = new ScorchSystem(arena, new FloorStains(new Vector2(arena.Left, arena.Top), arena.Size), 90f, 120f);

        // The shipped first pillar spans 840–1140 on both axes; this blast is just left of it.
        scorches.AddBlast(new Vector2(780, 990), 110f);

        Assert.Equal(1, scorches.WallMarkCount);
    }

    [Fact]
    public void WallMarks_AreCapped_SoOldOnesGiveWay()
    {
        var (scorches, _) = Create();

        for (var i = 0; i < 1000; i++)
        {
            scorches.AddBlast(new Vector2(1500, 60), 110f);
        }

        Assert.Equal(256, scorches.WallMarkCount);
    }

    [Fact]
    public void HarderHits_BleedMore_WithinLimits()
    {
        Assert.True(ParticleSystem.BleedScale(150f) > ParticleSystem.BleedScale(40f));
        Assert.True(ParticleSystem.BleedScale(40f) > ParticleSystem.BleedScale(12f));
        Assert.Equal(1f, ParticleSystem.BleedScale(25f), 3);
        Assert.Equal(ParticleSystem.BleedScale(1f), ParticleSystem.BleedScale(0f));
        Assert.Equal(ParticleSystem.BleedScale(10_000f), ParticleSystem.BleedScale(float.MaxValue));
    }

    private static (ScorchSystem Scorches, FloorStains Floor) Create()
    {
        var arena = new Arena(TestData.Arena());
        var floor = new FloorStains(new Vector2(arena.Left, arena.Top), arena.Size);
        return (new ScorchSystem(arena, floor, 90f, 120f), floor);
    }
}
