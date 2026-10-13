using System;
using System.Collections.Generic;
using Fourgate.Core.Rendering;
using Fourgate.Entities;
using Fourgate.Systems;
using Microsoft.Xna.Framework;

namespace Fourgate.UI;

/// <summary>
/// The upgrade card screen. Each player with an open offer sees their cards inside their
/// own view and chooses with their own controls.
/// </summary>
public sealed class UpgradeScreen
{
    // Ignore input briefly after cards appear so a held fire button does not pick one by accident.
    private const float InputLockSeconds = 0.35f;

    private const float CardMaxWidth = 380f;
    private const float CardHeight = 300f;
    private const float CardGap = 28f;
    private const float SideMargin = 48f;
    private const float CardBorder = 5f;
    private const float SelectedLift = 14f;
    private const float HeaderSize = 56f;
    private const float SubHeaderSize = 30f;
    private const float TitleSize = 38f;
    private const float DescriptionSize = 27f;
    private const float DescriptionLineHeight = 34f;
    private const float DetailSize = 24f;
    private const float HintSize = 26f;

    private static readonly Color DimColor = new(0, 0, 0, 170);
    private static readonly Color CardColor = new(30, 33, 44);
    private static readonly Color CardSelectedColor = new(46, 52, 72);
    private static readonly Color BorderColor = new(70, 76, 98);
    private static readonly Color BorderSelectedColor = new(255, 225, 120);
    private static readonly Color HeaderColor = new(255, 225, 120);
    private static readonly Color TitleColor = new(240, 240, 245);
    private static readonly Color TextColor = new(200, 205, 220);
    private static readonly Color DetailColor = new(170, 120, 255);
    private static readonly Color HintColor = new(150, 155, 170);

    private readonly UpgradeSystem _upgrades;
    private readonly List<SlotState> _slots = new();

    public UpgradeScreen(UpgradeSystem upgrades)
    {
        _upgrades = upgrades;
    }

    /// <summary>Polls every player's controls and applies their choices.</summary>
    public void Update(float deltaSeconds, IReadOnlyList<Player> players, IReadOnlyList<Camera2D> cameras)
    {
        while (_slots.Count < players.Count)
        {
            _slots.Add(new SlotState());
        }

        for (var i = 0; i < players.Count; i++)
        {
            var player = players[i];
            var input = player.Input;
            input.Update(player.Position, cameras[i]);

            var offer = _upgrades.GetOffer(i);
            var slot = _slots[i];
            if (offer.Count == 0)
            {
                slot.Reset();
                continue;
            }

            slot.OpenSeconds += deltaSeconds;
            slot.Selected = Math.Clamp(slot.Selected, 0, offer.Count - 1);
            if (slot.OpenSeconds < InputLockSeconds)
            {
                continue;
            }

            if (!slot.Armed)
            {
                // Wait for the fire button to be let go before accepting clicks.
                slot.Armed = !input.FireHeld;
                continue;
            }

            if (input.MenuLeftPressed)
            {
                slot.Selected = Math.Max(0, slot.Selected - 1);
            }

            if (input.MenuRightPressed)
            {
                slot.Selected = Math.Min(offer.Count - 1, slot.Selected + 1);
            }

            var clicked = false;
            if (input.HasPointer)
            {
                var hovered = GetCardAt(cameras[i].ViewRect, offer.Count, input.AimScreen);
                if (hovered >= 0)
                {
                    slot.Selected = hovered;
                    clicked = input.FirePressed;
                }
            }

            if (clicked || input.ConfirmPressed)
            {
                _upgrades.Choose(player, i, slot.Selected);
                slot.Reset();
            }
        }
    }

    /// <summary>Draws the cards of every player with an open offer; the renderer must be in a screen-space pass.</summary>
    public void Draw(IRenderer renderer, IReadOnlyList<Player> players, IReadOnlyList<Camera2D> cameras)
    {
        for (var i = 0; i < players.Count; i++)
        {
            var view = cameras[i].ViewRect;
            renderer.FillRect(new Vector2(view.X, view.Y), new Vector2(view.Width, view.Height), DimColor);

            var offer = _upgrades.GetOffer(i);
            var centerX = view.X + view.Width / 2f;
            if (offer.Count == 0)
            {
                renderer.DrawText(
                    "READY", new Vector2(centerX, view.Y + view.Height / 2f - HeaderSize / 2f), HeaderSize, HintColor,
                    TextAlign.Center);
                continue;
            }

            var slot = i < _slots.Count ? _slots[i] : null;
            var selected = slot is null ? 0 : Math.Clamp(slot.Selected, 0, offer.Count - 1);
            DrawOffer(renderer, players[i], offer, view, selected);
        }
    }

    private static void DrawOffer(
        IRenderer renderer, Player player, IReadOnlyList<UpgradeCard> offer, Rectangle view, int selected)
    {
        var centerX = view.X + view.Width / 2f;
        var cardsTop = GetCardRect(view, offer.Count, 0).Y;

        renderer.DrawText(
            "CHOOSE AN UPGRADE", new Vector2(centerX, cardsTop - 150f), HeaderSize, HeaderColor, TextAlign.Center);

        var picks = player.Progression.BankedPicks;
        if (picks > 1)
        {
            renderer.DrawText(
                picks == 2 ? "1 MORE PICK AFTER THIS" : "MORE PICKS AFTER THIS",
                new Vector2(centerX, cardsTop - 82f), SubHeaderSize, TextColor, TextAlign.Center);
        }

        for (var i = 0; i < offer.Count; i++)
        {
            var rect = GetCardRect(view, offer.Count, i);
            var isSelected = i == selected;
            var topLeft = new Vector2(rect.X, rect.Y - (isSelected ? SelectedLift : 0f));
            var size = new Vector2(rect.Width, rect.Height);
            var border = new Vector2(CardBorder);

            renderer.FillRect(topLeft - border, size + border * 2f, isSelected ? BorderSelectedColor : BorderColor);
            renderer.FillRect(topLeft, size, isSelected ? CardSelectedColor : CardColor);

            var cardCenterX = topLeft.X + size.X / 2f;
            var card = offer[i];
            renderer.DrawText(
                card.Title, new Vector2(cardCenterX, topLeft.Y + 46f), TitleSize, TitleColor, TextAlign.Center);
            for (var line = 0; line < card.Lines.Count; line++)
            {
                renderer.DrawText(
                    card.Lines[line],
                    new Vector2(cardCenterX, topLeft.Y + 112f + line * DescriptionLineHeight),
                    DescriptionSize,
                    TextColor,
                    TextAlign.Center);
            }
            renderer.DrawText(
                card.Detail, new Vector2(cardCenterX, topLeft.Y + size.Y - 60f), DetailSize, DetailColor,
                TextAlign.Center);
        }

        var hint = player.Input.HasPointer
            ? "CLICK A CARD,  OR  A / D  THEN  ENTER"
            : "LEFT / RIGHT  THEN  A";
        renderer.DrawText(
            hint, new Vector2(centerX, cardsTop + CardHeight + 44f), HintSize, HintColor, TextAlign.Center);
    }

    private static int GetCardAt(Rectangle view, int count, Vector2 point)
    {
        for (var i = 0; i < count; i++)
        {
            var rect = GetCardRect(view, count, i);
            if (point.X >= rect.Left && point.X <= rect.Right
                && point.Y >= rect.Top - SelectedLift && point.Y <= rect.Bottom)
            {
                return i;
            }
        }

        return -1;
    }

    // Cards sit in a centered row; they shrink to fit a narrow (split-screen) view.
    private static Rectangle GetCardRect(Rectangle view, int count, int index)
    {
        var available = view.Width - SideMargin * 2f - CardGap * (count - 1);
        var width = Math.Min(CardMaxWidth, available / count);
        var rowWidth = width * count + CardGap * (count - 1);
        var left = view.X + (view.Width - rowWidth) / 2f + index * (width + CardGap);
        var top = view.Y + (view.Height - CardHeight) / 2f;

        return new Rectangle((int)left, (int)top, (int)width, (int)CardHeight);
    }

    private sealed class SlotState
    {
        public int Selected { get; set; }

        public float OpenSeconds { get; set; }

        public bool Armed { get; set; }

        public void Reset()
        {
            Selected = 0;
            OpenSeconds = 0f;
            Armed = false;
        }
    }
}
