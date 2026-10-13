using Fourgate.Core.Data;
using Fourgate.Core.Rendering;
using Microsoft.Xna.Framework;

namespace Fourgate.Entities.Pickups;

/// <summary>A crate lying on the floor. Pooled; <see cref="Drop"/> resets all state.</summary>
public sealed class Crate
{
    /// <summary>Half the side length of the crate's square.</summary>
    public const float HalfSize = 16f;

    private const float BlinkInterval = 0.15f;

    private static readonly Color AmmoColor = new(200, 160, 70);
    private static readonly Color HealthColor = new(60, 170, 90);
    private static readonly Color UpgradeColor = new(170, 120, 255);
    private static readonly Color WeaponColor = new(60, 170, 230);

    public CrateType Type { get; private set; }

    /// <summary>The weapon inside a <see cref="CrateType.Weapon"/> crate.</summary>
    public WeaponConfig? Weapon { get; private set; }

    public Vector2 Position { get; private set; }

    /// <summary>Seconds until the crate disappears; infinite for crates that never expire.</summary>
    public float SecondsLeft { get; private set; }

    public bool IsExpired => SecondsLeft <= 0f;

    public VisualDescriptor Visual => new(
        ShapeKind.Square,
        HalfSize,
        Type switch
        {
            CrateType.Health => HealthColor,
            CrateType.Upgrade => UpgradeColor,
            CrateType.Weapon => WeaponColor,
            _ => AmmoColor,
        });

    public void Drop(CrateType type, Vector2 position, float lifetimeSeconds, WeaponConfig? weapon)
    {
        Type = type;
        Position = position;
        SecondsLeft = lifetimeSeconds;
        Weapon = weapon;
    }

    public void Tick(float deltaSeconds)
    {
        SecondsLeft -= deltaSeconds;
    }

    /// <summary>False on the "off" beats of the blink that warns the crate is about to disappear.</summary>
    public bool IsVisible(float blinkSeconds)
    {
        if (SecondsLeft >= blinkSeconds)
        {
            return true;
        }

        return (int)(SecondsLeft / BlinkInterval) % 2 == 0;
    }
}
