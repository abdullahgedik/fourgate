using System;
using System.Collections.Generic;
using Fourgate.Core.Audio;
using Fourgate.Core.Data;
using Fourgate.Core.Input;
using Fourgate.Core.Persistence;
using Fourgate.Core.Rendering;
using Fourgate.UI;
using Microsoft.Xna.Framework;

namespace Fourgate.Scenes;

/// <summary>
/// Settings: window mode, windowed size, screen shake, three volumes and the keyboard controls.
/// Left/right (or Enter, or a click) changes the highlighted line; display changes apply at once.
/// CONTROLS opens the key list. Back or the BACK line leaves.
/// </summary>
public sealed class SettingsScene : IScene
{
    private const float TitleSize = 80f;
    private const float HintSize = 26f;
    private const int VolumeStep = 10;

    private const int WindowRow = 0;
    private const int ResolutionRow = 1;
    private const int ShakeRow = 2;
    private const int MasterRow = 3;
    private const int MusicRow = 4;
    private const int EffectsRow = 5;
    private const int ControlsRow = 6;
    private const int BackRow = 7;
    private const int RowCount = 8;

    private static readonly Color TitleColor = new(240, 234, 226);

    private readonly IAudioManager _audio;
    private HellBackdrop? _backdropField;
    private readonly IPlayerInput _input;
    private readonly GameSettings _settings;
    private readonly IReadOnlyList<Point> _resolutions;
    private readonly Action<GameSettings> _applyDisplay;
    private readonly Camera2D _camera;
    private readonly int _screenWidth;
    private readonly int _screenHeight;
    private readonly MenuList _menu;
    private readonly IKeyCapture? _keys;
    private readonly ControlsMenu? _controls;
    private readonly string[] _labels = new string[RowCount];
    private readonly bool[] _disabled = new bool[RowCount];
    private int _selected;
    private int _labelVersion;

    /// <param name="input">Player 1's input.</param>
    /// <param name="config">Game settings (screen size, camera).</param>
    /// <param name="settings">The settings to change (changed in place).</param>
    /// <param name="resolutions">Windowed sizes to choose from.</param>
    /// <param name="applyDisplay">Applies the window mode and size right away.</param>
    /// <param name="keys">The keyboard player's keys; without it CONTROLS is greyed out.</param>
    public SettingsScene(
        IPlayerInput input, GameConfig config, GameSettings settings, IReadOnlyList<Point> resolutions,
        Action<GameSettings> applyDisplay, IAudioManager? audio = null, IKeyCapture? keys = null)
    {
        _keys = keys;
        _input = input;
        _audio = audio ?? new NullAudioManager();
        _settings = settings;
        _resolutions = resolutions;
        _applyDisplay = applyDisplay;
        _screenWidth = config.VirtualWidth;
        _screenHeight = config.VirtualHeight;
        _camera = new Camera2D(new Rectangle(0, 0, _screenWidth, _screenHeight), config.Camera);
        _menu = new MenuList(new Vector2(_screenWidth / 2f, _screenHeight * 0.19f), 760f, 72f, 16f);
        if (keys is not null)
        {
            _controls = new ControlsMenu(keys.Bindings, _screenWidth, _screenHeight);
        }

        RefreshLabels();
    }

    /// <summary>True once the player left the screen; the settings should then be saved.</summary>
    public bool IsDone { get; private set; }

    private HellBackdrop Backdrop => _backdropField ??= new HellBackdrop(_screenWidth, _screenHeight);

    public void Update(float deltaSeconds)
    {
        Backdrop.Update(deltaSeconds);
        var before = _selected;
        var labelsBefore = _labelVersion;
        UpdateMenu(deltaSeconds);
        if (IsDone)
        {
            _audio.PlaySfx(SfxIds.UiBack);
        }
        else if (_labelVersion != labelsBefore)
        {
            _audio.PlaySfx(SfxIds.UiConfirm);
        }
        else if (_selected != before)
        {
            _audio.PlaySfx(SfxIds.UiMove);
        }
    }

    private void UpdateMenu(float deltaSeconds)
    {
        _input.Update(_camera.ViewCenter, _camera);
        if (_controls is { IsOpen: true } && _keys is not null)
        {
            if (_controls.Update(_input, _keys, deltaSeconds))
            {
                _labelVersion++;
            }

            return;
        }

        if (_input.BackPressed)
        {
            IsDone = true;
            return;
        }

        if (_input.MenuUpPressed)
        {
            _selected = (_selected + RowCount - 1) % RowCount;
        }

        if (_input.MenuDownPressed)
        {
            _selected = (_selected + 1) % RowCount;
        }

        if (_input.MenuLeftPressed)
        {
            Change(_selected, -1);
        }

        if (_input.MenuRightPressed)
        {
            Change(_selected, 1);
        }

        var clicked = false;
        if (_input.HasPointer)
        {
            var hovered = _menu.ButtonAt(_input.AimScreen, RowCount);
            if (hovered >= 0)
            {
                _selected = hovered;
                clicked = _input.FirePressed;
            }
        }

        if (clicked || _input.ConfirmPressed)
        {
            if (_selected == BackRow)
            {
                IsDone = true;
            }
            else if (_selected == ControlsRow)
            {
                if (_controls is not null)
                {
                    _controls.Open();
                    _labelVersion++;
                }
            }
            else
            {
                Change(_selected, 1);
            }
        }
    }

    public void Draw(IRenderer renderer)
    {
        renderer.BeginScreen();
        Backdrop.Draw(renderer);
        if (_controls is { IsOpen: true })
        {
            _controls.Draw(renderer, _input);
            renderer.End();
            return;
        }

        renderer.DrawText("SETTINGS", new Vector2(_screenWidth / 2f, _screenHeight * 0.07f), TitleSize, TitleColor, TextAlign.Center);
        _menu.Draw(renderer, _labels, _selected, _disabled);

        var hint = _input.HasPointer
            ? "Up / Down: choose   ·   Left / Right or Enter: change   ·   Esc: back"
            : "D-pad: choose and change   ·   B: back";
        HellBackdrop.DrawHint(renderer, hint, new Vector2(_screenWidth / 2f, _screenHeight * 0.955f), HintSize);
        if (_input.HasPointer)
        {
            renderer.FillRect(_input.AimScreen - new Vector2(10f, 1f), new Vector2(20f, 2f), TitleColor);
            renderer.FillRect(_input.AimScreen - new Vector2(1f, 10f), new Vector2(2f, 20f), TitleColor);
        }

        renderer.End();
    }

    private void Change(int row, int step)
    {
        switch (row)
        {
            case WindowRow:
                _settings.WindowMode = _settings.WindowMode == WindowMode.Windowed
                    ? WindowMode.BorderlessFullscreen
                    : WindowMode.Windowed;
                _applyDisplay(_settings);
                break;

            case ResolutionRow:
                if (_settings.WindowMode != WindowMode.Windowed || _resolutions.Count == 0)
                {
                    return;
                }

                var next = (CurrentResolutionIndex() + step + _resolutions.Count) % _resolutions.Count;
                _settings.ResolutionWidth = _resolutions[next].X;
                _settings.ResolutionHeight = _resolutions[next].Y;
                _applyDisplay(_settings);
                break;

            case ShakeRow:
                _settings.ScreenShake = !_settings.ScreenShake;
                break;

            case MasterRow:
                _settings.MasterVolume = StepVolume(_settings.MasterVolume, step);
                break;

            case MusicRow:
                _settings.MusicVolume = StepVolume(_settings.MusicVolume, step);
                break;

            case EffectsRow:
                _settings.SfxVolume = StepVolume(_settings.SfxVolume, step);
                break;

            default:
                return;
        }

        RefreshLabels();
    }

    // Steps of 10; going past either end wraps round, so Enter alone can reach every value.
    private static int StepVolume(int volume, int step)
    {
        var next = volume + step * VolumeStep;
        if (next > GameSettings.MaxVolume)
        {
            return 0;
        }

        return next < 0 ? GameSettings.MaxVolume : next;
    }

    private int CurrentResolutionIndex()
    {
        for (var i = 0; i < _resolutions.Count; i++)
        {
            if (_resolutions[i].X == _settings.ResolutionWidth && _resolutions[i].Y == _settings.ResolutionHeight)
            {
                return i;
            }
        }

        return 0;
    }

    // Built only when something changes, so drawing allocates nothing.
    private void RefreshLabels()
    {
        _labelVersion++;
        var windowed = _settings.WindowMode == WindowMode.Windowed;
        _labels[WindowRow] = $"WINDOW:  {(windowed ? "WINDOWED" : "BORDERLESS FULLSCREEN")}";
        _labels[ResolutionRow] = $"WINDOW SIZE:  {_settings.ResolutionWidth} × {_settings.ResolutionHeight}";
        _labels[ShakeRow] = $"SCREEN SHAKE:  {(_settings.ScreenShake ? "ON" : "OFF")}";
        _labels[MasterRow] = $"MASTER VOLUME:  {_settings.MasterVolume}%";
        _labels[MusicRow] = $"MUSIC VOLUME:  {_settings.MusicVolume}%";
        _labels[EffectsRow] = $"EFFECTS VOLUME:  {_settings.SfxVolume}%";
        _labels[ControlsRow] = "CONTROLS";
        _labels[BackRow] = "BACK";
        _disabled[ResolutionRow] = !windowed;
        _disabled[ControlsRow] = _controls is null;
    }
}
