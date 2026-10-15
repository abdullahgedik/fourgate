using System.Collections.Generic;
using Fourgate.Core.Rendering;
using Microsoft.Xna.Framework;

namespace Fourgate.UI;

/// <summary>A column of wide buttons, one of them highlighted; used by the menus.</summary>
public sealed class MenuList
{
    private const float TextSize = 40f;
    private const float Border = 4f;

    private static readonly Color ButtonColor = new(30, 33, 44);
    private static readonly Color ButtonSelectedColor = new(46, 52, 72);
    private static readonly Color BorderColor = new(70, 76, 98);
    private static readonly Color BorderSelectedColor = new(255, 225, 120);
    private static readonly Color TextColor = new(225, 228, 238);
    private static readonly Color DisabledTextColor = new(110, 114, 130);

    public MenuList(Vector2 topCenter, float width, float height, float gap)
    {
        TopCenter = topCenter;
        Width = width;
        Height = height;
        Gap = gap;
    }

    public Vector2 TopCenter { get; }

    public float Width { get; }

    public float Height { get; }

    public float Gap { get; }

    /// <summary>The button under a screen point, or -1.</summary>
    public int ButtonAt(Vector2 screen, int count)
    {
        for (var i = 0; i < count; i++)
        {
            var topLeft = TopLeft(i);
            if (screen.X >= topLeft.X && screen.X <= topLeft.X + Width
                && screen.Y >= topLeft.Y && screen.Y <= topLeft.Y + Height)
            {
                return i;
            }
        }

        return -1;
    }

    /// <param name="renderer">Renderer in a screen-space pass.</param>
    /// <param name="labels">One label per button.</param>
    /// <param name="selected">Highlighted button.</param>
    /// <param name="disabled">Buttons shown greyed out; may be null.</param>
    public void Draw(IRenderer renderer, IReadOnlyList<string> labels, int selected, IReadOnlyList<bool>? disabled = null)
    {
        for (var i = 0; i < labels.Count; i++)
        {
            var topLeft = TopLeft(i);
            var size = new Vector2(Width, Height);
            var isSelected = i == selected;
            renderer.FillRect(topLeft, size, isSelected ? BorderSelectedColor : BorderColor);
            renderer.FillRect(
                topLeft + new Vector2(Border), size - new Vector2(Border * 2f), isSelected ? ButtonSelectedColor : ButtonColor);
            var greyed = disabled is not null && disabled[i];
            renderer.DrawTextCentered(
                labels[i], topLeft + size / 2f, TextSize, greyed ? DisabledTextColor : TextColor);
        }
    }

    private Vector2 TopLeft(int index)
    {
        return new Vector2(TopCenter.X - Width / 2f, TopCenter.Y + index * (Height + Gap));
    }
}
