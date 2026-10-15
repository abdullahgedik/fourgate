using Fourgate.Core.Audio;
using Fourgate.Core.Rendering;
using Microsoft.Xna.Framework;

namespace Fourgate.UI;

/// <summary>Debug: the latest sound events, newest at the bottom-left, so hooks can be checked without audio.</summary>
public sealed class AudioEventOverlay
{
    private const float TextSize = 22f;
    private const float LineHeight = 24f;
    private const float Left = 20f;
    private const float BottomMargin = 120f;

    private static readonly Color HeaderColor = new(120, 220, 255);
    private static readonly Color TextColor = new(200, 235, 255);

    public void Draw(IRenderer renderer, AudioEventLog log, int screenHeight)
    {
        renderer.BeginScreen();
        var y = screenHeight - BottomMargin;
        var shade = 1f;
        foreach (var id in log.Recent)
        {
            renderer.DrawText(id, new Vector2(Left, y), TextSize, TextColor * shade);
            y -= LineHeight;
            shade *= 0.85f;
        }

        renderer.DrawText("SOUND EVENTS", new Vector2(Left, y - 6f), TextSize, HeaderColor);
        renderer.End();
    }
}
