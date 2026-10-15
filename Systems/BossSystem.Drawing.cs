using System;
using System.Collections.Generic;
using Fourgate.Core.Audio;
using Fourgate.Core.Data;
using Fourgate.Core.Rendering;
using Fourgate.Core.Rng;
using Fourgate.Entities;
using Fourgate.Entities.Enemies;
using Microsoft.Xna.Framework;

namespace Fourgate.Systems;

/// <summary>The boss's drawing: attack warnings on the floor, beams and blasts, its armour.</summary>
public sealed partial class BossSystem
{
    /// <summary>Ground markers of a pattern about to happen; drawn under the enemies.</summary>
    public void DrawMarkers(IRenderer renderer)
    {
        if (IsChangingPhase)
        {
            // A big pulsing glow while it changes phase and cannot be hurt.
            var pulse = 0.5f + 0.5f * MathF.Sin(_transitionAge * 12f);
            var middle = Depth3D.BodyHeight(Boss!.Radius) * 0.5f;
            renderer.FillCircleRaised(Boss.Position, middle, Boss.Radius * (2.2f + 0.6f * pulse), TransitionColor * (0.15f + 0.2f * pulse));
            renderer.FillCircleRaised(Boss.Position, middle, Boss.Radius * 1.5f, TransitionColor * 0.35f);
            return;
        }

        var inFlight = _state == State.Acting && _pattern?.Type is BossPatternType.LeapSlam or BossPatternType.MissileRain;
        if (Boss is null || _pattern is null || (_state != State.Telegraph && !inFlight))
        {
            return;
        }

        // A warning for a hit that would kill turns a throbbing crimson.
        _markerColor = IsLethalWarning
            ? LethalColor * (LethalBoost * (0.75f + 0.25f * MathF.Sin(_time * LethalPulseSpeed)))
            : MarkerColor;

        var progress = inFlight ? 1f : 1f - Math.Clamp(_stateLeft / _pattern.TelegraphSeconds, 0f, 1f);
        switch (_pattern.Type)
        {
            case BossPatternType.LineCharge:
                var end = _origin + _direction * _reach;
                var width = Boss.Radius * 2f;
                renderer.DrawLine(_origin, end, width, _markerColor * 0.22f);
                renderer.DrawLine(_origin, Vector2.Lerp(_origin, end, progress), width, _markerColor * 0.4f);
                DrawLaneWalls(renderer, _origin, end, width, progress);
                break;

            case BossPatternType.GroundSlamRing:
                renderer.FillCircle(_origin, _pattern.Radius, _markerColor * 0.16f);
                renderer.FillCircle(_origin, _pattern.Radius * progress, _markerColor * 0.28f);
                DrawRingWall(renderer, _origin, _pattern.Radius, progress);
                break;

            case BossPatternType.Summon:
                var size = _data.GetEnemy(_pattern.EnemyId).Radius;
                for (var i = 0; i < _summonPoints.Count; i++)
                {
                    renderer.FillCircle(_summonPoints[i], size, _markerColor * 0.2f);
                    renderer.FillCircle(_summonPoints[i], size * progress, _markerColor * 0.45f);
                    DrawRingWall(renderer, _summonPoints[i], size, progress);
                }

                break;

            case BossPatternType.FireballRing:
            case BossPatternType.FireballSpiral:
                // A swelling glow, with a dot where each fireball (or spiral arm) will leave.
                renderer.FillCircleRaised(_origin, BossMiddle, Boss.Radius * (1.2f + 0.6f * progress), _markerColor * (0.25f + 0.4f * progress));
                var spouts = _pattern.Count;
                var start = MathF.Atan2(_direction.Y, _direction.X);
                for (var i = 0; i < spouts; i++)
                {
                    var angle = start + MathHelper.TwoPi * i / spouts;
                    var dot = _origin + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * (Boss.Radius + SpoutDistance);
                    renderer.FillCircleRaised(dot, Boss.Radius * BeamHeightPerRadius, SpoutSize * (0.5f + 0.5f * progress), _markerColor);
                }

                break;

            case BossPatternType.LeapSlam:
                renderer.FillCircle(_leapTo, _pattern.Radius, _markerColor * 0.16f);
                renderer.FillCircle(_leapTo, _pattern.Radius * progress, _markerColor * 0.3f);
                DrawRingWall(renderer, _leapTo, _pattern.Radius, progress);
                break;

            case BossPatternType.MissileRain:
                for (var i = 0; i < _missileTargets.Count; i++)
                {
                    renderer.FillCircle(_missileTargets[i], _pattern.Radius, _markerColor * 0.18f);
                    renderer.FillCircle(_missileTargets[i], _pattern.Radius * progress, _markerColor * 0.32f);
                    DrawRingWall(renderer, _missileTargets[i], _pattern.Radius, progress);
                }

                break;

            case BossPatternType.FireballStream:
                // A swelling glow and a single dot where the stream will leave.
                renderer.FillCircleRaised(_origin, BossMiddle, Boss.Radius * (1.2f + 0.6f * progress), _markerColor * (0.25f + 0.4f * progress));
                renderer.FillCircleRaised(
                    _origin + _direction * (Boss.Radius + SpoutDistance), Boss.Radius * BeamHeightPerRadius,
                    SpoutSize * (0.5f + 0.5f * progress), _markerColor);
                break;

            case BossPatternType.Haste:
                // A pulsing glow: it is about to speed up.
                renderer.FillCircleRaised(_origin, BossMiddle, Boss.Radius * (1.1f + 0.5f * progress), HasteColor * (0.2f + 0.4f * progress));
                break;

            case BossPatternType.FireballVolley:
                // A swelling glow, with a dot where each fireball of the fan will leave.
                renderer.FillCircleRaised(_origin, BossMiddle, Boss.Radius * (1.2f + 0.6f * progress), _markerColor * (0.25f + 0.4f * progress));
                var aim = MathF.Atan2(_direction.Y, _direction.X);
                var spread = MathHelper.ToRadians(_pattern.SpreadDegrees);
                for (var i = 0; i < _pattern.Count; i++)
                {
                    var angle = _pattern.Count > 1 ? aim + MathHelper.Lerp(-spread, spread, i / (_pattern.Count - 1f)) : aim;
                    var dot = _origin + Direction(angle) * (Boss.Radius + SpoutDistance);
                    renderer.FillCircleRaised(dot, Boss.Radius * BeamHeightPerRadius, SpoutSize * (0.5f + 0.5f * progress), _markerColor);
                }

                break;

            case BossPatternType.LaserSweep:
                // The wedge it will sweep: both edges, and the start lane filling up.
                var sweep = MathHelper.ToRadians(_pattern.SpinDegreesPerSecond) * _pattern.Seconds * _sweepSign;
                var first = MathF.Atan2(_direction.Y, _direction.X);
                for (var i = 0; i <= 8; i++)
                {
                    var edge = _origin + Direction(first + sweep * i / 8f) * _pattern.Distance;
                    renderer.DrawLine(_origin, edge, i == 0 || i == 8 ? 6f : 2f, _markerColor * (i == 0 || i == 8 ? 0.45f : 0.18f));
                }

                var laserEnd = _origin + _direction * _streamLength;
                renderer.DrawLine(_origin, Vector2.Lerp(_origin, laserEnd, progress), _pattern.Width, _markerColor * 0.5f);
                var wallHeight = WarningWallHeight * (0.3f + 0.7f * progress);
                renderer.DrawGlowWall(_origin, _origin + Direction(first) * _pattern.Distance, wallHeight, _markerColor * 0.6f);
                renderer.DrawGlowWall(_origin, _origin + Direction(first + sweep) * _pattern.Distance, wallHeight, _markerColor * 0.6f);
                break;

            case BossPatternType.FireStream:
                // The reach it can sweep, and the lane where the fire starts.
                renderer.FillCircle(_origin, _pattern.Distance, _markerColor * 0.1f);
                var streamEnd = _origin + _direction * _streamLength;
                renderer.DrawLine(_origin, streamEnd, _pattern.Width, _markerColor * 0.25f);
                renderer.DrawLine(_origin, Vector2.Lerp(_origin, streamEnd, progress), _pattern.Width, _markerColor * 0.45f);
                DrawLaneWalls(renderer, _origin, streamEnd, _pattern.Width, progress);
                DrawRingWall(renderer, _origin, _pattern.Distance, progress * 0.5f);
                break;

            case BossPatternType.Teleport:
                renderer.FillCircleRaised(_origin, BossMiddle, Boss.Radius * (1.5f - 0.5f * progress), _markerColor * 0.2f);
                renderer.FillCircle(_teleportTo, Boss.Radius, _markerColor * 0.2f);
                renderer.FillCircle(_teleportTo, Boss.Radius * progress, _markerColor * 0.45f);
                DrawRingWall(renderer, _teleportTo, Boss.Radius, progress);
                break;
        }
    }

    // A wide soft halo round a beam and its light spilled on the floor below.
    private static void DrawBeamGlow(IRenderer renderer, Vector2 from, Vector2 to, float height, float width, Color color)
    {
        renderer.DrawLine(from, to, width * BeamFloorLightWidth, color * 0.14f);
        renderer.DrawLineRaised(from, to, height, width * BeamHaloWidth, color * 0.16f);
    }

    // The two long sides and the far end of a warning lane, rising as the attack nears.
    private void DrawLaneWalls(IRenderer renderer, Vector2 from, Vector2 to, float width, float progress)
    {
        var along = to - from;
        if (along.LengthSquared() < 0.0001f)
        {
            return;
        }

        var side = Vector2.Normalize(new Vector2(-along.Y, along.X)) * (width / 2f);
        var height = WarningWallHeight * (0.3f + 0.7f * progress);
        var color = _markerColor * 0.6f;
        renderer.DrawGlowWall(from - side, to - side, height, color);
        renderer.DrawGlowWall(from + side, to + side, height, color);
        renderer.DrawGlowWall(to - side, to + side, height, color);
    }

    // The rim of a round warning, rising as the attack nears.
    private void DrawRingWall(IRenderer renderer, Vector2 center, float radius, float progress)
    {
        var height = WarningWallHeight * (0.3f + 0.7f * progress);
        var color = _markerColor * 0.6f;
        var previous = center + new Vector2(radius, 0f);
        for (var i = 1; i <= WarningRingSegments; i++)
        {
            var angle = MathHelper.TwoPi * i / WarningRingSegments;
            var point = center + Direction(angle) * radius;
            renderer.DrawGlowWall(previous, point, height, color);
            previous = point;
        }
    }

    // Halfway up the boss's body: where its glows sit.
    private float BossMiddle => Boss is null ? 0f : Depth3D.BodyHeight(Boss.Radius) * 0.5f;

    /// <summary>The shockwave of a slam in progress; drawn over the enemies.</summary>
    public void DrawEffects(IRenderer renderer)
    {
        if (Boss is null)
        {
            return;
        }

        DrawArmor(renderer, Boss);
        if (_pattern is { Type: BossPatternType.MissileRain } && _state == State.Acting)
        {
            // Missiles dropping out of the sky onto their marks.
            var fall = Math.Clamp(_elapsed / _pattern.Seconds, 0f, 1f);
            var lift = (1f - fall) * MissileDropHeight;
            for (var i = 0; i < _missileTargets.Count; i++)
            {
                renderer.DrawShadow(_missileTargets[i], lift, MissileRadius);
                renderer.DrawParticle(_missileTargets[i], 0f, lift, MissileRadius * 1.8f, MissileGlowColor * 0.5f);
                renderer.DrawParticle(_missileTargets[i], 0f, lift, MissileRadius, MissileColor);
            }

            return;
        }

        if (_pattern is { Type: BossPatternType.LaserSweep } && _state == State.Acting)
        {
            var end = Boss.Position + _direction * _streamLength;
            var laserHeight = Boss.Radius * BeamHeightPerRadius;
            DrawBeamGlow(renderer, Boss.Position, end, laserHeight, _pattern.Width, LaserColor);
            renderer.DrawLineRaised(Boss.Position, end, laserHeight, _pattern.Width * 2.5f, LaserColor * 0.35f);
            renderer.DrawLineRaised(Boss.Position, end, laserHeight, _pattern.Width, LaserColor);
            renderer.DrawLineRaised(Boss.Position, end, laserHeight, _pattern.Width * 0.35f, LaserCoreColor);
            return;
        }

        if (_pattern is { Type: BossPatternType.FireStream } && _state == State.Acting)
        {
            var end = Boss.Position + _direction * _streamLength;
            var fireHeight = Boss.Radius * BeamHeightPerRadius;
            DrawBeamGlow(renderer, Boss.Position, end, fireHeight, _pattern.Width, FireColor);
            renderer.DrawLineRaised(Boss.Position, end, fireHeight, _pattern.Width, FireColor * 0.85f);
            renderer.DrawLineRaised(Boss.Position, end, fireHeight, _pattern.Width * 0.4f, FireCoreColor);
            return;
        }

        if (_pattern is not { Type: BossPatternType.GroundSlamRing } || _state != State.Acting)
        {
            return;
        }

        var step = MathHelper.TwoPi / RingSegments;
        var previous = _origin + new Vector2(_ringRadius, 0f);
        for (var i = 1; i <= RingSegments; i++)
        {
            var angle = step * i;
            var point = _origin + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * _ringRadius;
            renderer.DrawBar(previous, point, _pattern.RingWidth, 0f, ShockwaveHeight, ShockwaveColor);
            previous = point;
        }
    }

    // A plate across the armored front, so players can see which side to avoid. Its inner edge
    // overlaps the body, where blocked shots stop.
    private void DrawArmor(IRenderer renderer, Enemy boss)
    {
        if (!boss.HasShieldBand)
        {
            return;
        }

        // A curved band along exactly the armored arc, where shots are stopped.
        var halfArc = MathHelper.ToRadians(boss.Config.FrontArmorArcDegrees / 2f);
        var radius = (boss.ShieldInnerRadius + boss.ShieldOuterRadius) / 2f;
        var thickness = boss.Config.FrontArmorThickness;
        var previous = boss.Position + Direction(boss.Facing - halfArc) * radius;
        for (var i = 1; i <= ArmorPlateSegments; i++)
        {
            var angle = boss.Facing - halfArc + 2f * halfArc * i / ArmorPlateSegments;
            var point = boss.Position + Direction(angle) * radius;
            renderer.DrawBar(previous, point, thickness, 0f, boss.Radius * ShieldHeightPerRadius, ArmorColor);
            previous = point;
        }
    }
}
