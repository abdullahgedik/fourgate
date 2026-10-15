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
    private const float EquipmentSize = 28f;

    // The reload bar under "RELOADING", and the drawn infinity sign (the font has no ∞).
    private const float ReloadBarWidth = 230f;
    private const float ReloadBarHeight = 8f;
    private const float ReloadBarGap = 4f;
    private const float InfinityWidth = 44f;
    private const float InfinityHeight = 20f;
    private const float InfinityThickness = 4f;
    private const float InfinityGap = 10f;
    private const int InfinitySegments = 28;

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
    private Weapon? _equipmentShown;
    private int _equipmentCountShown = -1;
    private string _equipmentText = string.Empty;

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
            // An unlimited reserve is shown as a drawn ∞ after the text.
            var reserve = weapon.HasInfiniteReserve ? string.Empty : weapon.Reserve.ToString();
            _ammoText = weapon.IsReloading
                ? "RELOADING"
                : weapon.UsesMagazine ? $"{weapon.Magazine} / {reserve}".TrimEnd() : reserve;
        }

        var right = view.Right - Margin;
        var ammoTop = view.Bottom - Margin - AmmoSize;
        var infinite = weapon.HasInfiniteReserve && !weapon.IsReloading;
        var textRight = infinite ? right - InfinityWidth - InfinityGap : right;
        renderer.DrawText(
            _ammoText, new Vector2(textRight, ammoTop), AmmoSize,
            weapon.IsReloading ? HighlightTextColor : TextColor, TextAlign.Right);
        if (infinite)
        {
            DrawInfinity(renderer, new Vector2(right - InfinityWidth / 2f, ammoTop + AmmoSize * 0.55f), TextColor);
        }

        if (weapon.IsReloading)
        {
            var barTopLeft = new Vector2(right - ReloadBarWidth, ammoTop + AmmoSize + ReloadBarGap);
            renderer.FillRect(barTopLeft, new Vector2(ReloadBarWidth, ReloadBarHeight), PanelColor);
            renderer.FillRect(
                barTopLeft, new Vector2(ReloadBarWidth * weapon.ReloadProgress, ReloadBarHeight), HighlightTextColor);
        }
        renderer.DrawText(
            _weaponName, new Vector2(right, ammoTop - WeaponNameSize), WeaponNameSize, DimTextColor, TextAlign.Right);

        var listTop = DrawGunList(renderer, player, right, ammoTop - WeaponNameSize - WeaponListGap);
        DrawEquipment(renderer, player, right, listTop - WeaponListGap);
    }

    // A lemniscate (figure of eight on its side) drawn from short line pieces.
    private static void DrawInfinity(IRenderer renderer, Vector2 center, Color color)
    {
        var halfWidth = InfinityWidth / 2f;
        var previous = Lemniscate(0f, halfWidth);
        for (var i = 1; i <= InfinitySegments; i++)
        {
            var point = Lemniscate(MathHelper.TwoPi * i / InfinitySegments, halfWidth);
            renderer.DrawLine(center + previous, center + point, InfinityThickness, color);
            previous = point;
        }
    }

    private static Vector2 Lemniscate(float t, float halfWidth)
    {
        var sin = MathF.Sin(t);
        var cos = MathF.Cos(t);
        var scale = 1f / (1f + sin * sin);
        // The bare curve is half as tall as wide; stretch it to the wanted height.
        return new Vector2(halfWidth * cos * scale, InfinityHeight * sin * cos * scale);
    }

    // The active equipment, with the button that uses it and how many are left.
    private void DrawEquipment(IRenderer renderer, Player player, float right, float bottom)
    {
        var equipment = player.ActiveEquipment;
        if (equipment is null)
        {
            return;
        }

        if (!ReferenceEquals(equipment, _equipmentShown) || equipment.Reserve != _equipmentCountShown)
        {
            _equipmentShown = equipment;
            _equipmentCountShown = equipment.Reserve;
            _equipmentText = $"G  {equipment.Config.DisplayName.ToUpperInvariant()}  {equipment.Reserve}";
        }

        var color = equipment.Reserve == 0 ? EmptyTextColor : TextColor;
        renderer.DrawText(
            _equipmentText, new Vector2(right, bottom - EquipmentSize), EquipmentSize, color, TextAlign.Right);
    }

    // Carried guns by slot number, lowest at the top, with the one in hand highlighted.
    // Returns the top of the list (or the given bottom when nothing is drawn).
    private float DrawGunList(IRenderer renderer, Player player, float right, float bottom)
    {
        var weapons = player.Weapons;
        var guns = 0;
        for (var i = 0; i < weapons.Count; i++)
        {
            if (weapons[i].Config.Category == WeaponCategory.Gun)
            {
                guns++;
            }
        }

        if (guns < 2)
        {
            return bottom;
        }

        while (_listEntries.Count < guns)
        {
            _listEntries.Add(new WeaponListEntry());
        }

        var top = bottom - guns * WeaponListLineHeight;
        var line = 0;
        for (var i = 0; i < weapons.Count; i++)
        {
            var weapon = weapons[i];
            if (weapon.Config.Category != WeaponCategory.Gun)
            {
                continue;
            }

            var entry = _listEntries[line];
            var ammo = weapon.HasInfiniteReserve ? -1 : weapon.Magazine + weapon.Reserve;
            if (!ReferenceEquals(entry.Weapon, weapon) || entry.Ammo != ammo)
            {
                entry.Weapon = weapon;
                entry.Ammo = ammo;
                entry.Text = ammo < 0
                    ? $"{weapon.Config.Slot}  {weapon.Config.DisplayName.ToUpperInvariant()}"
                    : $"{weapon.Config.Slot}  {weapon.Config.DisplayName.ToUpperInvariant()}  {ammo}";
            }

            var isCurrent = ReferenceEquals(weapon, player.CurrentWeapon);
            var color = isCurrent ? HighlightTextColor : (ammo == 0 ? EmptyTextColor : DimTextColor);
            renderer.DrawText(
                entry.Text, new Vector2(right, top + line * WeaponListLineHeight), WeaponListSize, color, TextAlign.Right);
            line++;
        }

        return top;
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
