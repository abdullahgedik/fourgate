using Fourgate.Core.Rendering;
using Microsoft.Xna.Framework;

namespace Fourgate.Tests;

public class VirtualViewportTests
{
    private const int VirtualWidth = 1920;
    private const int VirtualHeight = 1080;

    [Theory]
    [InlineData(1280, 720)]
    [InlineData(1920, 1080)]
    [InlineData(2560, 1440)]
    public void SameAspect_FillsTheWindow(int windowWidth, int windowHeight)
    {
        var viewport = VirtualViewport.Fit(VirtualWidth, VirtualHeight, windowWidth, windowHeight);

        Assert.Equal(new Rectangle(0, 0, windowWidth, windowHeight), viewport.Destination);
    }

    [Fact]
    public void WiderWindow_GetsBarsLeftAndRight()
    {
        var viewport = VirtualViewport.Fit(VirtualWidth, VirtualHeight, 2560, 1080);

        Assert.Equal(new Rectangle(320, 0, 1920, 1080), viewport.Destination);
        Assert.Equal(1f, viewport.Scale, 4);
    }

    [Fact]
    public void TallerWindow_GetsBarsTopAndBottom()
    {
        var viewport = VirtualViewport.Fit(VirtualWidth, VirtualHeight, 1280, 1024);

        Assert.Equal(new Rectangle(0, 152, 1280, 720), viewport.Destination);
    }

    [Fact]
    public void WindowToVirtual_MapsWindowCenterToVirtualCenter()
    {
        var viewport = VirtualViewport.Fit(VirtualWidth, VirtualHeight, 1280, 720);

        var result = viewport.WindowToVirtual(new Vector2(640, 360));

        AssertClose(new Vector2(960, 540), result);
    }

    [Fact]
    public void WindowToVirtual_SubtractsLetterboxOffset()
    {
        var viewport = VirtualViewport.Fit(VirtualWidth, VirtualHeight, 2560, 1080);

        AssertClose(Vector2.Zero, viewport.WindowToVirtual(new Vector2(320, 0)));
        AssertClose(new Vector2(1920, 1080), viewport.WindowToVirtual(new Vector2(2240, 1080)));
    }

    [Fact]
    public void WindowToVirtual_OverTheBars_FallsOutsideTheVirtualScreen()
    {
        var viewport = VirtualViewport.Fit(VirtualWidth, VirtualHeight, 1280, 1024);

        var result = viewport.WindowToVirtual(new Vector2(0, 0));

        Assert.True(result.Y < 0);
    }

    [Theory]
    [InlineData(1280, 720)]
    [InlineData(1280, 1024)]
    [InlineData(2560, 1080)]
    [InlineData(1000, 700)]
    public void VirtualToWindow_RoundTrips(int windowWidth, int windowHeight)
    {
        var viewport = VirtualViewport.Fit(VirtualWidth, VirtualHeight, windowWidth, windowHeight);
        var point = new Vector2(1460, 40);

        AssertClose(point, viewport.WindowToVirtual(viewport.VirtualToWindow(point)));
    }

    [Fact]
    public void MinimizedWindow_DoesNotDivideByZero()
    {
        var viewport = VirtualViewport.Fit(VirtualWidth, VirtualHeight, 0, 0);

        Assert.Equal(Vector2.Zero, viewport.WindowToVirtual(new Vector2(10, 10)));
    }

    private static void AssertClose(Vector2 expected, Vector2 actual)
    {
        Assert.Equal(expected.X, actual.X, 2);
        Assert.Equal(expected.Y, actual.Y, 2);
    }
}
