using System.Collections.Generic;

namespace Fourgate.Core.Pooling;

/// <summary>
/// Reuses objects so spawning and despawning does not allocate. Active items are
/// indexable; their order changes when one is returned.
/// </summary>
public sealed class Pool<T>
    where T : class, new()
{
    private readonly List<T> _active;
    private readonly Stack<T> _free;

    public Pool(int initialCapacity)
    {
        _active = new List<T>(initialCapacity);
        _free = new Stack<T>(initialCapacity);
        for (var i = 0; i < initialCapacity; i++)
        {
            _free.Push(new T());
        }
    }

    /// <summary>Number of active items.</summary>
    public int Count => _active.Count;

    public T this[int index] => _active[index];

    /// <summary>Activates an item. The caller must initialize it; it still holds its previous state.</summary>
    public T Rent()
    {
        var item = _free.Count > 0 ? _free.Pop() : new T();
        _active.Add(item);
        return item;
    }

    /// <summary>
    /// Deactivates the item at <paramref name="index"/>. The last active item takes its place,
    /// so iterate backwards when returning items inside a loop.
    /// </summary>
    public void ReturnAt(int index)
    {
        var item = _active[index];
        var last = _active.Count - 1;
        _active[index] = _active[last];
        _active.RemoveAt(last);
        _free.Push(item);
    }
}
