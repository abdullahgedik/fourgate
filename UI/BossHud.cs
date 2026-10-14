using Fourgate.Core.Rendering;
using Fourgate.Entities.Enemies;
using Microsoft.Xna.Framework;

namespace Fourgate.UI;

/// <summary>The boss's name and a large HP bar across the top of the screen.</summary>
public sealed class BossHud
{
    private const float Top = 118f;
    private const float NameSize = 30f;
    private const float BarWidth = 800f;
    private const float BarHeight = 18f;
    private const float BarGap = 40f;
    private const float Border = 3f;

    private static readonly Color NameColor = new(255, 200, 180);
    private static readonly Color BackColor = new(20, 10, 12);
    private static readonly Color FillColor = new(210, 50, 45);
    private static readonly Color InvulnerableColor = new(150, 150, 160);
    private static readonly Color BorderColor = new(90, 30, 30);

    public void Draw(IRenderer renderer, string name, Enemy boss, int screenWidth)
    {
        var center = screenWidth / 2f;
        renderer.DrawText(name, new Vector2(center, Top), NameSize, NameColor, TextAlign.Center);

        var topLeft = new Vector2(center - BarWidth / 2f, Top + BarGap);
        renderer.FillRect(topLeft - new Vector2(Border), new Vector2(BarWidth, BarHeight) + new Vector2(Border * 2f), BorderColor);
        renderer.FillRect(topLeft, new Vector2(BarWidth, BarHeight), BackColor);

        var share = boss.MaxHp > 0f ? MathHelper.Clamp(boss.Hp / boss.MaxHp, 0f, 1f) : 0f;
        renderer.FillRect(
            topLeft, new Vector2(BarWidth * share, BarHeight), boss.IsInvulnerable ? InvulnerableColor : FillColor);
    }
}
