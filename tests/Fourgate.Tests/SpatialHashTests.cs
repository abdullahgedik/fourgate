using Fourgate.Core;
using Fourgate.Core.Rng;
using Microsoft.Xna.Framework;

namespace Fourgate.Tests;

public class SpatialHashTests
{
    [Fact]
    public void Query_ReturnsOverlappingItemsOnly()
    {
        var hash = new SpatialHash<string>(100);
        hash.Insert("near", new Vector2(120, 100), 20);
        hash.Insert("far", new Vector2(900, 900), 20);
        var results = new List<string>();

        hash.Query(new Vector2(100, 100), 10, results);

        Assert.Equal(new[] { "near" }, results);
    }

    [Fact]
    public void Query_FindsItemsAcrossCellBorders()
    {
        var hash = new SpatialHash<string>(100);
        hash.Insert("left", new Vector2(95, 50), 20);
        hash.Insert("right", new Vector2(105, 50), 20);
        var results = new List<string>();

        hash.Query(new Vector2(99, 50), 1, results);

        Assert.Equal(2, results.Count);
    }

    [Fact]
    public void Query_FindsALargeItemCenteredSeveralCellsAway()
    {
        var hash = new SpatialHash<string>(50);
        hash.Insert("big", new Vector2(400, 0), 300);
        var results = new List<string>();

        hash.Query(new Vector2(110, 0), 5, results);

        Assert.Single(results);
    }

    [Fact]
    public void Query_WorksWithNegativeCoordinates()
    {
        var hash = new SpatialHash<string>(100);
        hash.Insert("a", new Vector2(-150, -250), 10);
        var results = new List<string>();

        hash.Query(new Vector2(-155, -245), 10, results);

        Assert.Single(results);
    }

    [Fact]
    public void Clear_RemovesEverything()
    {
        var hash = new SpatialHash<string>(100);
        hash.Insert("a", new Vector2(10, 10), 10);
        hash.Clear();
        var results = new List<string>();

        hash.Query(new Vector2(10, 10), 50, results);

        Assert.Empty(results);
    }

    [Fact]
    public void Query_MatchesABruteForceSearch()
    {
        var random = new GameRandom(1234);
        var hash = new SpatialHash<int>(96);
        var positions = new List<Vector2>();
        for (var i = 0; i < 300; i++)
        {
            var position = new Vector2(random.Range(0, 3000), random.Range(0, 3000));
            positions.Add(position);
            hash.Insert(i, position, 20);
        }

        var center = new Vector2(1500, 1500);
        var results = new List<int>();
        hash.Query(center, 400, results);

        var expected = new List<int>();
        for (var i = 0; i < positions.Count; i++)
        {
            if (Vector2.Distance(center, positions[i]) <= 420)
            {
                expected.Add(i);
            }
        }

        results.Sort();
        Assert.Equal(expected, results);
    }

    [Fact]
    public void GameRandom_SameSeed_GivesTheSameSequence()
    {
        var first = new GameRandom(42);
        var second = new GameRandom(42);

        for (var i = 0; i < 20; i++)
        {
            Assert.Equal(first.NextFloat(), second.NextFloat());
        }
    }
}
