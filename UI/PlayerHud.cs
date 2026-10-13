using System;
using Fourgate.Core.Rendering;
using Fourgate.Entities;
using Fourgate.Weapons;
using Microsoft.Xna.Framework;

namespace Fourgate.UI;

/// <summary>One player's overlay in the corners of their view: health, dash and weapon.</summary>
public sealed class PlayerHud
{
    private const float Margin = 28f;
    private const float BarWidth = 320f;
    private const float HpBarHeight = 28f;
    private const float DashBarHeight = 8f;
    private const float BarGap = 6f;
    private const float Border = 3f;
    private const float HpTextSize = 24f;
    private const float WeaponNameSize = 26f;
    private const float AmmoSize = 46f;

    private static readonly Color PanelColor = new(8, 9, 12, 200);
    private static readonly Color HpColor = new(215, 60, 60);
    private static readonly Color HpLowColor = new(255, 150, 60);
    private static readonly Color DashReadyColor = new(110, 200, 255);
    private static readonly Color DashChargingColor = new(60, 95, 130);
    private static readonly Color TextColor = new(240, 240, 245);
    private static readonly Color DimTextColor = new(170, 175, 190);
    private static readonly Color ReloadTextColor = new(255, 225, 120);

    // The texts are rebuilt only when their numbers change, to avoid allocating every frame.
    private int _hpShown = -1;
    private int _maxHpShown = -1;
    private string _hpText = string.Empty;

    private Weapon? _weaponShown;
    private int _magazineShown = -1;
    private int _reserveShown = -1;
    private bool _reloadingShown;
    private string _weaponName = string.Empty;
    private string _ammoText = string.Empty;

    /// <param name="renderer">Renderer in a screen-space pass.</param>
    /// <param name="player">The player to show.</param>
    /// <param name="view">The player's area of the virtual screen.</param>
    public void Draw(IRenderer renderer, Player player, Rectangle view)
    {
        DrawHealthAndDash(renderer, player, view);
        DrawWeapon(renderer, player, view);
    }

    private void DrawHealthAndDash(IRenderer renderer, Player player, Rectangle view)
    {
        var hp = (int)MathF.Ceiling(player.Hp);
        var maxHp = (int)MathF.Ceiling(player.MaxHp);
        if (hp != _hpShown || maxHp != _maxHpShown)
        {
            _hpShown = hp;
            _maxHpShown = maxHp;
            _hpText = $"{hp} / {maxHp}";
        }

        var dashTop = view.Bottom - Margin - DashBarHeight;
        var hpTop = dashTop - BarGap - HpBarHeight;
        var left = view.Left + Margin;

        var fraction = player.MaxHp > 0f ? Math.Clamp(player.Hp / player.MaxHp, 0f, 1f) : 0f;
        DrawBar(renderer, new Vector2(left, hpTop), HpBarHeight, fraction, fraction <= 0.3f ? HpLowColor : HpColor);
        renderer.DrawText(
            _hpText, new Vector2(left + BarWidth / 2f, hpTop + 1f), HpTextSize, TextColor, TextAlign.Center);

        var readiness = Math.Clamp(player.DashReadiness, 0f, 1f);
        DrawBar(
            renderer, new Vector2(left, dashTop), DashBarHeight, readiness,
            readiness >= 1f ? DashReadyColor : DashChargingColor);
    }

    private void DrawWeapon(IRenderer renderer, Player player, Rectangle view)
    {
        var weapon = player.CurrentWeapon;
        if (weapon is null)
        {
            return;
        }

        if (!ReferenceEquals(weapon, _weaponShown)
            || weapon.Magazine != _magazineShown
            || weapon.Reserve != _reserveShown
            || weapon.IsReloading != _reloadingShown)
        {
            _weaponShown = weapon;
            _magazineShown = weapon.Magazine;
            _reserveShown = weapon.Reserve;
            _reloadingShown = weapon.IsReloading;

            _weaponName = weapon.Config.DisplayName.ToUpperInvariant();
            var reserve = weapon.HasInfiniteReserve ? "INF" : weapon.Reserve.ToString();
            _ammoText = weapon.IsReloading ? "RELOADING" : $"{weapon.Magazine} / {reserve}";
        }

        var right = view.Right - Margin;
        var ammoTop = view.Bottom - Margin - AmmoSize;
        renderer.DrawText(
            _ammoText, new Vector2(right, ammoTop), AmmoSize,
            weapon.IsReloading ? ReloadTextColor : TextColor, TextAlign.Right);
        renderer.DrawText(
            _weaponName, new Vector2(right, ammoTop - WeaponNameSize), WeaponNameSize, DimTextColor, TextAlign.Right);
    }

    private static void DrawBar(IRenderer renderer, Vector2 topLeft, float height, float fraction, Color fill)
    {
        var border = new Vector2(Border);
        renderer.FillRect(topLeft - border, new Vector2(BarWidth, height) + border * 2f, PanelColor);
        renderer.FillRect(topLeft, new Vector2(BarWidth * fraction, height), fill);
    }
}
