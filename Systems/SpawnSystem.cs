using System;
using System.Collections.Generic;
using Fourgate.Core.Audio;
using Fourgate.Core.Data;
using Fourgate.Core.Rng;
using Fourgate.Entities;

namespace Fourgate.Systems;

/// <summary>
/// Feeds a wave's enemies into the arena: picks a door, makes it glow as a warning,
/// then releases a group through it.
/// </summary>
public sealed class SpawnSystem
{
    private readonly WavesConfig _waves;
    private readonly int _maxAlive;
    private readonly EnemySystem _enemies;
    private readonly Arena _arena;
    private readonly GameRandom _random;
    private readonly IAudioManager _audio;

    private readonly List<EnemyConfig> _queue = new();
    private readonly DoorSpawn[] _doors;
    private float _interval;
    private int _groupSize;
    private float _timer;

    public SpawnSystem(
        WavesConfig waves, int maxAlive, EnemySystem enemies, Arena arena, GameRandom random, IAudioManager audio)
    {
        _waves = waves;
        _maxAlive = maxAlive;
        _enemies = enemies;
        _arena = arena;
        _random = random;
        _audio = audio;

        _doors = new DoorSpawn[arena.Doors.Count];
        for (var i = 0; i < _doors.Length; i++)
        {
            _doors[i] = new DoorSpawn();
        }
    }

    /// <summary>Enemies of the current wave that have not entered the arena yet.</summary>
    public int PendingCount
    {
        get
        {
            var count = _queue.Count;
            for (var i = 0; i < _doors.Length; i++)
            {
                count += _doors[i].Group.Count;
            }

            return count;
        }
    }

    /// <summary>True when every enemy of the wave has entered the arena.</summary>
    public bool IsFinished => PendingCount == 0;

    /// <summary>Starts feeding in a new set of enemies; the first group is announced at once.</summary>
    public void Begin(IReadOnlyList<EnemyConfig> enemies, float interval, int groupSize)
    {
        _queue.Clear();
        for (var i = 0; i < enemies.Count; i++)
        {
            _queue.Add(enemies[i]);
        }

        _interval = interval;
        _groupSize = groupSize;
        _timer = 0f;
    }

    /// <summary>How strongly a door's warning glows: 0 when idle, rising to 1 as its group is about to emerge.</summary>
    public float GetDoorGlow(int doorIndex)
    {
        var door = _doors[doorIndex];
        if (door.Group.Count == 0)
        {
            return 0f;
        }

        var total = _waves.DoorWarningSeconds;
        return total > 0f ? Math.Clamp(1f - door.WarningLeft / total, 0f, 1f) : 1f;
    }

    public void Update(float deltaSeconds)
    {
        _timer -= deltaSeconds;
        if (_timer <= 0f && _queue.Count > 0 && TryAnnounceGroup())
        {
            _timer = _interval;
        }

        for (var i = 0; i < _doors.Length; i++)
        {
            var door = _doors[i];
            if (door.Group.Count == 0)
            {
                continue;
            }

            door.WarningLeft -= deltaSeconds;

            // A group that would exceed the cap waits behind its glowing door.
            if (door.WarningLeft <= 0f && _enemies.Count + door.Group.Count <= _maxAlive)
            {
                Release(i);
            }
        }
    }

    private bool TryAnnounceGroup()
    {
        // Pick a random door that is not already warning.
        var free = 0;
        for (var i = 0; i < _doors.Length; i++)
        {
            if (_doors[i].Group.Count == 0)
            {
                free++;
            }
        }

        if (free == 0)
        {
            return false;
        }

        var choice = _random.NextIndex(free);
        for (var i = 0; i < _doors.Length; i++)
        {
            var door = _doors[i];
            if (door.Group.Count > 0)
            {
                continue;
            }

            if (choice-- > 0)
            {
                continue;
            }

            var size = Math.Min(_groupSize, _queue.Count);
            for (var n = 0; n < size; n++)
            {
                var last = _queue.Count - 1;
                door.Group.Add(_queue[last]);
                _queue.RemoveAt(last);
            }

            door.WarningLeft = _waves.DoorWarningSeconds;
            _audio.PlaySfx(SfxIds.DoorWarning);
            return true;
        }

        return false;
    }

    private void Release(int doorIndex)
    {
        var door = _doors[doorIndex];
        var count = door.Group.Count;
        for (var i = 0; i < count; i++)
        {
            var config = door.Group[i];

            // Spread the group across the opening, with a little jitter so it does not look ruled.
            var slot = -1f + 2f * (i + 0.5f) / count;
            var lateral = slot + _random.Range(-0.5f, 0.5f) / count;
            _enemies.Spawn(config, _arena.GetDoorSpawnPoint(_arena.Doors[doorIndex], config.Radius, lateral));
        }

        door.Group.Clear();
    }

    private sealed class DoorSpawn
    {
        public List<EnemyConfig> Group { get; } = new();

        public float WarningLeft { get; set; }
    }
}
