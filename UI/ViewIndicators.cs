using System;
using Fourgate.Core.Rendering;
using Fourgate.Entities;
using Fourgate.Entities.Enemies;
using Fourgate.Entities.Pickups;
using Fourgate.Systems;
using Microsoft.Xna.Framework;

namespace Fourgate.UI;

/// <summary>
/// What a player's view shows on top of the arena: pointers at its edge toward warning doors,
/// the boss and weapon crates that are off screen, the red flash of being hurt, and the skull
/// over a coming lethal boss attack. Drawn in a screen pass.
/// </summary>
public sealed class ViewIndicators
{
    private const float EdgeInset = 40f;
    private const float EdgeRadius = 18f;
    private const float BossRadius = 24f;
    private const float HurtThickness = 60f;
    private const float LethalPulseSpeed = 14f;
    private const float SkullRadius = 26f;
    private const float SkullLift = 90f;
    private const float SkullMargin = 50f;

    private static readonly Color EdgeCoreColor = new(40, 20, 10);
    private static readonly Color WeaponCrateColor = new(60, 170, 230);
    private static readonly Color BossColor = new(230, 50, 45);
    private static readonly Color HurtColor = new(220, 20, 20);
    private static readonly Color SkullColor = new(255, 60, 110);
    private static readonly Color SkullHoleColor = new(20, 0, 8);

    private readonly Arena _arena;

    public ViewIndicators(Arena arena)
    {
        _arena = arena;
    }

    /// <summary>Points toward glowing doors outside the view, so a spawn warning is never missed.</summary>
    public void DrawDoors(IRenderer renderer, Camera2D camera, WaveSystem? waves)
    {
        if (waves is null)
        {
            return;
        }

        var doors = _arena.Doors;
        for (var i = 0; i < doors.Count; i++)
        {
            var glow = waves.Spawner.GetDoorGlow(i);
            if (glow > 0f && TryEdgeMarker(camera, doors[i].Center, out var marker))
            {
                var radius = EdgeRadius * (0.6f + 0.4f * glow);
                renderer.FillCircle(marker, radius, ArenaView.GateGlowColor);
                renderer.FillCircle(marker, radius * 0.45f, EdgeCoreColor);
            }
        }
    }

    /// <summary>A red marker at the edge of the view points to a boss outside it.</summary>
    public void DrawBoss(IRenderer renderer, Camera2D camera, Enemy? boss)
    {
        if (boss is not null && TryEdgeMarker(camera, boss.Position, out var marker))
        {
            renderer.FillCircle(marker, BossRadius, BossColor);
            renderer.FillCircle(marker, BossRadius * 0.45f, EdgeCoreColor);
        }
    }

    /// <summary>Points toward weapon crates outside the view, so a new weapon is found quickly.</summary>
    public void DrawWeaponCrates(IRenderer renderer, Camera2D camera, DropSystem drops)
    {
        for (var i = 0; i < drops.Count; i++)
        {
            var crate = drops[i];
            if (crate.Type == CrateType.Weapon && TryEdgeMarker(camera, crate.Position, out var marker))
            {
                var half = new Vector2(EdgeRadius * 0.8f);
                renderer.FillRect(marker - half - new Vector2(3f), half * 2f + new Vector2(6f), EdgeCoreColor);
                renderer.FillRect(marker - half, half * 2f, WeaponCrateColor);
            }
        }
    }

    /// <summary>Red bands along the edges of the view, fading out (0–1), when its player was just hurt.</summary>
    public void DrawHurtFlash(IRenderer renderer, Rectangle view, float strength)
    {
        if (strength <= 0f)
        {
            return;
        }

        var color = HurtColor * (0.45f * strength);
        var thick = HurtThickness;
        renderer.FillRect(new Vector2(view.Left, view.Top), new Vector2(view.Width, thick), color);
        renderer.FillRect(new Vector2(view.Left, view.Bottom - thick), new Vector2(view.Width, thick), color);
        renderer.FillRect(new Vector2(view.Left, view.Top + thick), new Vector2(thick, view.Height - 2f * thick), color);
        renderer.FillRect(new Vector2(view.Right - thick, view.Top + thick), new Vector2(thick, view.Height - 2f * thick), color);
    }

    /// <summary>
    /// A lethal boss attack is coming: a throbbing skull over where it will land (kept inside the
    /// view when that spot is off screen), on top of its crimson warning markings.
    /// </summary>
    public void DrawLethalWarning(IRenderer renderer, Camera2D camera, BossSystem bosses)
    {
        if (!bosses.IsLethalWarning)
        {
            return;
        }

        var view = camera.ViewRect;
        var pulse = 0.6f + 0.4f * MathF.Sin(bosses.Time * LethalPulseSpeed);
        var at = camera.WorldToScreen(bosses.LethalPoint) - new Vector2(0f, SkullLift);
        at = new Vector2(
            Math.Clamp(at.X, view.Left + SkullMargin, view.Right - SkullMargin),
            Math.Clamp(at.Y, view.Top + SkullMargin, view.Bottom - SkullMargin));
        DrawSkull(renderer, at, SkullRadius * (0.92f + 0.08f * pulse), SkullColor * (0.75f + 0.25f * pulse));
    }

    // Where a pointer goes at the edge of the view for something outside it; false when it is in view.
    private static bool TryEdgeMarker(Camera2D camera, Vector2 world, out Vector2 marker)
    {
        var view = camera.ViewRect;
        var screen = camera.WorldToScreen(world);
        marker = new Vector2(
            Math.Clamp(screen.X, view.Left + EdgeInset, view.Right - EdgeInset),
            Math.Clamp(screen.Y, view.Top + EdgeInset, view.Bottom - EdgeInset));
        return screen.X < view.Left || screen.X > view.Right || screen.Y < view.Top || screen.Y > view.Bottom;
    }

    // A simple skull: a round head, a narrower jaw with teeth gaps, two dark eyes and a nose.
    private static void DrawSkull(IRenderer renderer, Vector2 center, float radius, Color color)
    {
        renderer.FillCircle(center, radius, color);
        var jawSize = new Vector2(radius * 1.1f, radius * 0.55f);
        var jawTopLeft = center + new Vector2(-jawSize.X / 2f, radius * 0.55f);
        renderer.FillRect(jawTopLeft, jawSize, color);
        renderer.FillCircle(center + new Vector2(-radius * 0.38f, radius * 0.05f), radius * 0.27f, SkullHoleColor);
        renderer.FillCircle(center + new Vector2(radius * 0.38f, radius * 0.05f), radius * 0.27f, SkullHoleColor);
        renderer.FillCircle(center + new Vector2(0f, radius * 0.45f), radius * 0.1f, SkullHoleColor);
        for (var i = 1; i < 4; i++)
        {
            var x = jawTopLeft.X + jawSize.X * i / 4f;
            renderer.FillRect(
                new Vector2(x - 1.5f, jawTopLeft.Y + jawSize.Y * 0.35f), new Vector2(3f, jawSize.Y * 0.65f), SkullHoleColor);
        }
    }
}
