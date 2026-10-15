using System;
using System.Collections.Generic;
using Fourgate.Core;
using Fourgate.Core.Rendering;
using Fourgate.Entities;
using Fourgate.Systems;
using Microsoft.Xna.Framework;

namespace Fourgate.UI;

/// <summary>
/// The upgrade card screen. Each player with an open offer sees their cards inside their
/// own view and chooses with their own controls. It is animated: the view dims and the cards
/// rise in one after another, the highlighted card lifts, a chosen card pops while the others
/// fall away, and after the last pick everything fades out as play resumes.
/// </summary>
public sealed class UpgradeScreen
{
    // Ignore input briefly after cards appear so a held fire button does not pick one by accident.
    private const float InputLockSeconds = 0.4f;

    private const float CardMaxWidth = 380f;
    private const float CardHeight = 300f;
    private const float CardGap = 28f;
    private const float SideMargin = 48f;
    private const float CardBorder = 5f;
    private const float SelectedLift = 14f;
    private const float SelectedGrowth = 0.04f;
    private const float HeaderSize = 56f;
    private const float SubHeaderSize = 30f;
    private const float TitleSize = 38f;
    private const float DescriptionSize = 27f;
    private const float DescriptionLineHeight = 34f;
    private const float DetailSize = 24f;
    private const float HintSize = 26f;

    // Animation timings (seconds) and distances.
    private const float DimInSeconds = 0.25f;
    private const float HeaderDelay = 0.05f;
    private const float HeaderInSeconds = 0.3f;
    private const float HeaderDrop = 30f;
    private const float CardsDelay = 0.1f;
    private const float CardStagger = 0.07f;
    private const float CardInSeconds = 0.4f;
    private const float CardRise = 90f;
    private const float LiftSpeed = 14f;
    private const float PickSeconds = 0.45f;
    private const float PickGrowth = 0.08f;
    private const float PickFall = 70f;
    private const float CloseSeconds = 0.3f;
    private const float CloseRise = 50f;

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
    private static readonly Color FlashColor = new(255, 245, 210);

    private readonly UpgradeSystem _upgrades;
    private readonly List<SlotState> _slots = new();

    public UpgradeScreen(UpgradeSystem upgrades)
    {
        _upgrades = upgrades;
    }

    /// <summary>True while something is still fading out after the last pick (play has resumed).</summary>
    public bool IsAnimating
    {
        get
        {
            for (var i = 0; i < _slots.Count; i++)
            {
                if (_slots[i].IsClosing || _slots[i].IsLeaving)
                {
                    return true;
                }
            }

            return false;
        }
    }

    /// <summary>Polls every player's controls and plays the screen's animations.</summary>
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
            slot.Animate(deltaSeconds);
            if (offer.Count == 0)
            {
                continue;
            }

            if (!slot.IsOpen)
            {
                slot.Open(offer);
            }

            // A chosen card plays out its moment before the choice takes effect.
            if (slot.PickedIndex >= 0)
            {
                if (slot.PickSeconds >= PickSeconds)
                {
                    FinishPick(player, i, slot);
                }

                continue;
            }

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
                slot.Pick(slot.Selected);
            }
        }
    }

    /// <summary>Plays the fade-out after the last pick while the game runs again.</summary>
    public void Tick(float deltaSeconds)
    {
        for (var i = 0; i < _slots.Count; i++)
        {
            _slots[i].Animate(deltaSeconds);
        }
    }

    /// <summary>Draws the cards of every player with an open offer; the renderer must be in a screen-space pass.</summary>
    public void Draw(IRenderer renderer, IReadOnlyList<Player> players, IReadOnlyList<Camera2D> cameras)
    {
        var anyOpen = _upgrades.AnyOfferOpen;
        for (var i = 0; i < players.Count; i++)
        {
            var view = cameras[i].ViewRect;
            var slot = i < _slots.Count ? _slots[i] : null;
            var offer = _upgrades.GetOffer(i);
            var centerX = view.X + view.Width / 2f;

            if (slot is not null && slot.IsOpen && offer.Count > 0)
            {
                // Cards on show; a card just chosen from the previous pick floats off behind the new ones.
                var dimIn = slot.KeepDim ? 1f : Ease.OutCubic(slot.OpenSeconds / DimInSeconds);
                FillView(renderer, view, DimColor * dimIn);
                DrawLeavingCard(renderer, slot, view);
                DrawOffer(renderer, players[i], slot, view);
                continue;
            }

            if (slot is { IsWaitingForNext: true })
            {
                // Between two picks: the dim stays while the next cards are dealt.
                FillView(renderer, view, DimColor);
                DrawLeavingCard(renderer, slot, view);
                continue;
            }

            if (slot is { IsClosing: true })
            {
                // The last pick is spent: the view brightens (unless a partner is still choosing).
                var closing = Ease.OutCubic(slot.CloseSeconds / CloseSeconds);
                FillView(renderer, view, DimColor * (anyOpen ? 1f : 1f - closing));
                DrawLeavingCard(renderer, slot, view);
                if (anyOpen)
                {
                    DrawReady(renderer, view, centerX, closing);
                }

                continue;
            }

            // Done choosing while a partner is still at it.
            if (anyOpen)
            {
                FillView(renderer, view, DimColor);
                DrawReady(renderer, view, centerX, 1f);
            }
        }
    }

    // The card just chosen keeps its popped size and floats up as it fades.
    private static void DrawLeavingCard(IRenderer renderer, SlotState slot, Rectangle view)
    {
        if (!slot.IsLeaving || slot.LeavingCard is null)
        {
            return;
        }

        var leaving = Ease.OutCubic(slot.LeavingSeconds / CloseSeconds);
        var rect = GetCardRect(view, slot.LeavingCount, slot.LeavingIndex);
        DrawCard(
            renderer, slot.LeavingCard, rect, -SelectedLift - CloseRise * leaving, 1f + SelectedGrowth + PickGrowth,
            1f - leaving, 1f, 0f);
    }

    // The choice takes effect; another banked pick deals new cards, otherwise the screen closes.
    private void FinishPick(Player player, int playerSlot, SlotState slot)
    {
        slot.SendChosenCardOff();
        _upgrades.Choose(player, playerSlot, slot.PickedIndex);
        if (player.Progression.BankedPicks > 0)
        {
            slot.WaitForNextOffer();
            return;
        }

        slot.Close();
    }

    private static void DrawOffer(IRenderer renderer, Player player, SlotState slot, Rectangle view)
    {
        var offer = slot.Cards;
        var centerX = view.X + view.Width / 2f;
        var cardsTop = GetCardRect(view, offer.Count, 0).Y;
        var picking = slot.PickedIndex >= 0 ? Math.Clamp(slot.PickSeconds / PickSeconds, 0f, 1f) : 0f;

        // The header drops in, and fades as a card is chosen.
        var header = Ease.OutCubic((slot.OpenSeconds - HeaderDelay) / HeaderInSeconds) * (1f - Ease.OutCubic(picking * 2f));
        var headerShift = -HeaderDrop * (1f - header);
        renderer.DrawText(
            "CHOOSE AN UPGRADE", new Vector2(centerX, cardsTop - 150f + headerShift), HeaderSize, HeaderColor * header,
            TextAlign.Center);

        var picks = player.Progression.BankedPicks;
        if (picks > 1)
        {
            renderer.DrawText(
                picks == 2 ? "1 MORE PICK AFTER THIS" : "MORE PICKS AFTER THIS",
                new Vector2(centerX, cardsTop - 82f + headerShift), SubHeaderSize, TextColor * header, TextAlign.Center);
        }

        for (var i = 0; i < offer.Count; i++)
        {
            // Each card rises into place a moment after the one before it, with a little overshoot.
            var arrival = (slot.OpenSeconds - CardsDelay - i * CardStagger) / CardInSeconds;
            var shown = Ease.OutCubic(arrival);
            var offset = (1f - Ease.OutBack(arrival)) * CardRise - SelectedLift * slot.Lift[i];
            var scale = 1f + SelectedGrowth * slot.Lift[i];
            var flash = 0f;

            if (slot.PickedIndex >= 0)
            {
                if (i == slot.PickedIndex)
                {
                    // The chosen card pops and flashes...
                    scale += PickGrowth * Ease.OutBack(picking * 2f);
                    flash = 1f - Ease.OutCubic(picking * 1.5f);
                }
                else
                {
                    // ...while the others drop away.
                    shown *= 1f - Ease.OutCubic(picking * 1.6f);
                    offset += PickFall * Ease.InCubic(picking);
                }
            }

            DrawCard(renderer, offer[i], GetCardRect(view, offer.Count, i), offset, scale, shown, slot.Lift[i], flash);
        }

        var hint = player.Input.HasPointer
            ? "CLICK A CARD,  OR  A / D  THEN  ENTER"
            : "LEFT / RIGHT  THEN  A";
        renderer.DrawText(
            hint, new Vector2(centerX, cardsTop + CardHeight + 44f), HintSize, HintColor * header, TextAlign.Center);
    }

    // One card: moved up or down by offset, grown by scale around its middle, faded by opacity;
    // highlight 0–1 blends in the selected colours, flash 0–1 washes it with light.
    private static void DrawCard(
        IRenderer renderer, UpgradeCard card, Rectangle rect, float offset, float scale, float opacity, float highlight,
        float flash)
    {
        if (opacity <= 0.01f)
        {
            return;
        }

        var size = new Vector2(rect.Width, rect.Height) * scale;
        var center = new Vector2(rect.X + rect.Width / 2f, rect.Y + rect.Height / 2f + offset);
        var topLeft = center - size / 2f;
        var border = new Vector2(CardBorder);

        renderer.FillRect(topLeft - border, size + border * 2f, Color.Lerp(BorderColor, BorderSelectedColor, highlight) * opacity);
        renderer.FillRect(topLeft, size, Color.Lerp(CardColor, CardSelectedColor, highlight) * opacity);

        var textScale = scale;
        renderer.DrawText(
            card.Title, new Vector2(center.X, topLeft.Y + 46f * textScale), TitleSize * textScale, TitleColor * opacity,
            TextAlign.Center);
        for (var line = 0; line < card.Lines.Count; line++)
        {
            renderer.DrawText(
                card.Lines[line],
                new Vector2(center.X, topLeft.Y + (112f + line * DescriptionLineHeight) * textScale),
                DescriptionSize * textScale,
                TextColor * opacity,
                TextAlign.Center);
        }

        renderer.DrawText(
            card.Detail, new Vector2(center.X, topLeft.Y + size.Y - 60f * textScale), DetailSize * textScale,
            DetailColor * opacity, TextAlign.Center);

        if (flash > 0f)
        {
            renderer.FillRect(topLeft - border, size + border * 2f, FlashColor * (0.55f * flash * opacity));
        }
    }

    private static void DrawReady(IRenderer renderer, Rectangle view, float centerX, float opacity)
    {
        renderer.DrawText(
            "READY", new Vector2(centerX, view.Y + view.Height / 2f - HeaderSize / 2f), HeaderSize, HintColor * opacity,
            TextAlign.Center);
    }

    private static void FillView(IRenderer renderer, Rectangle view, Color color)
    {
        renderer.FillRect(new Vector2(view.X, view.Y), new Vector2(view.Width, view.Height), color);
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
        // More cards than any offer shows.
        private const int MaxCards = 8;

        private readonly List<UpgradeCard> _cards = new();

        public int Selected { get; set; }

        public float OpenSeconds { get; private set; }

        public bool Armed { get; set; }

        public bool IsOpen { get; private set; }

        /// <summary>The offer being shown (kept after the choice, for the closing animation).</summary>
        public IReadOnlyList<UpgradeCard> Cards => _cards;

        /// <summary>How lifted each card is (0–1), easing toward the highlighted one.</summary>
        public float[] Lift { get; } = new float[MaxCards];

        public int PickedIndex { get; private set; } = -1;

        public float PickSeconds { get; private set; }

        public float CloseSeconds { get; private set; } = -1f;

        public bool IsClosing => CloseSeconds >= 0f && CloseSeconds < UpgradeScreen.CloseSeconds;

        /// <summary>True after a pick while the next cards are being dealt.</summary>
        public bool IsWaitingForNext => !IsOpen && KeepDim;

        /// <summary>The card just chosen, floating off; null when there is none.</summary>
        public UpgradeCard? LeavingCard { get; private set; }

        public int LeavingIndex { get; private set; }

        public int LeavingCount { get; private set; }

        public float LeavingSeconds { get; private set; } = -1f;

        public bool IsLeaving => LeavingSeconds >= 0f && LeavingSeconds < UpgradeScreen.CloseSeconds;

        public void SendChosenCardOff()
        {
            LeavingCard = _cards[PickedIndex];
            LeavingIndex = PickedIndex;
            LeavingCount = _cards.Count;
            LeavingSeconds = 0f;
        }

        /// <summary>True for a follow-up offer: the view is already dimmed.</summary>
        public bool KeepDim { get; private set; }

        public void Open(IReadOnlyList<UpgradeCard> offer)
        {
            _cards.Clear();
            for (var i = 0; i < offer.Count; i++)
            {
                _cards.Add(offer[i]);
            }

            IsOpen = true;
            OpenSeconds = 0f;
            Selected = 0;
            Armed = false;
            PickedIndex = -1;
            PickSeconds = 0f;
            CloseSeconds = -1f;
            Array.Clear(Lift);
        }

        public void Pick(int index)
        {
            PickedIndex = index;
            PickSeconds = 0f;
        }

        // The next banked pick deals new cards on the next frame; the dim stays.
        public void WaitForNextOffer()
        {
            IsOpen = false;
            KeepDim = true;
        }

        public void Close()
        {
            IsOpen = false;
            KeepDim = false;
            CloseSeconds = 0f;
        }

        public void Animate(float deltaSeconds)
        {
            if (CloseSeconds >= 0f)
            {
                CloseSeconds += deltaSeconds;
            }

            if (LeavingSeconds >= 0f)
            {
                LeavingSeconds += deltaSeconds;
            }

            if (!IsOpen)
            {
                return;
            }

            OpenSeconds += deltaSeconds;
            if (PickedIndex >= 0)
            {
                PickSeconds += deltaSeconds;
            }

            var step = Math.Min(1f, deltaSeconds * LiftSpeed);
            for (var i = 0; i < _cards.Count && i < MaxCards; i++)
            {
                var target = i == Selected ? 1f : 0f;
                Lift[i] += (target - Lift[i]) * step;
            }
        }
    }
}
