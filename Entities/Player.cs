using System;
using Fourgate.Core.Audio;
using Fourgate.Core.Data;
using Fourgate.Core.Input;
using Fourgate.Core.Rendering;
using Microsoft.Xna.Framework;

namespace Fourgate.Entities;

/// <summary>A player character. Owns its input source and all per-player state.</summary>
public sealed class Player
{
    private static readonly Color[] BodyColors = { new(90, 170, 255), new(255, 170, 70) };
    private static readonly Color DashColor = new(235, 245, 255);

    private readonly PlayerConfig _config;
    private readonly IAudioManager _audio;
    private Vector2 _dashDirection;
    private float _dashTimeLeft;
    private float _dashCooldownLeft;

    public Player(int index, IPlayerInput input, PlayerConfig config, IAudioManager audio, Vector2 position)
    {
        Index = index;
        Input = input;
        _config = config;
        _audio = audio;
        Position = position;
    }

    /// <summary>Zero-based player number.</summary>
    public int Index { get; }

    public IPlayerInput Input { get; }

    public Vector2 Position { get; private set; }

    /// <summary>Unit-length facing direction in world space.</summary>
    public Vector2 AimDirection { get; private set; } = Vector2.UnitX;

    public float Radius => _config.Radius;

    public bool IsDashing => _dashTimeLeft > 0f;

    /// <summary>True while nothing can damage the player (dash i-frames).</summary>
    public bool IsInvulnerable => IsDashing && _config.Dash.IFrames;

    /// <summary>Seconds until the next dash is available.</summary>
    public float DashCooldownLeft => _dashCooldownLeft;

    public VisualDescriptor Visual
    {
        get
        {
            var body = BodyColors[Index % BodyColors.Length];
            if (IsDashing)
            {
                body = DashColor;
            }
            else if (_dashCooldownLeft > 0f)
            {
                // Dimmed while the dash recharges.
                body = Color.Lerp(body, Color.Black, 0.35f);
            }

            return new VisualDescriptor(ShapeKind.Circle, Radius, body);
        }
    }

    /// <summary>Advances the player by one simulation step. The input must already be polled.</summary>
    public void Update(float deltaSeconds, Arena arena)
    {
        AimDirection = Input.AimDirection;

        var move = Input.Move;
        if (move.LengthSquared() > 1f)
        {
            move.Normalize();
        }

        _dashCooldownLeft = Math.Max(0f, _dashCooldownLeft - deltaSeconds);
        if (Input.DashPressed && !IsDashing && _dashCooldownLeft <= 0f)
        {
            StartDash(move);
        }

        var walkSeconds = deltaSeconds;
        if (IsDashing)
        {
            var dash = _config.Dash;
            var dashSeconds = Math.Min(deltaSeconds, _dashTimeLeft);
            Position += _dashDirection * (dash.Distance / dash.Duration) * dashSeconds;

            _dashTimeLeft -= dashSeconds;
            if (_dashTimeLeft < 0.0001f)
            {
                _dashTimeLeft = 0f;
            }

            walkSeconds -= dashSeconds;
        }

        Position += move * _config.MoveSpeed * walkSeconds;
        Position = arena.ClampInside(Position, Radius);
    }

    private void StartDash(Vector2 move)
    {
        // Dash where the player is heading; standing still dashes toward the aim.
        _dashDirection = move != Vector2.Zero ? Vector2.Normalize(move) : AimDirection;
        _dashTimeLeft = _config.Dash.Duration;
        _dashCooldownLeft = _config.Dash.Cooldown;
        _audio.PlaySfx(SfxIds.PlayerDash);
    }
}
