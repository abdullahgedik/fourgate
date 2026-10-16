using System;
using System.Collections.Generic;
using Fourgate.Entities;
using Microsoft.Xna.Framework;

namespace Fourgate.Systems;

/// <summary>
/// The way to one target from anywhere on the floor, on a grid: every cell knows how far it is
/// from the target going round pillars, so an enemy only has to step to its cheapest neighbour.
/// One field per player serves every enemy chasing that player. Pillar cells are walls; barrel
/// cells can be crossed but cost a lot, so the way goes round barrels whenever there is one and
/// through them (enemies break barrels they touch) only when a player is walled in.
/// </summary>
public sealed class FlowField
{
    /// <summary>The extra cost of stepping into a barrel's cell, in straight steps.</summary>
    public const int BarrelCost = 12;

    private const int Straight = 10;
    private const int Diagonal = 14;
    private const byte Open = 0;
    private const byte Wall = 1;
    private const byte Barrel = 2;

    private static readonly int[] StepX = { 1, -1, 0, 0, 1, 1, -1, -1 };
    private static readonly int[] StepY = { 0, 0, 1, -1, 1, -1, 1, -1 };

    private readonly float _left;
    private readonly float _top;
    private readonly byte[] _cells;
    private readonly int[] _cost;
    private readonly long[] _heap;
    private int _heapCount;
    private int _targetCell = -1;

    /// <param name="arena">The floor and its pillars.</param>
    /// <param name="cellSize">Grid spacing (the barrel grid).</param>
    public FlowField(Arena arena, float cellSize)
    {
        _left = arena.Left;
        _top = arena.Top;
        CellSize = cellSize;
        Columns = Math.Max(1, (int)MathF.Ceiling(arena.Size / cellSize));
        _cells = new byte[Columns * Columns];
        _cost = new int[Columns * Columns];
        _heap = new long[Columns * Columns * 8];

        // Any cell a pillar covers, even partly, is a wall.
        var half = new Vector2(cellSize / 2f - 0.5f);
        for (var row = 0; row < Columns; row++)
        {
            for (var column = 0; column < Columns; column++)
            {
                if (arena.OverlapsPillar(CenterOf(column, row), half))
                {
                    _cells[row * Columns + column] = Wall;
                }
            }
        }
    }

    public int Columns { get; }

    public float CellSize { get; }

    /// <summary>The cell the field leads to, or -1 before it is first built.</summary>
    public int TargetCell => _targetCell;

    /// <summary>The cell a point is in, or -1 off the grid.</summary>
    public int CellAt(Vector2 point)
    {
        var column = (int)MathF.Floor((point.X - _left) / CellSize);
        var row = (int)MathF.Floor((point.Y - _top) / CellSize);
        return column < 0 || row < 0 || column >= Columns || row >= Columns ? -1 : row * Columns + column;
    }

    /// <summary>Works out the way to <paramref name="target"/> from every cell.</summary>
    /// <param name="target">Where the way leads.</param>
    /// <param name="barrels">Centres of the solid obstacles standing on the floor.</param>
    public void Build(Vector2 target, IReadOnlyList<Vector2> barrels)
    {
        for (var i = 0; i < _cells.Length; i++)
        {
            if (_cells[i] == Barrel)
            {
                _cells[i] = Open;
            }

            _cost[i] = int.MaxValue;
        }

        for (var i = 0; i < barrels.Count; i++)
        {
            var cell = CellAt(barrels[i]);
            if (cell >= 0 && _cells[cell] == Open)
            {
                _cells[cell] = Barrel;
            }
        }

        _targetCell = CellAt(target);
        if (_targetCell < 0)
        {
            return;
        }

        _heapCount = 0;
        _cost[_targetCell] = 0;
        Push(0, _targetCell);
        while (_heapCount > 0)
        {
            var entry = Pop();
            var cell = (int)(entry & 0xFFFFFFFF);
            if ((int)(entry >> 32) > _cost[cell])
            {
                // A stale copy: this cell was already reached more cheaply.
                continue;
            }

            var column = cell % Columns;
            var row = cell / Columns;
            for (var d = 0; d < StepX.Length; d++)
            {
                var next = Neighbour(column, row, d);
                if (next < 0)
                {
                    continue;
                }

                // Entering a barrel's cell costs extra; leaving one does not.
                var cost = _cost[cell] + (d < 4 ? Straight : Diagonal) + (_cells[next] == Barrel ? BarrelCost * Straight : 0);
                if (cost < _cost[next])
                {
                    _cost[next] = cost;
                    Push(cost, next);
                }
            }
        }
    }

    /// <summary>
    /// Which way to walk from a point: toward the middle of its cheapest neighbour. False off the
    /// grid, in the target's own cell, or where the target cannot be reached.
    /// </summary>
    public bool TryGetDirection(Vector2 from, out Vector2 direction)
    {
        direction = Vector2.Zero;
        var cell = CellAt(from);
        if (cell < 0 || _targetCell < 0 || cell == _targetCell || _cost[cell] == int.MaxValue)
        {
            return false;
        }

        var column = cell % Columns;
        var row = cell / Columns;
        var best = -1;
        var bestCost = _cost[cell];
        for (var d = 0; d < StepX.Length; d++)
        {
            var next = Neighbour(column, row, d);
            if (next >= 0 && _cost[next] < bestCost)
            {
                bestCost = _cost[next];
                best = next;
            }
        }

        if (best < 0)
        {
            return false;
        }

        var toward = CenterOf(best % Columns, best / Columns) - from;
        if (toward.LengthSquared() < 0.0001f)
        {
            return false;
        }

        direction = Vector2.Normalize(toward);
        return true;
    }

    /// <summary>True when the straight line between two points crosses no pillar or barrel cell.</summary>
    public bool LineClear(Vector2 from, Vector2 to)
    {
        var line = to - from;
        var length = line.Length();
        var steps = (int)MathF.Ceiling(length / (CellSize * 0.4f));
        for (var i = 1; i < steps; i++)
        {
            var cell = CellAt(from + line * (i / (float)steps));
            if (cell >= 0 && _cells[cell] != Open)
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>Steps (in straight cells) from a point to the target; infinite when it cannot be reached.</summary>
    public float StepsFrom(Vector2 point)
    {
        var cell = CellAt(point);
        return cell < 0 || _cost[cell] == int.MaxValue ? float.PositiveInfinity : _cost[cell] / (float)Straight;
    }

    private Vector2 CenterOf(int column, int row)
    {
        return new Vector2(_left + (column + 0.5f) * CellSize, _top + (row + 0.5f) * CellSize);
    }

    // The neighbour in direction d, or -1: off the grid, a wall, or a diagonal that would cut a
    // pillar's or barrel's corner.
    private int Neighbour(int column, int row, int d)
    {
        var x = column + StepX[d];
        var y = row + StepY[d];
        if (x < 0 || y < 0 || x >= Columns || y >= Columns || _cells[y * Columns + x] == Wall)
        {
            return -1;
        }

        if (d >= 4 && (_cells[row * Columns + x] != Open || _cells[y * Columns + column] != Open))
        {
            return -1;
        }

        return y * Columns + x;
    }

    // A binary heap of (cost, cell) pairs packed into longs, cheapest first; a cell may be in it
    // more than once, and the stale copies are skipped when popped.
    private void Push(int cost, int cell)
    {
        if (_heapCount >= _heap.Length)
        {
            return;
        }

        var i = _heapCount++;
        _heap[i] = ((long)cost << 32) | (uint)cell;
        while (i > 0)
        {
            var parent = (i - 1) / 2;
            if (_heap[parent] <= _heap[i])
            {
                break;
            }

            (_heap[parent], _heap[i]) = (_heap[i], _heap[parent]);
            i = parent;
        }
    }

    private long Pop()
    {
        var top = _heap[0];
        _heap[0] = _heap[--_heapCount];
        var i = 0;
        while (true)
        {
            var left = i * 2 + 1;
            var right = left + 1;
            var smallest = i;
            if (left < _heapCount && _heap[left] < _heap[smallest])
            {
                smallest = left;
            }

            if (right < _heapCount && _heap[right] < _heap[smallest])
            {
                smallest = right;
            }

            if (smallest == i)
            {
                break;
            }

            (_heap[smallest], _heap[i]) = (_heap[i], _heap[smallest]);
            i = smallest;
        }

        return top;
    }
}
