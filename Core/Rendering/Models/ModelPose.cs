using Microsoft.Xna.Framework;

namespace Fourgate.Core.Rendering.Models;

/// <summary>
/// What an entity reports for its model to be drawn: which model, where, which way, and how far
/// along its walk and its moves are. The renderer builds the pose; gameplay never sees a joint.
/// </summary>
/// <param name="ModelId">The model in models.json.</param>
/// <param name="Position">Where it stands on the floor.</param>
/// <param name="Facing">Which way its body faces (radians, 0 = +X).</param>
/// <param name="Aim">Which way its aiming parts turn (radians); the same as Facing for most.</param>
/// <param name="WalkDistance">Distance walked so far, which drives the step cycle.</param>
/// <param name="Speed">How fast it moves now (world units per second).</param>
/// <param name="Tint">The entity's own colour, for tinted parts.</param>
/// <param name="Recoil">0–1: a gun kick.</param>
/// <param name="Lunge">0–1: an attack lunge.</param>
/// <param name="Flinch">0–1: recoiling from a hit.</param>
/// <param name="Fall">0–1: toppling over backward (dead).</param>
/// <param name="Shrink">0–1: shrinking away (a corpse being cleared).</param>
/// <param name="GunId">The gun model held, or null for none.</param>
/// <param name="ShadowRadius">Size of its shadow on the floor; 0 for none.</param>
/// <param name="Hold">How it holds its gun, which can change the arms' pose.</param>
/// <param name="MoveHeading">Which way it is actually moving (radians); null when that is the way it faces.</param>
/// <param name="Windup">0–1: drawing back before a throw or a blow.</param>
/// <param name="Elevation">How high it is off the floor (a leap, a thrown grenade).</param>
/// <param name="Tumble">Spin end over end (radians), for something thrown.</param>
/// <param name="Glow">0–1: how strongly every part is washed with <paramref name="GlowColor"/> (a hit flash, burning).</param>
/// <param name="GlowColor">The colour of that wash.</param>
/// <param name="MuzzleFlash">0–1: the held gun's flash, drawn at its muzzle (and its exhaust).</param>
/// <param name="MuzzleFlashSize">How big that flash is at its brightest.</param>
public readonly record struct ModelPose(
    string ModelId,
    Vector2 Position,
    float Facing,
    float Aim,
    float WalkDistance,
    float Speed,
    Color Tint,
    float Recoil = 0f,
    float Lunge = 0f,
    float Flinch = 0f,
    float Fall = 0f,
    float Shrink = 0f,
    string? GunId = null,
    float ShadowRadius = 0f,
    ModelHold Hold = ModelHold.OneHand,
    float? MoveHeading = null,
    float Windup = 0f,
    float Elevation = 0f,
    float Tumble = 0f,
    float Glow = 0f,
    Color GlowColor = default,
    float MuzzleFlash = 0f,
    float MuzzleFlashSize = 0f);
