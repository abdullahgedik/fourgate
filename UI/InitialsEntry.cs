using Fourgate.Core.Input;
using Fourgate.Core.Rendering;
using Microsoft.Xna.Framework;

namespace Fourgate.UI;

/// <summary>
/// Three letters chosen arcade-style: up/down changes the letter, left/right moves between them,
/// confirm saves. Works the same on keyboard and gamepad.
/// </summary>
public sealed class InitialsEntry
{
    private const int Length = 3;
    private const float LetterSize = 90f;
    private const float BoxSize = 110f;
    private const float BoxGap = 30f;
    private const float Border = 4f;

    private static readonly Color BoxColor = new(30, 33, 44);
    private static readonly Color BorderColor = new(70, 76, 98);
    private static readonly Color SelectedColor = new(255, 225, 120);
    private static readonly Color LetterColor = new(240, 240, 245);

    private readonly char[] _letters = { 'A', 'A', 'A' };
    private readonly string[] _letterTexts = { "A", "A", "A" };
    private int _slot;

    /// <summary>True once confirmed.</summary>
    public bool IsDone { get; private set; }

    public string Initials => new(_letters);

    public void Update(IPlayerInput input)
    {
        if (IsDone)
        {
            return;
        }

        if (input.MenuUpPressed)
        {
            Change(1);
        }

        if (input.MenuDownPressed)
        {
            Change(-1);
        }

        if (input.MenuLeftPressed)
        {
            _slot = (_slot + Length - 1) % Length;
        }

        if (input.MenuRightPressed)
        {
            _slot = (_slot + 1) % Length;
        }

        if (input.ConfirmPressed)
        {
            IsDone = true;
        }
    }

    /// <param name="renderer">Renderer in a screen-space pass.</param>
    /// <param name="topCenter">Where the middle of the top edge of the boxes goes.</param>
    public void Draw(IRenderer renderer, Vector2 topCenter)
    {
        var totalWidth = Length * BoxSize + (Length - 1) * BoxGap;
        for (var i = 0; i < Length; i++)
        {
            var topLeft = new Vector2(topCenter.X - totalWidth / 2f + i * (BoxSize + BoxGap), topCenter.Y);
            var size = new Vector2(BoxSize);
            renderer.FillRect(topLeft, size, i == _slot ? SelectedColor : BorderColor);
            renderer.FillRect(topLeft + new Vector2(Border), size - new Vector2(Border * 2f), BoxColor);
            renderer.DrawText(
                _letterTexts[i], new Vector2(topLeft.X + BoxSize / 2f, topLeft.Y + 4f), LetterSize, LetterColor,
                TextAlign.Center);
        }
    }

    private void Change(int step)
    {
        var letter = _letters[_slot] - 'A';
        letter = (letter + step + 26) % 26;
        _letters[_slot] = (char)('A' + letter);
        _letterTexts[_slot] = _letters[_slot].ToString();
    }
}
