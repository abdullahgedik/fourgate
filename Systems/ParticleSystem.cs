using System;
using Fourgate.Core.Data;
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
    private const float ShotHeight = 30f;
    private const float SplatAlpha = 0.6f;

    // Lasting stains are nearly solid and a little brighter than flying drops, so fresh blood
    // shows on top of soot and older stains (newer marks cover older ones).
    private const float StainAlpha = 0.85f;

    // A hit of this damage bleeds at the presets' own sizes; see BleedScale.
    private const float BleedReferenceDamage = 25f;
    private const float MinBleedScale = 0.6f;
    private const float MaxBleedScale = 1.6f;

    // Sparks a fully glowing door sends out each second.
    private const float DoorSparksPerSecond = 90f;

    private static readonly Color FlameHot = new(255, 230, 140);
    private static readonly Color FlameCool = new(210, 45, 10);
    private static readonly Color SparkHot = new(255, 250, 220);
    private static readonly Color SparkCool = new(255, 150, 40);
    private static readonly Color SmokeDark = new(45, 42, 40);
    private static readonly Color SmokeLight = new(90, 86, 82);
    private static readonly Color BloodBright = new(170, 30, 25);
    private static readonly Color BloodDark = new(70, 10, 10);
    private static readonly Color BloodStain = new(115, 18, 15);
    private static readonly Color GlassLight = new(200, 240, 200);
    private static readonly Color GlassDark = new(90, 140, 90);
    private static readonly Color DustLight = new(150, 150, 160);
    private static readonly Color DustDark = new(80, 80, 90);
    private static readonly Color ChipLight = new(120, 120, 130);
    private static readonly Color ChipDark = new(60, 60, 68);
    private static readonly Color EnergyHot = new(235, 255, 255);
    private static readonly Color EnergyCool = new(60, 200, 255);
    private static readonly Color ScorchSmoke = new(40, 50, 60);

    // Pull on drops, chips and sparks that fall back to the floor (world units per second squared).
    private const float Gravity = 650f;

    private readonly Particle[] _particles = new Particle[Capacity];
    private readonly GameRandom _random;
    private int _count;

    public ParticleSystem(GameRandom random)
    {
        _random = random;
    }

    public int Count => _count;

    /// <summary>
    /// Where landed blood goes to stay for the run; without it, splats lie on the floor for a
    /// couple of seconds and fade.
    /// </summary>
    public FloorStains? Stains { get; set; }

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

    /// <summary>
    /// A fire bomb bursting on the floor: a flash, glass shards, and burning splashes flung out
    /// across the area the fire will cover, with a puff of flame rising from the middle.
    /// </summary>
    public void FireSplash(Vector2 center, float radius)
    {
        Add(new Particle
        {
            Position = center,
            BaseHeight = 6f,
            Life = 0.12f,
            SizeStart = radius * 0.5f,
            SizeEnd = radius * 0.9f,
            ColorStart = FlameHot,
            ColorEnd = FlameCool,
            AlphaStart = 0.9f,
        });

        // Burning splashes arc out and fall inside the fire's reach.
        var splashes = (int)Math.Clamp(radius * 0.35f, 12f, 40f);
        for (var i = 0; i < splashes; i++)
        {
            var size = _random.Range(3f, 6f);
            Add(new Particle
            {
                Position = center,
                BaseHeight = 4f,
                Velocity = RandomDirection() * (radius * _random.Range(0.6f, 2.4f)),
                Climb = _random.Range(70f, 200f),
                Gravity = Gravity,
                Drag = 1.5f,
                Life = 1.2f,
                SizeStart = size,
                SizeEnd = size * 0.7f,
                ColorStart = FlameHot,
                ColorEnd = FlameCool,
                AlphaStart = 1f,
            });
        }

        // Glass from the bottle.
        for (var i = 0; i < 10; i++)
        {
            Add(new Particle
            {
                Position = center,
                BaseHeight = 4f,
                Velocity = RandomDirection() * _random.Range(120f, 380f),
                Climb = _random.Range(60f, 220f),
                Gravity = Gravity,
                Drag = 2f,
                Life = 1f,
                SizeStart = _random.Range(1.5f, 2.5f),
                SizeEnd = 1.5f,
                ColorStart = GlassLight,
                ColorEnd = GlassDark,
                AlphaStart = 1f,
            });
        }

        // A puff of flame rising as the fuel catches.
        for (var i = 0; i < 8; i++)
        {
            Add(new Particle
            {
                Position = center + RandomInDisc(radius * 0.3f),
                BaseHeight = 4f,
                Velocity = RandomDirection() * _random.Range(20f, 60f),
                Climb = _random.Range(60f, 120f),
                Drag = 2f,
                Rise = 30f,
                Life = _random.Range(0.35f, 0.6f),
                SizeStart = radius * 0.18f,
                SizeEnd = radius * 0.06f,
                ColorStart = FlameHot,
                ColorEnd = FlameCool,
                AlphaStart = 0.9f,
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

    /// <summary>
    /// Sparks drifting out of a door that is about to let enemies in, along its opening and up
    /// its height; call every frame it glows.
    /// </summary>
    /// <param name="from">One end of the door's inner edge.</param>
    /// <param name="to">The other end.</param>
    /// <param name="inward">Unit direction into the arena.</param>
    /// <param name="height">How tall the door is.</param>
    /// <param name="strength">0–1; more sparks the nearer the enemies are.</param>
    public void DoorSparks(
        Vector2 from, Vector2 to, Vector2 inward, float height, float strength, Color hot, Color cool, float deltaSeconds)
    {
        var sparks = Spawns(DoorSparksPerSecond * strength * deltaSeconds);
        for (var i = 0; i < sparks; i++)
        {
            Add(new Particle
            {
                Position = Vector2.Lerp(from, to, _random.NextFloat()),
                BaseHeight = _random.Range(0f, height),
                Velocity = Turn(inward, _random.Range(-0.5f, 0.5f)) * _random.Range(40f, 140f),
                Climb = _random.Range(10f, 40f),
                Drag = 1.2f,
                Rise = 15f,
                Life = _random.Range(0.5f, 0.9f),
                SizeStart = _random.Range(2.5f, 4.5f),
                SizeEnd = 1f,
                ColorStart = hot,
                ColorEnd = cool,
                AlphaStart = 0.9f,
            });
        }
    }

    /// <summary>What a gun's shot throws off where it hits a body, by the gun's hit effect.</summary>
    /// <param name="height">Where on the body it struck, above the floor (3D only).</param>
    /// <param name="damage">Damage of the hit: harder hits throw bigger drops and leave bigger stains.</param>
    public void BodyHit(HitEffect effect, Vector2 position, Vector2 shotDirection, float height, float damage = BleedReferenceDamage)
    {
        var bleed = BleedScale(damage);
        switch (effect)
        {
            case HitEffect.Pellet:
                Drops(position, height, shotDirection, 3, 1.1f, 60f, 220f, 2f * bleed, 3.5f * bleed, 3f * bleed);
                Mist(position, height, shotDirection, 1, 4f * bleed);
                break;

            case HitEffect.Heavy:
                Drops(position, height, shotDirection, 7, 0.6f, 120f, 340f, 3f * bleed, 5f * bleed, 5f * bleed);
                Mist(position, height, shotDirection, 3, 7f * bleed);
                break;

            case HitEffect.Sniper:
                Flash(position, height, 18f, SparkHot);
                Drops(position, height, shotDirection, 14, 0.35f, 260f, 680f, 3f * bleed, 6f * bleed, 7f * bleed);
                Mist(position, height, shotDirection, 6, 11f * bleed);
                break;

            case HitEffect.Energy:
                Flash(position, height, 16f, EnergyHot);
                GlowSparks(position, height, shotDirection, 9);
                Smoke(position, height, 2, ScorchSmoke);
                break;

            default:
                Drops(position, height, shotDirection, 4, 0.7f, 60f, 190f, 2.5f * bleed, 4f * bleed, 4f * bleed);
                Mist(position, height, shotDirection, 2, 4.5f * bleed);
                break;
        }
    }

    /// <summary>
    /// How much bigger than usual a hit's drops and stains are: grows with the square root of
    /// the damage (a bigger calibre bleeds more), within limits so tiny or huge hits stay sensible.
    /// </summary>
    public static float BleedScale(float damage)
    {
        return Math.Clamp(MathF.Sqrt(Math.Max(0f, damage) / BleedReferenceDamage), MinBleedScale, MaxBleedScale);
    }

    /// <summary>What a gun's bullet kicks up where it stops on a wall or pillar, by the gun's hit effect.</summary>
    public void WallHit(HitEffect effect, Vector2 position, Vector2 shotDirection)
    {
        var height = ShotHeight;
        switch (effect)
        {
            case HitEffect.Heavy:
                Dust(position, height);
                Chips(position, height, shotDirection, 4);
                break;

            case HitEffect.Sniper:
                Dust(position, height);
                Dust(position, height);
                Chips(position, height, shotDirection, 9);
                Sparks(position);
                break;

            case HitEffect.Energy:
                Flash(position, height, 14f, EnergyHot);
                GlowSparks(position, height, -shotDirection, 6);
                break;

            default:
                Dust(position, height);
                break;
        }
    }

    /// <summary>A puff of dust where a bullet stops on a wall or pillar, at the height it struck.</summary>
    public void Dust(Vector2 position, float height)
    {
        for (var i = 0; i < 4; i++)
        {
            Add(new Particle
            {
                Position = position,
                BaseHeight = height,
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
                Climb = _random.Range(40f, 140f),
                Gravity = Gravity,
                Drag = 5f,
                Life = _random.Range(0.15f, 0.3f),
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
            particle.Climb = particle.Climb * keep + (particle.Rise - particle.Gravity) * deltaSeconds;
            particle.Position += particle.Velocity * deltaSeconds;
            particle.Lift += particle.Climb * deltaSeconds;
            if (particle.Gravity > 0f && particle.BaseHeight + particle.Lift <= 0f)
            {
                Land(ref particle);
            }
        }
    }

    // A falling drop becomes a splat on the floor (a lasting stain when there is a stain layer);
    // anything else just dies there.
    private void Land(ref Particle particle)
    {
        particle.Lift = -particle.BaseHeight;
        particle.Velocity = Vector2.Zero;
        particle.Climb = 0f;
        particle.Gravity = 0f;
        if (particle.SplatSize <= 0f)
        {
            particle.Age = particle.Life;
            return;
        }

        if (Stains is not null)
        {
            Stains.Add(particle.Position, particle.SplatSize, BloodStain * StainAlpha);
            particle.Age = particle.Life;
            return;
        }

        particle.IsSplat = true;
        particle.Age = 0f;
        particle.Life = particle.SplatLife;
        particle.SizeStart = particle.SplatSize;
        particle.SizeEnd = particle.SplatSize * 1.15f;
        particle.ColorStart = particle.ColorEnd;
        particle.AlphaStart = SplatAlpha;
    }

    /// <summary>Splats lying on the floor; drawn before bodies so they stay under them.</summary>
    public void DrawFloor(IRenderer renderer)
    {
        for (var i = 0; i < _count; i++)
        {
            ref readonly var particle = ref _particles[i];
            if (!particle.IsSplat)
            {
                continue;
            }

            var t = particle.Age / particle.Life;
            var size = MathHelper.Lerp(particle.SizeStart, particle.SizeEnd, t);
            // Stays solid, then fades over the last third.
            var fade = Math.Min(1f, (1f - t) * 3f);
            renderer.FillCircle(particle.Position, size, particle.ColorStart * (particle.AlphaStart * fade));
        }
    }

    // Blood drops thrown along the shot that arc up, fall and leave splats on the floor.
    private void Drops(
        Vector2 position, float height, Vector2 direction, int count, float spread, float minSpeed, float maxSpeed,
        float minSize, float maxSize, float splatSize)
    {
        for (var i = 0; i < count; i++)
        {
            var size = _random.Range(minSize, maxSize);
            Add(new Particle
            {
                Position = position,
                BaseHeight = height,
                Velocity = Turn(direction, _random.Range(-spread, spread)) * _random.Range(minSpeed, maxSpeed),
                Climb = _random.Range(40f, 170f),
                Gravity = Gravity,
                Drag = 1.5f,
                Life = 1.2f,
                SizeStart = size,
                SizeEnd = size * 0.8f,
                ColorStart = BloodBright,
                ColorEnd = BloodDark,
                AlphaStart = 0.95f,
                SplatSize = splatSize * _random.Range(0.6f, 1.2f),
                SplatLife = _random.Range(1.6f, 2.6f),
            });
        }
    }

    // A soft red mist hanging where the shot went through.
    private void Mist(Vector2 position, float height, Vector2 direction, int count, float size)
    {
        for (var i = 0; i < count; i++)
        {
            Add(new Particle
            {
                Position = position,
                BaseHeight = height,
                Velocity = Turn(direction, _random.Range(-0.6f, 0.6f)) * _random.Range(40f, 150f),
                Drag = 5f,
                Rise = 10f,
                Life = _random.Range(0.25f, 0.45f),
                SizeStart = size,
                SizeEnd = size * 1.8f,
                ColorStart = BloodBright,
                ColorEnd = BloodDark,
                AlphaStart = 0.5f,
            });
        }
    }

    // One bright blink at the point of impact.
    private void Flash(Vector2 position, float height, float size, Color color)
    {
        Add(new Particle
        {
            Position = position,
            BaseHeight = height,
            Life = 0.09f,
            SizeStart = size,
            SizeEnd = size * 1.6f,
            ColorStart = color,
            ColorEnd = color,
            AlphaStart = 1f,
        });
    }

    // Glowing sparks that fly out, mostly along the shot, and fall back down.
    private void GlowSparks(Vector2 position, float height, Vector2 direction, int count)
    {
        for (var i = 0; i < count; i++)
        {
            Add(new Particle
            {
                Position = position,
                BaseHeight = height,
                Velocity = Turn(direction, _random.Range(-1.4f, 1.4f)) * _random.Range(90f, 320f),
                Climb = _random.Range(60f, 220f),
                Gravity = Gravity * 0.8f,
                Drag = 2f,
                Life = _random.Range(0.35f, 0.6f),
                SizeStart = _random.Range(2f, 3.5f),
                SizeEnd = 1f,
                ColorStart = EnergyHot,
                ColorEnd = EnergyCool,
                AlphaStart = 1f,
            });
        }
    }

    // Thin smoke rising from a burn.
    private void Smoke(Vector2 position, float height, int count, Color color)
    {
        for (var i = 0; i < count; i++)
        {
            Add(new Particle
            {
                Position = position + RandomDirection() * _random.Range(0f, 6f),
                BaseHeight = height,
                Velocity = RandomDirection() * _random.Range(5f, 20f),
                Climb = _random.Range(20f, 40f),
                Drag = 1.5f,
                Rise = 25f,
                Life = _random.Range(0.6f, 0.9f),
                SizeStart = 4f,
                SizeEnd = 10f,
                ColorStart = color,
                ColorEnd = color,
                AlphaStart = 0.35f,
            });
        }
    }

    // Bits of wall knocked back toward the shooter, falling to the floor.
    private void Chips(Vector2 position, float height, Vector2 shotDirection, int count)
    {
        for (var i = 0; i < count; i++)
        {
            Add(new Particle
            {
                Position = position,
                BaseHeight = height,
                Velocity = Turn(-shotDirection, _random.Range(-1.2f, 1.2f)) * _random.Range(80f, 240f),
                Climb = _random.Range(60f, 200f),
                Gravity = Gravity,
                Drag = 1.5f,
                Life = 1f,
                SizeStart = _random.Range(2f, 3.5f),
                SizeEnd = 2f,
                ColorStart = ChipLight,
                ColorEnd = ChipDark,
                AlphaStart = 1f,
            });
        }
    }

    public void Draw(IRenderer renderer)
    {
        for (var i = 0; i < _count; i++)
        {
            ref readonly var particle = ref _particles[i];
            if (particle.IsSplat)
            {
                continue;
            }

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
        public float Gravity;

        // Size and life of the splat a falling drop leaves on the floor; 0 for none.
        public float SplatSize;
        public float SplatLife;
        public bool IsSplat;
    }
}
