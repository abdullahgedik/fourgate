using Fourgate.Core.Data;
using Fourgate.Entities;
using Microsoft.Xna.Framework;

namespace Fourgate.Tests;

public class ArenaTests
{
    private static readonly ArenaConfig Config = new()
    {
        X = 460, Y = 40, Size = 1000, DoorWidth = 140, WallThickness = 16,
    };

    [Fact]
    public void HasOneDoorPerWall_CenteredOnThatWall()
    {
        var arena = new Arena(Config);

        Assert.Equal(4, arena.Doors.Count);
        Assert.Equal(new Vector2(960, 32), DoorOn(arena, DoorSide.Top).Center);
        Assert.Equal(new Vector2(960, 1048), DoorOn(arena, DoorSide.Bottom).Center);
        Assert.Equal(new Vector2(452, 540), DoorOn(arena, DoorSide.Left).Center);
        Assert.Equal(new Vector2(1468, 540), DoorOn(arena, DoorSide.Right).Center);
    }

    [Fact]
    public void DoorsPointIntoTheArena()
    {
        var arena = new Arena(Config);

        foreach (var door in arena.Doors)
        {
            var step = door.Center + door.Inward * Config.WallThickness;
            Assert.InRange(step.X, arena.Left, arena.Right);
            Assert.InRange(step.Y, arena.Top, arena.Bottom);
        }
    }

    private static Door DoorOn(Arena arena, DoorSide side)
    {
        return arena.Doors.Single(d => d.Side == side);
    }
}
