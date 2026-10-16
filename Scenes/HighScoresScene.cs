using Fourgate.Core.Audio;
using Fourgate.Core.Data;
using Fourgate.Core.Input;
using Fourgate.Core.Persistence;
using Fourgate.Core.Rendering;
using Fourgate.Systems;
using Fourgate.UI;
using Microsoft.Xna.Framework;

namespace Fourgate.Scenes;

/// <summary>The four high-score lists (one per mode) side by side; confirm goes back.</summary>
public sealed class HighScoresScene : IScene
{
    private const float TitleSize = 80f;
    private const float HintSize = 26f;

    private static readonly Color TitleColor = new(240, 234, 226);

    private static readonly RunMode[] Modes =
    {
        new(false, false), new(true, false), new(false, true), new(true, true),
    };

    private readonly IAudioManager _audio;
    private HellBackdrop? _backdropField;
    private readonly IPlayerInput _input;
    private readonly HighScoreTable _table;
    private readonly Camera2D _camera;
    private readonly int _screenWidth;
    private readonly int _screenHeight;
    private readonly HighScoreListView[] _views = new HighScoreListView[Modes.Length];

    public HighScoresScene(IPlayerInput input, HighScoreTable table, GameConfig config, IAudioManager? audio = null)
    {
        _input = input;
        _audio = audio ?? new NullAudioManager();
        _table = table;
        _screenWidth = config.VirtualWidth;
        _screenHeight = config.VirtualHeight;
        _camera = new Camera2D(new Rectangle(0, 0, _screenWidth, _screenHeight), config.Camera);
        for (var i = 0; i < _views.Length; i++)
        {
            _views[i] = new HighScoreListView();
        }
    }

    /// <summary>True once the player asked to go back.</summary>
    public bool IsDone { get; private set; }

    private HellBackdrop Backdrop => _backdropField ??= new HellBackdrop(_screenWidth, _screenHeight);

    public void Update(float deltaSeconds)
    {
        Backdrop.Update(deltaSeconds);
        _input.Update(_camera.ViewCenter, _camera);
        if (_input.ConfirmPressed || _input.BackPressed || (_input.HasPointer && _input.FirePressed))
        {
            IsDone = true;
            _audio.PlaySfx(SfxIds.UiBack);
        }
    }

    public void Draw(IRenderer renderer)
    {
        renderer.BeginScreen();
        Backdrop.Draw(renderer);
        renderer.DrawText("HIGH SCORES", new Vector2(_screenWidth / 2f, 40f), TitleSize, TitleColor, TextAlign.Center);

        // A 2 × 2 grid: Normal and Fast on top, Nightmare and Nightmare + Fast below.
        for (var i = 0; i < Modes.Length; i++)
        {
            var column = i % 2;
            var row = i / 2;
            var topCenter = new Vector2(_screenWidth * (0.27f + 0.46f * column), 170f + row * 430f);
            _views[i].Draw(renderer, Modes[i].Name, _table.Get(Modes[i].Key), _table.Version, topCenter, -1);
        }

        HellBackdrop.DrawHint(
            renderer, "Enter, Esc or click to go back", new Vector2(_screenWidth / 2f, _screenHeight - 50f), HintSize);
        renderer.End();
    }
}
