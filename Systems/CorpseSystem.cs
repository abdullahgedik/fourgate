using System;
using Fourgate.Core;
using Fourgate.Core.Rendering;
using Fourgate.Core.Rendering.Models;
using Microsoft.Xna.Framework;

namespace Fourgate.Systems;

/// <summary>
/// Bodies of the dead, purely visual: a model topples over backward,
/// lies a moment, then shrinks away. A fixed-size buffer; the oldest gives way when it is full.
/// </summary>
public sealed class CorpseSystem
{
    private const int Capacity = 64;
    private const float FallSeconds = 0.45f;
    private const float ShrinkSeconds = 0.35f;

    private readonly Corpse[] _corpses = new Corpse[Capacity];
    private int _next;

    /// <summary>Bodies currently lying in the arena.</summary>
    public int Count
    {
        get
        {
            var count = 0;
            for (var i = 0; i < Capacity; i++)
            {
                if (_corpses[i].Model is not null)
                {
                    count++;
                }
            }

            return count;
        }
    }

    /// <param name="model">The model the body is drawn with.</param>
    /// <param name="position">Where it fell.</param>
    /// <param name="facing">Which way it was facing; it falls backward.</param>
    /// <param name="tint">Its colour for tinted parts.</param>
    /// <param name="radius">Its shadow while it falls.</param>
    /// <param name="lieSeconds">How long it lies before shrinking away.</param>
    public void Add(string model, Vector2 position, float facing, Color tint, float radius, float lieSeconds)
    {
        _corpses[_next] = new Corpse(model, position, facing, tint, radius, lieSeconds, 0f);
        _next = (_next + 1) % Capacity;
    }

    public void Update(float deltaSeconds)
    {
        for (var i = 0; i < Capacity; i++)
        {
            ref var corpse = ref _corpses[i];
            if (corpse.Model is null)
            {
                continue;
            }

            corpse.Age += deltaSeconds;
            if (corpse.Age >= FallSeconds + corpse.LieSeconds + ShrinkSeconds)
            {
                corpse = default;
            }
        }
    }

    public void Draw(IRenderer renderer)
    {
        for (var i = 0; i < Capacity; i++)
        {
            ref readonly var corpse = ref _corpses[i];
            if (corpse.Model is null)
            {
                continue;
            }

            var fall = Ease.OutCubic(corpse.Age / FallSeconds);
            var shrink = Math.Clamp((corpse.Age - FallSeconds - corpse.LieSeconds) / ShrinkSeconds, 0f, 1f);
            var pose = new ModelPose(
                corpse.Model, corpse.Position, corpse.Facing, corpse.Facing, 0f, 0f, corpse.Tint,
                Fall: fall, Shrink: Ease.InCubic(shrink), ShadowRadius: corpse.Radius);
            renderer.DrawModel(pose, default);
        }
    }

    private struct Corpse
    {
        public Corpse(string model, Vector2 position, float facing, Color tint, float radius, float lieSeconds, float age)
        {
            Model = model;
            Position = position;
            Facing = facing;
            Tint = tint;
            Radius = radius;
            LieSeconds = lieSeconds;
            Age = age;
        }

        public string? Model;
        public Vector2 Position;
        public float Facing;
        public Color Tint;
        public float Radius;
        public float LieSeconds;
        public float Age;
    }
}
