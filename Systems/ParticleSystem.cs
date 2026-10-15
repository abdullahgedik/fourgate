using System;
using Fourgate.Core.Rendering;
using Fourgate.Core.Rng;
using Microsoft.Xna.Framework;

namespace Fourgate.Systems;

/// <summary>
/// Purely visual particles: explosion bursts, flames over fire pools and burning enemies, ember
/// trails. A fixed-size buffer, so nothing is allocated while playing; when it is full new
/// particles are skipped. It has its own random source, so it never changes gameplay randomness.
/// </summary>
public sealed class ParticleSystem
{
    private const int Capacity = 4000;

    // Starting heights (3D only) of things born in the air: shot impacts and dust.
    private const float ShotHeight = 18f;
    private const float DustHeight = 4f;

    private static readonly Color FlameHot = new(255, 230, 140);
    private static readonly Color FlameCool = new(210, 45, 10);
    private static readonly Color SparkHot = new(255, 250, 220);
    private static readonly Color SparkCool = new(255, 150, 40);
    private static readonly Color SmokeDark = new(45, 42, 40);
    private static readonly Color SmokeLight = new(90, 86, 82);
    private static readonly Color BloodBright = new(170, 30, 25);
    private static readonly Color BloodDark = new(70, 10, 10);
    private static readonly Color DustLight = new(150, 150, 160);
    private static readonly Color DustDark = new(80, 80, 90);

    private readonly Particle[] _particles = new Particle[Capacity];
    private readonly GameRandom _random;
    private int _count;

    public ParticleSystem(GameRandom random)
    {
        _random = random;
    }

    public int Count => _count;

    /// <summary>A fireball burst, sparks and rising smoke, sized by the blast radius.</summary>
    public void Explosion(Vector2 center, float radius)
    {
        var flames = (int)Math.Clamp(radius * 0.3f, 14f, 70f);
        for (var i = 0; i < flames; i++)
        {
            var direction = RandomDirection();
            var speed = _random.Range(0.6f, 3.2f) * radius;
            Add(new Particle
            {
                Position = center + direction * _random.Range(0f, radius * 0.25f),
                Velocity = direction * speed,
                Climb = _random.Range(0f, 50f),
                Drag = 5f,
                Rise = 30f,
                Life = _random.Range(0.35f, 0.7f),
                SizeStart = radius * _random.Range(0.12f, 0.22f),
                SizeEnd = radius * 0.04f,
                ColorStart = FlameHot,
                ColorEnd = FlameCool,
                AlphaStart = 0.95f,
            });
        }

        var sparks = (int)Math.Clamp(radius * 0.12f, 6f, 28f);
        for (var i = 0; i < sparks; i++)
        {
            var direction = RandomDirection();
            Add(new Particle
            {
                Position = center,
                Velocity = direction * _random.Range(3f, 6f) * radius,
                Drag = 3f,
                Life = _random.Range(0.2f, 0.4f),
                SizeStart = _random.Range(2.5f, 4.5f),
                SizeEnd = 1f,
                ColorStart = SparkHot,
                ColorEnd = SparkCool,
                AlphaStart = 1f,
            });
        }

        var smoke = (int)Math.Clamp(radius * 0.08f, 4f, 16f);
        for (var i = 0; i < smoke; i++)
        {
            var direction = RandomDirection();
            Add(new Particle
            {
                Position = center + direction * _random.Range(0f, radius * 0.5f),
                Velocity = direction * _random.Range(20f, 60f),
                Climb = _random.Range(10f, 40f),
                Drag = 1.5f,
                Rise = 15f,
                Life = _random.Range(0.9f, 1.6f),
                SizeStart = radius * 0.15f,
                SizeEnd = radius * 0.35f,
                ColorStart = SmokeDark,
                ColorEnd = SmokeLight,
                AlphaStart = 0.4f,
            });
        }
    }

    /// <summary>Flames licking up over a fire pool; call every frame it burns.</summary>
    /// <param name="center">Middle of the pool.</param>
    /// <param name="radius">Its size.</param>
    /// <param name="strength">0–1; a fading pool burns lower.</param>
    /// <param name="deltaSeconds">Frame time.</param>
    public void FirePool(Vector2 center, float radius, float strength, float deltaSeconds)
    {
        var flames = Spawns((20f + radius * 0.35f) * strength * deltaSeconds);
        for (var i = 0; i < flames; i++)
        {
            Add(new Particle
            {
                Position = center + RandomInDisc(radius * 0.9f),
                Velocity = RandomDirection() * _random.Range(0f, 12f),
                Climb = _random.Range(25f, 70f),
                Drag = 0.5f,
                Rise = 50f,
                Life = _random.Range(0.35f, 0.75f),
                SizeStart = _random.Range(6f, 13f),
                SizeEnd = 1.5f,
                ColorStart = FlameHot,
                ColorEnd = FlameCool,
                AlphaStart = 0.85f * strength,
            });
        }

        var smoke = Spawns(3f * strength * deltaSeconds);
        for (var i = 0; i < smoke; i++)
        {
            Add(new Particle
            {
                Position = center + RandomInDisc(radius * 0.7f),
                Climb = _random.Range(20f, 45f),
                Drag = 0.5f,
                Rise = 10f,
                Life = _random.Range(1f, 1.6f),
                SizeStart = 10f,
                SizeEnd = 26f,
                ColorStart = SmokeDark,
                ColorEnd = SmokeLight,
                AlphaStart = 0.25f * strength,
            });
        }
    }

    /// <summary>Small flames rising from a burning body; call every frame it burns.</summary>
    public void Burning(Vector2 center, float radius, float deltaSeconds)
    {
        var flames = Spawns((14f + radius * 0.8f) * deltaSeconds);
        for (var i = 0; i < flames; i++)
        {
            Add(new Particle
            {
                Position = center + RandomInDisc(radius * 0.8f),
                BaseHeight = radius * 0.6f,
                Velocity = RandomDirection() * _random.Range(0f, 15f),
                Climb = _random.Range(40f, 90f),
                Drag = 0.5f,
                Rise = 60f,
                Life = _random.Range(0.25f, 0.5f),
                SizeStart = _random.Range(4f, 8f) + radius * 0.1f,
                SizeEnd = 1f,
                ColorStart = FlameHot,
                ColorEnd = FlameCool,
                AlphaStart = 0.9f,
            });
        }
    }

    /// <summary>Glowing embers left behind something hot that is moving; call every frame.</summary>
    /// <param name="position">Where it is now.</param>
    /// <param name="height">How high it flies (3D only).</param>
    /// <param name="perSecond">How many embers a second.</param>
    /// <param name="size">Size of a fresh ember.</param>
    /// <param name="deltaSeconds">Frame time.</param>
    public void Embers(Vector2 position, float height, float perSecond, float size, float deltaSeconds)
    {
        var embers = Spawns(perSecond * deltaSeconds);
        for (var i = 0; i < embers; i++)
        {
            Add(new Particle
            {
                Position = position + RandomInDisc(size),
                BaseHeight = height,
                Velocity = RandomDirection() * _random.Range(5f, 30f),
                Climb = 20f,
                Drag = 1f,
                Rise = 20f,
                Life = _random.Range(0.2f, 0.45f),
                SizeStart = size,
                SizeEnd = 0.5f,
                ColorStart = FlameHot,
                ColorEnd = FlameCool,
                AlphaStart = 0.8f,
            });
        }
    }

    /// <summary>A small dark-red spray where a shot hits an enemy, thrown along the shot.</summary>
    public void HitPuff(Vector2 position, Vector2 shotDirection)
    {
        for (var i = 0; i < 5; i++)
        {
            Add(new Particle
            {
                Position = position,
                BaseHeight = ShotHeight,
                Velocity = Turn(shotDirection, _random.Range(-0.8f, 0.8f)) * _random.Range(80f, 240f),
                Drag = 6f,
                Life = _random.Range(0.2f, 0.35f),
                SizeStart = _random.Range(3f, 5.5f),
                SizeEnd = 1.5f,
                ColorStart = BloodBright,
                ColorEnd = BloodDark,
                AlphaStart = 0.9f,
            });
        }
    }

    /// <summary>A puff of dust where a bullet stops on a wall or pillar.</summary>
    public void Dust(Vector2 position)
    {
        for (var i = 0; i < 4; i++)
        {
            Add(new Particle
            {
                Position = position,
                BaseHeight = DustHeight,
                Velocity = RandomDirection() * _random.Range(30f, 90f),
                Drag = 5f,
                Life = _random.Range(0.2f, 0.35f),
                SizeStart = _random.Range(3f, 5f),
                SizeEnd = 6f,
                ColorStart = DustLight,
                ColorEnd = DustDark,
                AlphaStart = 0.6f,
            });
        }
    }

    /// <summary>A few bright sparks (a ricochet off a wall).</summary>
    public void Sparks(Vector2 position)
    {
        for (var i = 0; i < 6; i++)
        {
            Add(new Particle
            {
                Position = position,
                BaseHeight = ShotHeight,
                Velocity = RandomDirection() * _random.Range(150f, 350f),
                Drag = 5f,
                Life = _random.Range(0.12f, 0.25f),
                SizeStart = 2.5f,
                SizeEnd = 1f,
                ColorStart = SparkHot,
                ColorEnd = SparkCool,
                AlphaStart = 1f,
            });
        }
    }

    /// <summary>Sparks and smoke boiling off where a beam strikes; call every frame it does.</summary>
    public void BeamImpact(Vector2 point, float height, Color hot, Color cool, float deltaSeconds)
    {
        var sparks = Spawns(70f * deltaSeconds);
        for (var i = 0; i < sparks; i++)
        {
            Add(new Particle
            {
                Position = point,
                BaseHeight = height,
                Velocity = RandomDirection() * _random.Range(120f, 320f),
                Climb = _random.Range(20f, 120f),
                Drag = 4f,
                Rise = -60f,
                Life = _random.Range(0.15f, 0.35f),
                SizeStart = _random.Range(2.5f, 4.5f),
                SizeEnd = 1f,
                ColorStart = hot,
                ColorEnd = cool,
                AlphaStart = 1f,
            });
        }

        var smoke = Spawns(8f * deltaSeconds);
        for (var i = 0; i < smoke; i++)
        {
            Add(new Particle
            {
                Position = point + RandomInDisc(12f),
                Velocity = RandomDirection() * _random.Range(10f, 30f),
                Climb = _random.Range(30f, 60f),
                Drag = 1f,
                Rise = 10f,
                Life = _random.Range(0.7f, 1.2f),
                SizeStart = 8f,
                SizeEnd = 22f,
                ColorStart = SmokeDark,
                ColorEnd = SmokeLight,
                AlphaStart = 0.35f,
            });
        }
    }

    /// <summary>Glints drifting off the length of a beam; call every frame it is on.</summary>
    public void AlongBeam(Vector2 from, Vector2 to, float height, Color hot, Color cool, float deltaSeconds)
    {
        var length = Vector2.Distance(from, to);
        var glints = Spawns(length * 0.12f * deltaSeconds);
        for (var i = 0; i < glints; i++)
        {
            Add(new Particle
            {
                Position = Vector2.Lerp(from, to, _random.NextFloat()),
                BaseHeight = height,
                Velocity = RandomDirection() * _random.Range(10f, 40f),
                Climb = _random.Range(10f, 50f),
                Drag = 2f,
                Life = _random.Range(0.2f, 0.4f),
                SizeStart = _random.Range(3f, 5f),
                SizeEnd = 1f,
                ColorStart = hot,
                ColorEnd = cool,
                AlphaStart = 0.9f,
            });
        }
    }

    /// <summary>A one-off burst along a beam that just fired, and a flash where it ended.</summary>
    public void BeamFlash(Vector2 from, Vector2 to, float height, Color hot, Color cool)
    {
        var length = Vector2.Distance(from, to);
        var count = (int)Math.Clamp(length / 30f, 6f, 80f);
        for (var i = 0; i < count; i++)
        {
            Add(new Particle
            {
                Position = Vector2.Lerp(from, to, _random.NextFloat()),
                BaseHeight = height,
                Velocity = RandomDirection() * _random.Range(20f, 90f),
                Climb = _random.Range(-20f, 40f),
                Drag = 3f,
                Life = _random.Range(0.25f, 0.5f),
                SizeStart = _random.Range(2.5f, 5f),
                SizeEnd = 1f,
                ColorStart = hot,
                ColorEnd = cool,
                AlphaStart = 1f,
            });
        }

        for (var i = 0; i < 14; i++)
        {
            Add(new Particle
            {
                Position = to,
                BaseHeight = height,
                Velocity = RandomDirection() * _random.Range(100f, 300f),
                Climb = _random.Range(0f, 120f),
                Drag = 4f,
                Rise = -60f,
                Life = _random.Range(0.2f, 0.4f),
                SizeStart = _random.Range(3f, 6f),
                SizeEnd = 1f,
                ColorStart = hot,
                ColorEnd = cool,
                AlphaStart = 1f,
            });
        }
    }

    /// <summary>Flames rising along a stream of fire; call every frame it burns.</summary>
    public void FireAlong(Vector2 from, Vector2 to, float height, float deltaSeconds)
    {
        var length = Vector2.Distance(from, to);
        var flames = Spawns(length * 0.25f * deltaSeconds);
        for (var i = 0; i < flames; i++)
        {
            var t = _random.NextFloat();
            Add(new Particle
            {
                Position = Vector2.Lerp(from, to, t) + RandomInDisc(10f + 20f * t),
                BaseHeight = height,
                Velocity = RandomDirection() * _random.Range(0f, 20f),
                Climb = _random.Range(30f, 80f),
                Drag = 0.5f,
                Rise = 50f,
                Life = _random.Range(0.25f, 0.5f),
                SizeStart = _random.Range(6f, 12f) * (0.6f + t),
                SizeEnd = 1.5f,
                ColorStart = FlameHot,
                ColorEnd = FlameCool,
                AlphaStart = 0.85f,
            });
        }
    }

    public void Update(float deltaSeconds)
    {
        for (var i = _count - 1; i >= 0; i--)
        {
            ref var particle = ref _particles[i];
            particle.Age += deltaSeconds;
            if (particle.Age >= particle.Life)
            {
                _particles[i] = _particles[_count - 1];
                _count--;
                continue;
            }

            // Movement over the floor and climbing are kept apart, so 3D can show real height.
            var keep = MathF.Max(0f, 1f - particle.Drag * deltaSeconds);
            particle.Velocity *= keep;
            particle.Climb = particle.Climb * keep + particle.Rise * deltaSeconds;
            particle.Position += particle.Velocity * deltaSeconds;
            particle.Lift += particle.Climb * deltaSeconds;
        }
    }

    public void Draw(IRenderer renderer)
    {
        for (var i = 0; i < _count; i++)
        {
            ref readonly var particle = ref _particles[i];
            var t = particle.Age / particle.Life;
            var size = MathHelper.Lerp(particle.SizeStart, particle.SizeEnd, t);
            var color = Color.Lerp(particle.ColorStart, particle.ColorEnd, t);
            renderer.DrawParticle(
                particle.Position, particle.BaseHeight, particle.Lift, size, color * (particle.AlphaStart * (1f - t)));
        }
    }

    // Whole particles for an expected count, with the fraction rolled: 2.3 gives 2, or 3 three times in ten.
    private int Spawns(float expected)
    {
        var whole = (int)expected;
        return whole + (_random.NextFloat() < expected - whole ? 1 : 0);
    }

    private Vector2 RandomDirection()
    {
        var angle = _random.Range(0f, MathHelper.TwoPi);
        return new Vector2(MathF.Cos(angle), MathF.Sin(angle));
    }

    private static Vector2 Turn(Vector2 direction, float radians)
    {
        var cos = MathF.Cos(radians);
        var sin = MathF.Sin(radians);
        return new Vector2(direction.X * cos - direction.Y * sin, direction.X * sin + direction.Y * cos);
    }

    private Vector2 RandomInDisc(float radius)
    {
        return RandomDirection() * (radius * MathF.Sqrt(_random.NextFloat()));
    }

    private void Add(in Particle particle)
    {
        if (_count < Capacity)
        {
            _particles[_count++] = particle;
        }
    }

    private struct Particle
    {
        public Vector2 Position;
        public Vector2 Velocity;
        public float BaseHeight;
        public float Lift;
        public float Climb;
        public float Age;
        public float Life;
        public float Drag;
        public float Rise;
        public float SizeStart;
        public float SizeEnd;
        public Color ColorStart;
        public Color ColorEnd;
        public float AlphaStart;
    }
}
