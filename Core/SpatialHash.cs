using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;

namespace Fourgate.Core;

/// <summary>
/// Uniform grid of circles for fast "what is near here" queries. Rebuilt every frame:
/// <see cref="Clear"/>, then <see cref="Insert"/> everything, then query.
/// </summary>
public sealed class SpatialHash<T>
{
    private readonly float _cellSize;
    private readonly Dictionary<long, List<Entry>> _cells = new();
    private readonly List<List<Entry>> _usedCells = new();
    private float _maxRadius;

    public SpatialHash(float cellSize)
    {
        if (cellSize <= 0f)
        {
            throw new ArgumentOutOfRangeException(nameof(cellSize), "Cell size must be positive.");
        }

        _cellSize = cellSize;
    }

    /// <summary>Empties the grid but keeps its memory for reuse.</summary>
    public void Clear()
    {
        for (var i = 0; i < _usedCells.Count; i++)
        {
            _usedCells[i].Clear();
        }

        _usedCells.Clear();
        _maxRadius = 0f;
    }

    public void Insert(T item, Vector2 position, float radius)
    {
        var key = Key(Cell(position.X), Cell(position.Y));
        if (!_cells.TryGetValue(key, out var cell))
        {
            cell = new List<Entry>();
            _cells[key] = cell;
        }

        if (cell.Count == 0)
        {
            _usedCells.Add(cell);
        }

        cell.Add(new Entry(item, position, radius));
        _maxRadius = Math.Max(_maxRadius, radius);
    }

    /// <summary>
    /// Appends every item whose circle (as inserted) overlaps the query circle to
    /// <paramref name="results"/>. The list is not cleared first.
    /// </summary>
    public void Query(Vector2 center, float radius, List<T> results)
    {
        // Items are stored by their center, so reach far enough to catch the largest one.
        var reach = radius + _maxRadius;
        var minX = Cell(center.X - reach);
        var maxX = Cell(center.X + reach);
        var minY = Cell(center.Y - reach);
        var maxY = Cell(center.Y + reach);

        for (var y = minY; y <= maxY; y++)
        {
            for (var x = minX; x <= maxX; x++)
            {
                if (!_cells.TryGetValue(Key(x, y), out var cell))
                {
                    continue;
                }

                for (var i = 0; i < cell.Count; i++)
                {
                    var entry = cell[i];
                    var limit = radius + entry.Radius;
                    if (Vector2.DistanceSquared(center, entry.Position) <= limit * limit)
                    {
                        results.Add(entry.Item);
                    }
                }
            }
        }
    }

    private static long Key(int x, int y)
    {
        return ((long)x << 32) ^ (uint)y;
    }

    private int Cell(float coordinate)
    {
        return (int)MathF.Floor(coordinate / _cellSize);
    }

    private readonly record struct Entry(T Item, Vector2 Position, float Radius);
}
