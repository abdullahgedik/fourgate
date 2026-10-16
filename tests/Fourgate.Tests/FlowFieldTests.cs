using Fourgate.Core.Data;
using Fourgate.Entities;
using Fourgate.Systems;
using Microsoft.Xna.Framework;

namespace Fourgate.Tests;

public class FlowFieldTests
{
    private static readonly List<Vector2> NoBarrels = new();

    // One 300x300 pillar: x and y 1350-1650, which is exactly cells 22.5-27.5 on a 60 grid.
    private static readonly Arena PillarArena = new(new ArenaConfig
    {
        X = 0, Y = 0, Size = 3000, DoorWidth = 420, WallThickness = 24,
        Pillars = new List<PillarConfig> { new() { CenterX = 1500, CenterY = 1500, Width = 300, Height = 300 } },
    });

    [Fact]
    public void OpenGround_LeadsStraightAtTheTarget()
    {
        var field = new FlowField(new Arena(TestData.Arena()), 60f);
        field.Build(new Vector2(1500, 1500), NoBarrels);

        Assert.True(field.TryGetDirection(new Vector2(900, 1500), out var way));
        Assert.True(way.X > 0.9f);
        Assert.True(field.LineClear(new Vector2(900, 1500), new Vector2(1500, 1500)));
    }

    [Fact]
    public void Walker_FollowingTheField_GetsRoundAPillar()
    {
        var field = new FlowField(PillarArena, 60f);
        var target = new Vector2(1500, 1800);
        field.Build(target, NoBarrels);
        var walker = new Vector2(1500, 1200);
        Assert.False(field.LineClear(walker, target));

        for (var i = 0; i < 400; i++)
        {
            if (!field.TryGetDirection(walker, out var way))
            {
                break;
            }

            walker = PillarArena.ResolveMovement(walker + way * 5f, 20f);
        }

        // In the target's own cell the field hands over to walking straight at it.
        Assert.Equal(field.TargetCell, field.CellAt(walker));
    }

    [Fact]
    public void TheWay_GoesRoundABarrel_WhenThereIsRoom()
    {
        var field = new FlowField(new Arena(TestData.Arena()), 60f);
        var barrel = new Vector2(1050, 1050);
        field.Build(new Vector2(1290, 1050), new List<Vector2> { barrel });

        // Walking from just left of the barrel, the next step is up or down round it, not into it.
        Assert.True(field.TryGetDirection(new Vector2(990, 1050), out var way));
        Assert.True(MathF.Abs(way.Y) > 0.3f);
        Assert.False(field.LineClear(new Vector2(900, 1050), new Vector2(1290, 1050)));
    }

    [Fact]
    public void AWalledInTarget_IsStillReached_ThroughTheBarrels()
    {
        var field = new FlowField(new Arena(TestData.Arena()), 60f);
        var barrels = new List<Vector2> { new(90, 30), new(30, 90), new(90, 90) };
        field.Build(new Vector2(30, 30), barrels);

        Assert.True(field.TryGetDirection(new Vector2(600, 600), out _));
        Assert.True(float.IsFinite(field.StepsFrom(new Vector2(600, 600))));
    }

    [Fact]
    public void BossField_KeepsABigBodyClearOfThePillar()
    {
        var normal = new FlowField(PillarArena, 60f);
        var wide = new FlowField(PillarArena, 60f, clearance: 40f);
        var target = new Vector2(1500, 1800);
        normal.Build(target, NoBarrels);
        wide.Build(target, NoBarrels);

        // The cell just beside the pillar's face is open to a zombie, not to a boss.
        var besideFace = new Vector2(1680, 1500);
        Assert.True(float.IsFinite(normal.StepsFrom(besideFace)));
        Assert.False(float.IsFinite(wide.StepsFrom(besideFace)));
    }

    [Fact]
    public void BigWalker_FollowingTheBossField_GetsRoundAPillar()
    {
        var field = new FlowField(PillarArena, 60f, clearance: 40f);
        var target = new Vector2(1500, 1900);
        field.Build(target, NoBarrels);
        var walker = new Vector2(1500, 1100);

        for (var i = 0; i < 600; i++)
        {
            if (!field.TryGetDirection(walker, out var way))
            {
                break;
            }

            walker = PillarArena.ResolveMovement(walker + way * 5f, 70f);
        }

        Assert.Equal(field.TargetCell, field.CellAt(walker));
    }

    [Fact]
    public void Cost_CountsTheDetour()
    {
        var field = new FlowField(PillarArena, 60f);
        var target = new Vector2(1500, 1800);
        field.Build(target, NoBarrels);

        // Straight through would be 10 cells; round the pillar is clearly longer.
        Assert.True(field.StepsFrom(new Vector2(1500, 1200)) > 12f);
    }
}
