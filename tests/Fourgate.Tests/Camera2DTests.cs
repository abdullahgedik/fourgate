using Fourgate.Core.Data;
using Fourgate.Core.Rendering;
using Microsoft.Xna.Framework;

namespace Fourgate.Tests;

public class Camera2DTests
{
    private static readonly Rectangle FullScreen = new(0, 0, 1920, 1080);
    private static readonly Vector2 ScreenCenter = new(960, 540);

    [Fact]
    public void ViewCenter_ShowsTheCameraPosition()
    {
        var camera = CreateCamera();
        camera.SnapTo(new Vector2(700, 300), ScreenCenter);

        AssertClose(new Vector2(700, 300), camera.ScreenToWorld(ScreenCenter));
        AssertClose(ScreenCenter, camera.WorldToScreen(new Vector2(700, 300)));
    }

    [Fact]
    public void ScreenToWorld_AppliesZoom()
    {
        var camera = CreateCamera(zoom: 2f);
        camera.SnapTo(new Vector2(1000, 1000), ScreenCenter);

        AssertClose(new Vector2(1050, 975), camera.ScreenToWorld(ScreenCenter + new Vector2(100, -50)));
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(1919, 1079)]
    [InlineData(433, 871)]
    public void WorldToScreen_RoundTrips(float x, float y)
    {
        var camera = CreateCamera(zoom: 1.15f);
        camera.SnapTo(new Vector2(640, 1210), ScreenCenter);
        var screen = new Vector2(x, y);

        AssertClose(screen, camera.WorldToScreen(camera.ScreenToWorld(screen)));
    }

    [Fact]
    public void SplitScreenView_ConvertsRelativeToItsOwnCenter()
    {
        var camera = new Camera2D(new Rectangle(960, 0, 960, 1080), Config());
        camera.SnapTo(new Vector2(500, 500), camera.ViewCenter);

        AssertClose(new Vector2(500, 500), camera.ScreenToWorld(new Vector2(1440, 540)));
    }

    [Fact]
    public void Transform_MatchesWorldToScreen()
    {
        var camera = CreateCamera(zoom: 1.15f);
        camera.SnapTo(new Vector2(640, 1210), ScreenCenter);
        var world = new Vector2(812, 1004);

        AssertClose(camera.WorldToScreen(world), Vector2.Transform(world, camera.GetTransform()));
    }

    [Fact]
    public void AimLead_ShiftsTowardTheAimPointByTheLeadFraction()
    {
        var camera = CreateCamera(leadFraction: 0.5f, leadMax: 1000f);

        camera.SnapTo(new Vector2(1000, 1000), ScreenCenter + new Vector2(200, 0));

        AssertClose(new Vector2(1100, 1000), camera.Position);
    }

    [Fact]
    public void AimLead_IsCappedAtTheMaxDistance()
    {
        var camera = CreateCamera(leadFraction: 1f, leadMax: 50f);

        camera.SnapTo(new Vector2(1000, 1000), ScreenCenter + new Vector2(0, 400));

        AssertClose(new Vector2(1000, 1050), camera.Position);
    }

    [Fact]
    public void Bounds_KeepTheViewInsideTheArenaPlusMargin()
    {
        var camera = CreateCamera(edgeMargin: 100f);
        camera.SetBounds(Vector2.Zero, new Vector2(2000, 2000));

        camera.SnapTo(Vector2.Zero, ScreenCenter);

        // Half the view is 960 x 540, so the camera stops that far from the margin edge.
        AssertClose(new Vector2(860, 440), camera.Position);
    }

    [Fact]
    public void Bounds_SmallerThanTheView_AreCentered()
    {
        var camera = CreateCamera();
        camera.SetBounds(Vector2.Zero, new Vector2(1000, 1000));

        camera.SnapTo(new Vector2(100, 900), ScreenCenter);

        AssertClose(new Vector2(500, 500), camera.Position);
    }

    [Fact]
    public void Follow_MovesPartWayAndConvergesOnTheTarget()
    {
        var camera = CreateCamera(smoothing: 10f);
        camera.SnapTo(new Vector2(1000, 1000), ScreenCenter);
        var focus = new Vector2(1200, 1000);

        camera.Follow(focus, ScreenCenter, 1f / 60f);
        Assert.InRange(camera.Position.X, 1000.1f, 1199.9f);

        for (var i = 0; i < 300; i++)
        {
            camera.Follow(focus, ScreenCenter, 1f / 60f);
        }

        AssertClose(focus, camera.Position);
    }

    [Fact]
    public void Follow_WithoutSmoothing_Snaps()
    {
        var camera = CreateCamera(smoothing: 0f);

        camera.Follow(new Vector2(1200, 1000), ScreenCenter, 1f / 60f);

        AssertClose(new Vector2(1200, 1000), camera.Position);
    }

    private static Camera2D CreateCamera(
        float zoom = 1f, float leadFraction = 0f, float leadMax = 0f, float smoothing = 0f, float edgeMargin = 0f)
    {
        return new Camera2D(FullScreen, Config(zoom, leadFraction, leadMax, smoothing, edgeMargin));
    }

    private static CameraConfig Config(
        float zoom = 1f, float leadFraction = 0f, float leadMax = 0f, float smoothing = 0f, float edgeMargin = 0f)
    {
        return new CameraConfig
        {
            Zoom = zoom,
            LeadFraction = leadFraction,
            LeadMaxDistance = leadMax,
            Smoothing = smoothing,
            EdgeMargin = edgeMargin,
        };
    }

    private static void AssertClose(Vector2 expected, Vector2 actual)
    {
        Assert.Equal(expected.X, actual.X, 1);
        Assert.Equal(expected.Y, actual.Y, 1);
    }
}
