using Fourgate.Core.Audio;
using Fourgate.Core.Data;
using Fourgate.Entities;
using Microsoft.Xna.Framework;

namespace Fourgate.Tests;

public class PlayerTests
{
    private const float Step = 1f / 60f;

    private static readonly Arena TestArena = new(new ArenaConfig
    {
        X = 0, Y = 0, Size = 3000, DoorWidth = 420, WallThickness = 24,
    });

    private static readonly Vector2 Start = new(1500, 1500);

    [Fact]
    public void Moves_AtMoveSpeed()
    {
        var input = new FakeInput { Move = Vector2.UnitX };
        var player = CreatePlayer(input);

        Run(player, 60);

        Assert.Equal(1800f, player.Position.X, 0);
        Assert.Equal(1500f, player.Position.Y, 0);
    }

    [Fact]
    public void OversizedMoveVector_IsNotFasterThanMoveSpeed()
    {
        var input = new FakeInput { Move = new Vector2(1, 1) };
        var player = CreatePlayer(input);

        Run(player, 60);

        Assert.Equal(300f, Vector2.Distance(Start, player.Position), 0);
    }

    [Fact]
    public void Walls_StopThePlayerAtItsRadius()
    {
        var input = new FakeInput { Move = -Vector2.UnitX };
        var player = CreatePlayer(input);

        Run(player, 600);

        Assert.Equal(20f, player.Position.X, 3);
    }

    [Fact]
    public void FacesTheAimDirection()
    {
        var input = new FakeInput { AimDirection = Vector2.UnitY };
        var player = CreatePlayer(input);

        player.Update(Step, TestArena);

        Assert.Equal(Vector2.UnitY, player.AimDirection);
    }

    [Fact]
    public void Dash_CoversItsDistanceInTheMoveDirection()
    {
        var input = new FakeInput { Move = Vector2.UnitY, DashPressed = true };
        var player = CreatePlayer(input);

        player.Update(Step, TestArena);
        input.DashPressed = false;
        input.Move = Vector2.Zero;
        Run(player, 30);

        Assert.False(player.IsDashing);
        Assert.Equal(1700f, player.Position.Y, 0);
    }

    [Fact]
    public void Dash_WhileStandingStill_GoesTowardTheAim()
    {
        var input = new FakeInput { AimDirection = -Vector2.UnitX, DashPressed = true };
        var player = CreatePlayer(input);

        player.Update(Step, TestArena);
        input.DashPressed = false;
        Run(player, 30);

        Assert.Equal(1300f, player.Position.X, 0);
    }

    [Fact]
    public void Dash_IsInvulnerableOnlyWhileDashing()
    {
        var input = new FakeInput { Move = Vector2.UnitX, DashPressed = true };
        var player = CreatePlayer(input);
        Assert.False(player.IsInvulnerable);

        player.Update(Step, TestArena);
        Assert.True(player.IsInvulnerable);

        input.DashPressed = false;
        Run(player, 30);
        Assert.False(player.IsInvulnerable);
    }

    [Fact]
    public void Dash_WithoutIFrames_IsNotInvulnerable()
    {
        var input = new FakeInput { Move = Vector2.UnitX, DashPressed = true };
        var player = CreatePlayer(input, iFrames: false);

        player.Update(Step, TestArena);

        Assert.True(player.IsDashing);
        Assert.False(player.IsInvulnerable);
    }

    [Fact]
    public void Dash_CannotBeRepeatedDuringCooldown()
    {
        var input = new FakeInput { DashPressed = true };
        var player = CreatePlayer(input);

        // Dash held down for one second: only the first press may trigger within the 2 s cooldown.
        Run(player, 60);

        Assert.Equal(1700f, player.Position.X, 0);
        Assert.True(player.DashCooldownLeft > 0f);
    }

    [Fact]
    public void Dash_IsAvailableAgainAfterCooldown()
    {
        var input = new FakeInput { DashPressed = true };
        var player = CreatePlayer(input);

        player.Update(Step, TestArena);
        input.DashPressed = false;
        Run(player, 125);
        Assert.Equal(0f, player.DashCooldownLeft);

        input.DashPressed = true;
        player.Update(Step, TestArena);

        Assert.True(player.IsDashing);
    }

    private static Player CreatePlayer(FakeInput input, bool iFrames = true)
    {
        var config = new PlayerConfig
        {
            MoveSpeed = 300,
            Radius = 20,
            Dash = new DashConfig { Cooldown = 2f, Duration = 0.18f, Distance = 200, IFrames = iFrames },
        };

        return new Player(0, input, config, new NullAudioManager(), Start);
    }

    private static void Run(Player player, int steps)
    {
        for (var i = 0; i < steps; i++)
        {
            player.Update(Step, TestArena);
        }
    }
}
