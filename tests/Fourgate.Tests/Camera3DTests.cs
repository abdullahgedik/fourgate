using Fourgate.Core.Rendering;
using Microsoft.Xna.Framework;

namespace Fourgate.Tests;

public class Camera3DTests
{
    [Theory]
    [InlineData(960f, 540f)]
    [InlineData(100f, 100f)]
    [InlineData(1800f, 1000f)]
    [InlineData(400f, 900f)]
    public void ScreenToWorld_AndBack_LandsOnTheSamePixel(float x, float y)
    {
        var camera = Camera(new Rectangle(0, 0, 1920, 1080));

        var world = camera.ScreenToWorld(new Vector2(x, y));
        var back = camera.WorldToScreen(world);

        Assert.InRange(back.X, x - 0.5f, x + 0.5f);
        Assert.InRange(back.Y, y - 0.5f, y + 0.5f);
    }

    [Fact]
    public void ViewCenter_LooksAtTheCameraPosition_AndScreenUpIsWorldUp()
    {
        var camera = Camera(new Rectangle(0, 0, 1920, 1080));

        var center = camera.ScreenToWorld(camera.ViewCenter);
        var higher = camera.ScreenToWorld(camera.ViewCenter - new Vector2(0f, 200f));
        var right = camera.ScreenToWorld(camera.ViewCenter + new Vector2(200f, 0f));

        Assert.InRange(Vector2.Distance(center, camera.Position), 0f, 0.5f);
        Assert.True(higher.Y < center.Y);
        Assert.True(right.X > center.X);
    }

    [Fact]
    public void SplitScreenHalf_ConvertsWithinItsOwnHalf()
    {
        var camera = Camera(new Rectangle(960, 0, 960, 1080));

        var world = camera.ScreenToWorld(new Vector2(1500, 300));

        Assert.InRange(camera.WorldToScreen(world).X, 1499.5f, 1500.5f);
        Assert.InRange(Vector2.Distance(camera.ScreenToWorld(camera.ViewCenter), camera.Position), 0f, 0.5f);
    }

    private static Camera2D Camera(Rectangle view)
    {
        var camera = new Camera2D(view, TestData.Game().Camera) { Is3D = true };
        camera.SnapTo(new Vector2(1500, 1500), camera.ViewCenter);
        return camera;
    }
}
