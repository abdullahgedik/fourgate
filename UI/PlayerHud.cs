using System;
using System.Collections.Generic;
using Fourgate.Core.Data;
using Fourgate.Core.Rendering;
using Fourgate.Entities;
using Fourgate.Weapons;
using Microsoft.Xna.Framework;

namespace Fourgate.UI;

/// <summary>One player's overlay in the corners of their view: level, health, dash and weapon.</summary>
public sealed class PlayerHud
{
    private const float Margin = 28f;
    private const float BarWidth = 320f;
    private const float HpBarHeight = 28f;
    private const float DashBarHeight = 8f;
    private const float XpBarHeight = 10f;
    private const float BarGap = 6f;
    private const float Border = 3f;
    private const float HpTextSize = 24f;
    private const float LevelTextSize = 26f;
    private const float WeaponNameSize = 26f;
    private const float AmmoSize = 46f;
    private const float WeaponListSize = 22f;
    private const float WeaponListLineHeight = 24f;
    private const float WeaponListGap = 14f;

    private static readonly Color PanelColor = new(8, 9, 12, 200);
    private static readonly Color HpColor = new(215, 60, 60);
    private static readonly Color HpLowColor = new(255, 150, 60);
    private static readonly Color XpColor = new(170, 120, 255);
    private static readonly Color DashReadyColor = new(110, 200, 255);
    private static readonly Color DashChargingColor = new(60, 95, 130);
    private static readonly Color TextColor = new(240, 240, 245);
    private static readonly Color DimTextColor = new(170, 175, 190);
    private static readonly Color HighlightTextColor = new(255, 225, 120);
    private static readonly Color EmptyTextColor = new(200, 80, 80);

    private readonly XpConfig _xpConfig;

    // The texts are rebuilt only when their numbers change, to avoid allocating every frame.
    private int _hpShown = -1;
    private int _maxHpShown = -1;
    private string _hpText = string.Empty;

    private int _levelShown = -1;
    private string _levelText = string.Empty;
    private int _picksShown = -1;
    private string _picksText = string.Empty;

    private Weapon? _weaponShown;
    private int _weaponLevelShown = -1;
    private int _magazineShown = -1;
    private int _reserveShown = -1;
    private bool _reloadingShown;
    private string _weaponName = string.Empty;
    private string _ammoText = string.Empty;
    private readonly List<WeaponListEntry> _listEntries = new();

    public PlayerHud(XpConfig xpConfig)
    {
        _xpConfig = xpConfig;
    }

    /// <param name="renderer">Renderer in a screen-space pass.</param>
    /// <param name="player">The player to show.</param>
    /// <param name="view">The player's area of the virtual screen.</param>
    public void Draw(IRenderer renderer, Player player, Rectangle view)
    {
        var left = view.Left + Margin;
        var dashTop = view.Bottom - Margin - DashBarHeight;
        var hpTop = dashTop - BarGap - HpBarHeight;
        var xpTop = hpTop - BarGap - Border - XpBarHeight;
        var levelTop = xpTop - Border - LevelTextSize;

        DrawDash(renderer, player, new Vector2(left, dashTop));
        DrawHealth(renderer, player, new Vector2(left, hpTop));
        DrawProgression(renderer, player.Progression, new Vector2(left, xpTop), levelTop);
        DrawWeapon(renderer, player, view);
    }

    private static void DrawDash(IRenderer renderer, Player player, Vector2 topLeft)
    {
        var readiness = Math.Clamp(player.DashReadiness, 0f, 1f);
        DrawBar(renderer, topLeft, DashBarHeight, readiness, readiness >= 1f ? DashReadyColor : DashChargingColor);
    }

    private void DrawHealth(IRenderer renderer, Player player, Vector2 topLeft)
    {
        var hp = (int)MathF.Ceiling(player.Hp);
        var maxHp = (int)MathF.Ceiling(player.MaxHp);
        if (hp != _hpShown || maxHp != _maxHpShown)
        {
            _hpShown = hp;
            _maxHpShown = maxHp;
            _hpText = $"{hp} / {maxHp}";
        }

        var fraction = player.MaxHp > 0f ? Math.Clamp(player.Hp / player.MaxHp, 0f, 1f) : 0f;
        DrawBar(renderer, topLeft, HpBarHeight, fraction, fraction <= 0.3f ? HpLowColor : HpColor);
        renderer.DrawText(
            _hpText, topLeft + new Vector2(BarWidth / 2f, 1f), HpTextSize, TextColor, TextAlign.Center);
    }

    private void DrawProgression(IRenderer renderer, PlayerProgression progression, Vector2 barTopLeft, float textTop)
    {
        if (progression.Level != _levelShown)
        {
            _levelShown = progression.Level;
            _levelText = $"LEVEL {progression.Level}";
        }

        if (progression.BankedPicks != _picksShown)
        {
            _picksShown = progression.BankedPicks;
            _picksText = progression.BankedPicks == 1 ? "1 UPGRADE READY" : $"{progression.BankedPicks} UPGRADES READY";
        }

        var fraction = Math.Clamp(progression.Xp / (float)_xpConfig.XpToNext(progression.Level), 0f, 1f);
        DrawBar(renderer, barTopLeft, XpBarHeight, fraction, XpColor);

        renderer.DrawText(_levelText, new Vector2(barTopLeft.X, textTop), LevelTextSize, TextColor);
        if (progression.BankedPicks > 0)
        {
            renderer.DrawText(
                _picksText, new Vector2(barTopLeft.X + BarWidth, textTop), LevelTextSize, HighlightTextColor,
                TextAlign.Right);
        }
    }

    private void DrawWeapon(IRenderer renderer, Player player, Rectangle view)
    {
        var weapon = player.CurrentWeapon;
        if (weapon is null)
        {
            return;
        }

        if (!ReferenceEquals(weapon, _weaponShown)
            || weapon.Level != _weaponLevelShown
            || weapon.Magazine != _magazineShown
            || weapon.Reserve != _reserveShown
            || weapon.IsReloading != _reloadingShown)
        {
            _weaponShown = weapon;
            _weaponLevelShown = weapon.Level;
            _magazineShown = weapon.Magazine;
            _reserveShown = weapon.Reserve;
            _reloadingShown = weapon.IsReloading;

            _weaponName = $"{weapon.Config.DisplayName.ToUpperInvariant()}   LV {weapon.Level}";
            var reserve = weapon.HasInfiniteReserve ? "INF" : weapon.Reserve.ToString();
            _ammoText = weapon.IsReloading ? "RELOADING" : $"{weapon.Magazine} / {reserve}";
        }

        var right = view.Right - Margin;
        var ammoTop = view.Bottom - Margin - AmmoSize;
        renderer.DrawText(
            _ammoText, new Vector2(right, ammoTop), AmmoSize,
            weapon.IsReloading ? HighlightTextColor : TextColor, TextAlign.Right);
        renderer.DrawText(
            _weaponName, new Vector2(right, ammoTop - WeaponNameSize), WeaponNameSize, DimTextColor, TextAlign.Right);

        DrawWeaponList(renderer, player, right, ammoTop - WeaponNameSize - WeaponListGap);
    }

    // Carried weapons by slot number, lowest at the top, with the one in hand highlighted.
    private void DrawWeaponList(IRenderer renderer, Player player, float right, float bottom)
    {
        var weapons = player.Weapons;
        if (weapons.Count < 2)
        {
            return;
        }

        while (_listEntries.Count < weapons.Count)
        {
            _listEntries.Add(new WeaponListEntry());
        }

        var top = bottom - weapons.Count * WeaponListLineHeight;
        for (var i = 0; i < weapons.Count; i++)
        {
            var weapon = weapons[i];
            var entry = _listEntries[i];
            var ammo = weapon.HasInfiniteReserve ? -1 : weapon.Magazine + weapon.Reserve;
            if (!ReferenceEquals(entry.Weapon, weapon) || entry.Ammo != ammo)
            {
                entry.Weapon = weapon;
                entry.Ammo = ammo;
                entry.Text = ammo < 0
                    ? $"{SlotLabel(weapon.Config.Slot)}  {weapon.Config.DisplayName.ToUpperInvariant()}"
                    : $"{SlotLabel(weapon.Config.Slot)}  {weapon.Config.DisplayName.ToUpperInvariant()}  {ammo}";
            }

            var isCurrent = ReferenceEquals(weapon, player.CurrentWeapon);
            var color = isCurrent ? HighlightTextColor : (ammo == 0 ? EmptyTextColor : DimTextColor);
            renderer.DrawText(
                entry.Text, new Vector2(right, top + i * WeaponListLineHeight), WeaponListSize, color, TextAlign.Right);
        }
    }

    // The key that selects the slot: 1-9, then 0 and minus.
    private static string SlotLabel(int slot)
    {
        return slot switch
        {
            10 => "0",
            11 => "-",
            _ => slot.ToString(),
        };
    }

    private sealed class WeaponListEntry
    {
        public Weapon? Weapon { get; set; }

        public int Ammo { get; set; } = int.MinValue;

        public string Text { get; set; } = string.Empty;
    }

    private static void DrawBar(IRenderer renderer, Vector2 topLeft, float height, float fraction, Color fill)
    {
        var border = new Vector2(Border);
        renderer.FillRect(topLeft - border, new Vector2(BarWidth, height) + border * 2f, PanelColor);
        renderer.FillRect(topLeft, new Vector2(BarWidth * fraction, height), fill);
    }
}
