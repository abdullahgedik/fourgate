using System.Collections.Generic;
using Fourgate.Core.Pooling;
using Fourgate.Core.Rendering;
using Fourgate.Entities;
using Fourgate.Entities.Projectiles;
using Fourgate.Weapons;
using Microsoft.Xna.Framework;

namespace Fourgate.Systems;

/// <summary>Owns the flames of stream weapons: their flight, the burning, and the fire trail they leave.</summary>
public sealed class FlameSystem
{
    // Embers flying off each flame of a flamethrower stream.
    private const float FlameEmbersPerSecond = 6f;

    // How high the stream flies.
    private const float FlameHeight = 28f;

    private const int InitialFlames = 128;

    private readonly Arena _arena;
    private readonly HazardSystem _hazards;
    private readonly Pool<Flame> _flames = new(InitialFlames);
    private readonly List<IHittable> _targets = new();
    private readonly List<IHittableSource> _sources = new();
    private readonly List<IHittable> _touching = new();

    public FlameSystem(Arena arena, HazardSystem hazards)
    {
        _arena = arena;
        _hazards = hazards;
    }

    public int Count => _flames.Count;

    /// <summary>Debug switch: every flame kills what it touches.</summary>
    public bool OneShotKills { get; set; }

    /// <summary>Registers a single thing flames can burn.</summary>
    public void AddTarget(IHittable target)
    {
        _targets.Add(target);
    }

    /// <summary>Registers a searchable group of things flames can burn (the enemies).</summary>
    public void AddSource(IHittableSource source)
    {
        _sources.Add(source);
    }

    public void Emit(in FlameShot shot)
    {
        _flames.Rent().Launch(shot);
    }

    /// <summary>Where visual particles go; null draws none (tests).</summary>
    public ParticleSystem? Particles { get; set; }

    public void Update(float deltaSeconds)
    {
        for (var i = _flames.Count - 1; i >= 0; i--)
        {
            var flame = _flames[i];
            flame.Advance(deltaSeconds);
            Particles?.Embers(flame.Position, FlameHeight, FlameEmbersPerSecond, flame.Radius * 0.25f, deltaSeconds);

            if (HitsWall(flame))
            {
                flame.IsSpent = true;
            }
            else
            {
                Burn(flame);
            }

            LeaveTrail(flame);
            if (flame.IsSpent)
            {
                _flames.ReturnAt(i);
            }
        }
    }

    public void Draw(IRenderer renderer)
    {
        for (var i = 0; i < _flames.Count; i++)
        {
            var flame = _flames[i];
            var visual = flame.Visual;
            renderer.FillCircleRaised(flame.Position, FlameHeight, visual.Radius, visual.Color);
        }
    }

    private void Burn(Flame flame)
    {
        _touching.Clear();
        for (var i = 0; i < _targets.Count; i++)
        {
            if (Vector2.Distance(_targets[i].Position, flame.Position) <= flame.Radius + _targets[i].Radius)
            {
                _touching.Add(_targets[i]);
            }
        }

        for (var i = 0; i < _sources.Count; i++)
        {
            _sources[i].Collect(flame.Position, flame.Radius, _touching);
        }

        var damage = OneShotKills ? float.MaxValue : flame.Shot.Damage;
        for (var i = 0; i < _touching.Count; i++)
        {
            if (!flame.TryTouch(_touching[i]))
            {
                continue;
            }

            // Fire damage: no hit flash or hit sound for each of the many small flames.
            _touching[i].Burn(damage, flame.Shot.OwnerIndex);
            if (!flame.Shot.PassThrough)
            {
                flame.IsSpent = true;
                return;
            }
        }
    }

    private bool HitsWall(Flame flame)
    {
        return _arena.IsSolid(flame.Position);
    }

    // Pools are dropped along the path the flame has covered, spacing apart.
    private void LeaveTrail(Flame flame)
    {
        var shot = flame.Shot;
        if (shot.TrailSeconds <= 0f)
        {
            return;
        }

        while (flame.TryTakeTrailPoint(out var point))
        {
            var position = _arena.PlaceOnFloor(point, shot.TrailRadius * 0.5f);
            // The trail is laid where the player walks; it burns enemies only.
            _hazards.SpawnFire(
                position, shot.TrailRadius, shot.TrailSeconds, shot.TrailDamagePerSecond, shot.OwnerIndex, 0f,
                hurtsPlayers: false);
        }
    }
}
