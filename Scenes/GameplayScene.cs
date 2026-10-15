using System;
using System.Collections.Generic;
using System.Globalization;
using Fourgate.Core;
using Fourgate.Core.Audio;
using Fourgate.Core.Data;
using Fourgate.Core.Input;
using Fourgate.Core.Persistence;
using Fourgate.Core.Rendering;
using Fourgate.Core.Rendering.Models;
using Fourgate.Core.Rng;
using Fourgate.Entities;
using Fourgate.Entities.Enemies;
using Fourgate.Entities.Pickups;
using Fourgate.Systems;
using Fourgate.UI;
using Fourgate.Weapons;
using Microsoft.Xna.Framework;

namespace Fourgate.Scenes;

/// <summary>The arena and everything that happens in it.</summary>
public sealed class GameplayScene : IScene
{
    // Floor grid spacing when no weapon snaps to a grid.
    private const float GridSpacing = 100f;
    private const float GridLineThickness = 2f;

    // Heights for the 3D look (ignored in 2D).
    private const float WallHeight = 90f;
    private const float PillarHeight = 120f;
    private const float GateInset = 2f;
    private const float GunBottom = 14f;
    private const float GunTop = 22f;
    private const float FacingLineLength = 14f;
    private const float FacingLineThickness = 6f;
    private const float DualBarrelOffset = 7f;
    private const float DualBarrelThickness = 5f;
    private const float CrosshairArm = 14f;
    private const float CrosshairThickness = 2f;
    private const float DoorHaloDepth = 110f;
    private const float DoorHaloOpacity = 0.3f;

    // The 3D door warning: pulses that quicken as the enemies near, a curtain of light in the
    // opening, lamps on the gate posts and light spilled deeper onto the floor.
    private const float DoorPulseSlow = 4f;
    private const float DoorPulseFast = 14f;
    private const float DoorCurtainOpacity = 0.35f;
    private const float DoorLampRadius = 16f;
    private const float DoorFloorLightDepth = 260f;
    private const int DoorFloorLightBands = 5;

    // Once its enemies are out, a door's warning fades over this long instead of vanishing.
    private const float DoorFadeSeconds = 0.4f;

    // Bodies (3D only): how long they lie before shrinking away, and how much darker a dead zombie is.
    private const float EnemyCorpseSeconds = 1.6f;
    private const float PlayerCorpseSeconds = 8f;
    private const float BossCorpseSeconds = 6f;
    private const float CorpseDarkening = 0.75f;

    // The lethal-attack warning on screen.
    private const float LethalPulseSpeed = 14f;
    private const float LethalEdgeOpacity = 0.35f;
    private const float LethalEdgeThickness = 26f;
    private const float SkullRadius = 26f;
    private const float SkullLift = 90f;
    private const float SkullMargin = 50f;
    private const float LethalTextSize = 30f;
    private static readonly Color LethalEdgeColor = new(235, 0, 85);
    private static readonly Color LethalSkullColor = new(255, 60, 110);
    private static readonly Color SkullHoleColor = new(20, 0, 8);

    // After the last player dies: "YOU DIED" grows in over a red tint, the world carries on, and
    // the results screen fades in a little later (its choices are read once it is in).
    private const float DeathScreenDelay = 2.2f;
    private const float DeathTextFadeSeconds = 0.9f;
    private const float DeathTextSize = 150f;
    private const float DeathTextStartScale = 1.3f;
    private const float DeathTintOpacity = 0.35f;
    private const float GameOverFadeSeconds = 0.6f;
    private static readonly Color DeathTintColor = new(80, 0, 0);
    private static readonly Color DeathTextColor = new(220, 40, 40);
    private const float EdgeIndicatorInset = 40f;
    private const float EdgeIndicatorRadius = 18f;
    private const float WeaponCrateSpacing = 70f;
    private const float DebugNoticeSeconds = 2f;
    private const float PlayerSpawnSpacing = 90f;

    // Screen shake: how far away a blast still shakes a view, the blast radius that gives the
    // full amount, and the trauma added by a blast and by getting hurt.
    private const float ShakeReach = 1400f;
    private const float ShakeRadiusForFull = 260f;
    private const float ExplosionShake = 0.9f;
    private const float HurtShake = 0.55f;

    // A red flash round the edges of a hurt player's view.
    private const float HurtFlashSeconds = 0.35f;
    private const float HurtFlashThickness = 60f;
    private const float HurtFeedbackMinDamage = 2f;
    private const float BossCrateSpacing = 70f;
    private const float BossIndicatorRadius = 24f;
    private const float SplitLineThickness = 4f;
    private const float DownedNoticeY = 230f;
    private const float PauseTitleSize = 60f;
    private const float PauseTextSize = 32f;
    private const float DownedNoticeSize = 30f;
    private const float DebugNoticeY = 190f;
    private const float DebugNoticeSize = 28f;

    private static readonly Color WallColor = new(70, 74, 92);
    private static readonly Color FloorColor = new(34, 36, 46);
    private static readonly Color GridColor = new(42, 45, 57);
    private static readonly Color GateColor = new(150, 60, 50);
    private static readonly Color GateGlowColor = new(255, 190, 70);
    private static readonly Color EdgeIndicatorCoreColor = new(40, 20, 10);
    private static readonly Color WeaponCrateIndicatorColor = new(60, 170, 230);
    private static readonly Color FacingColor = new(225, 230, 240);
    private static readonly Color CrosshairColor = new(240, 240, 240);
    private static readonly Color DebugNoticeColor = new(255, 225, 120);
    private static readonly Color SplitLineColor = new(10, 10, 14);
    private static readonly Color DownedNoticeColor = new(255, 120, 100);
    private static readonly Color BossIndicatorColor = new(230, 50, 45);
    private static readonly Color HurtFlashColor = new(220, 20, 20);
    private static readonly Color PauseDimColor = new(0, 0, 0, 185);
    private static readonly Color PauseTitleColor = new(240, 240, 245);
    private static readonly Color PauseTextColor = new(170, 175, 190);

    private readonly GameData _data;
    private readonly float _floorCellSize;
    private readonly GameRandom _random;
    private readonly Arena _arena;
    private readonly ShotSystem _shots;
    private readonly ExplosiveSystem _explosives;
    private readonly HazardSystem _hazards;
    private readonly MineSystem _mines;
    private readonly FlameSystem _flames;
    private readonly ShotRouter _shotRouter;
    private readonly EnemySystem _enemies;
    private readonly WaveSystem? _waves;
    private readonly XpSystem _xp;
    private readonly DropSystem _drops;
    private readonly UpgradeSystem _upgrades;
    private readonly WeaponUnlocks _unlocks;
    private int _unlocksAppliedUpToWave;
    private readonly UpgradeScreen _upgradeScreen;
    private readonly WaveHud _waveHud = new();
    private readonly EnemyShotSystem _fireballs = new();
    private readonly BossHud _bossHud = new();
    private readonly BossSystem _bosses;
    private readonly ParticleSystem _particles;
    private readonly CorpseSystem _corpses = new();
    private readonly List<bool> _wasAlive = new();
    private readonly GameRandom _visualRandom = new(Environment.TickCount);
    private readonly GameSettings? _settings;
    private float _doorPulseTime;

    // -1 while someone is alive; then seconds since the last player died.
    private float _deathSeconds = -1f;
    private float _gameOverSeconds;
    private readonly float[] _doorGlow;

    /// <summary>Blood that stays on the floor for the whole run; painted by the game before each frame.</summary>
    public FloorStains FloorStains { get; }

    /// <summary>Soot left by explosions on the floor and on walls and pillars.</summary>
    public ScorchSystem Scorches { get; }
    private readonly List<float> _lastHp = new();
    private readonly List<float> _hurtFlashLeft = new();
    private readonly ModeRules _rules;
    private readonly string _modeLabel;
    private readonly ScoreSystem _score;
    private readonly ScoreHud _scoreHud = new();
    private readonly HighScoreTable? _highScores;
    private readonly HighScoreStore? _highScoreStore;
    private InitialsEntry? _initials;
    private readonly PauseOverlay _pauseOverlay;
    private readonly VictoryOverlay _victoryOverlay;
    private int _pausedBy = -1;
    private bool _justResumed;
    private bool _victoryOpen;
    private bool _victoryShown;
    private int _newEntryPlace = -1;
    private string _debugNotice = string.Empty;
    private float _debugNoticeLeft;
    private string _disconnectedNotice = string.Empty;
    private readonly GameOverOverlay _gameOverOverlay = new();
    private readonly IAudioManager _audio;
    private float _elapsedSeconds;
    private readonly List<PracticeTarget> _practiceTargets = new();

    // _cameras[i] is the view of _players[i].
    private readonly List<Player> _players = new();
    private readonly List<Camera2D> _cameras = new();
    private readonly List<PlayerHud> _playerHuds = new();

    // Each player's half of the screen in co-op, whether their view is drawn (a downed
    // player's is not while the partner fights on), and the notice shown to the partner.
    private readonly List<Rectangle> _splitViews = new();
    private readonly List<bool> _viewShown = new();
    private readonly List<string> _downedNotices = new();
    private readonly Rectangle _fullView;
    private readonly WeaponConfig _startingWeapon;
    private WavePhase? _lastWavePhase;

    /// <param name="data">All game data.</param>
    /// <param name="inputs">One input per player: one for single-player, two for co-op.</param>
    /// <param name="audio">Audio events.</param>
    /// <param name="mode">Fast / Nightmare toggles; Normal when left out.</param>
    /// <param name="highScores">The local high scores, or null to keep none (tests).</param>
    /// <param name="highScoreStore">Where to save them after a new entry, or null.</param>
    public GameplayScene(
        GameData data, IReadOnlyList<IPlayerInput> inputs, IAudioManager audio, RunMode mode = default,
        HighScoreTable? highScores = null, HighScoreStore? highScoreStore = null, GameSettings? settings = null)
    {
        _data = data;
        _floorCellSize = FloorCellSize(data);
        _audio = audio;
        var config = data.Game;
        _random = new GameRandom(config.Debug.Seed ?? Environment.TickCount);
        _arena = new Arena(config.Arena);
        _doorGlow = new float[_arena.Doors.Count];
        FloorStains = new FloorStains(new Vector2(_arena.Left, _arena.Top), _arena.Size);
        Scorches = new ScorchSystem(_arena, FloorStains, WallHeight, PillarHeight);
        _enemies = new EnemySystem(audio) { KnockbackDamping = config.Knockback.Damping, Fireballs = _fireballs };
        _bosses = new BossSystem(_enemies, data, _random, audio)
        {
            MaxAliveEnemies = config.MaxAliveEnemies,
            Fireballs = _fireballs,
        };
        _shots = new ShotSystem(_arena, audio);
        _shots.AddSource(_enemies);
        _xp = new XpSystem(config.Xp, audio);
        _shots.OneShotKills = config.Debug.OneShotKills;
        _hazards = new HazardSystem(config.FriendlyFire);
        _hazards.AddSource(_enemies);
        _explosives = new ExplosiveSystem(_arena, config.FriendlyFire, _random, audio, _hazards)
        {
            KnockbackDistance = config.Knockback.Explosion,
        };
        _shots.Explosives = _explosives;
        _explosives.AddSource(_enemies);
        _explosives.OneShotKills = config.Debug.OneShotKills;
        _mines = new MineSystem(_explosives, _random, audio, _hazards) { Arena = _arena };
        _arena.Obstacles = _mines;
        _mines.AddSource(_enemies);

        // Barrels can be hit by everything a player does.
        _shots.AddSource(_mines);
        _explosives.AddSource(_mines);
        _hazards.AddSource(_mines);
        _flames = new FlameSystem(_arena, _hazards);

        // Visual only, with its own random source so effects never change a seeded run.
        _particles = new ParticleSystem(_visualRandom);
        _particles.Stains = FloorStains;
        _shots.Particles = _particles;
        _bosses.Particles = _particles;
        _bosses.Impact = OnExplosion;
        _explosives.Exploded = OnExplosion;
        _hazards.Particles = _particles;
        _explosives.Particles = _particles;
        _enemies.Particles = _particles;
        _fireballs.Particles = _particles;
        _flames.Particles = _particles;
        _flames.AddSource(_enemies);
        _flames.AddSource(_mines);
        _flames.OneShotKills = config.Debug.OneShotKills;
        _shotRouter = new ShotRouter(_shots, _explosives, _mines, _flames);
        _unlocks = new WeaponUnlocks(config, audio, _random);
        _drops = new DropSystem(config.Crates, _random, audio, _unlocks);
        _upgrades = new UpgradeSystem(data.Upgrades, _unlocks, _random, audio);
        _upgradeScreen = new UpgradeScreen(_upgrades);
        audio.PlayMusic(MusicIds.Gameplay);
        _pauseOverlay = new PauseOverlay(config.VirtualWidth, config.VirtualHeight);
        _victoryOverlay = new VictoryOverlay(config.VirtualWidth, config.VirtualHeight);
        _settings = settings;
        _highScores = highScores;
        _highScoreStore = highScoreStore;
        _rules = new ModeRules(data.Modes, mode);
        _score = new ScoreSystem(config.Score, _rules.ScoreMultiplier);
        _modeLabel = mode == default ? string.Empty : mode.Name;
        _enemies.SpeedMultiplier = _rules.EnemySpeedMultiplier;
        _enemies.WanderWhenAlone = true;
        _drops.HealthChanceMultiplier = _rules.HealthCrateChanceMultiplier;
        _waves = config.Debug.DisableWaves ? null : new WaveSystem(data, _enemies, _arena, _random, audio) { Rules = _rules };

        var startingWeapon = data.GetWeapon(config.Player.StartingWeapon);
        _startingWeapon = startingWeapon;
        _unlocks.Unlock(startingWeapon);
        _fullView = new Rectangle(0, 0, config.VirtualWidth, config.VirtualHeight);

        // Side by side in the middle; with two players the screen is split down the middle,
        // player 1 on the left.
        var viewWidth = config.VirtualWidth / inputs.Count;
        for (var i = 0; i < inputs.Count; i++)
        {
            var offset = (i - (inputs.Count - 1) / 2f) * PlayerSpawnSpacing;
            var player = new Player(i, inputs[i], config.Player, audio, _arena.Center + new Vector2(offset, 0f));
            _unlocks.Grant(player, startingWeapon);
            AddPlayer(player, new Rectangle(i * viewWidth, 0, viewWidth, config.VirtualHeight), config.Camera);
        }

        if (_rules.AllWeaponsAtStart)
        {
            GiveEveryWeapon();
        }

        _lastWavePhase = _waves?.Phase;
        if (inputs.Count > 1)
        {
            _bosses.HpMultiplier = config.Coop.BossHpMultiplier;
            if (_waves is not null)
            {
                _waves.BudgetMultiplier = config.Coop.EnemyBudgetMultiplier;
            }
        }

        if (config.Debug.PracticeTargets)
        {
            AddPracticeTargets(config.Player.Radius);
        }
    }

    /// <summary>
    /// True once the run has ended and the game over screen is up. After a death the world keeps
    /// going behind it (zombies wander off); after a finished campaign it stands still.
    /// </summary>
    public bool IsGameOver { get; private set; }

    /// <summary>True between the last player's death and the game over screen ("YOU DIED").</summary>
    public bool IsDying => _deathSeconds >= 0f && !IsGameOver;

    /// <summary>True when a player asked to start over from the game over screen.</summary>
    public bool RestartRequested { get; private set; }

    public void Update(float deltaSeconds)
    {
        _debugNoticeLeft = Math.Max(0f, _debugNoticeLeft - deltaSeconds);
        for (var i = 0; i < _hurtFlashLeft.Count; i++)
        {
            _hurtFlashLeft[i] = Math.Max(0f, _hurtFlashLeft[i] - deltaSeconds);
        }
        if (IsGameOver)
        {
            if (_deathSeconds >= 0f)
            {
                _deathSeconds += deltaSeconds;
                UpdateWorldWithoutPlayers(deltaSeconds);
            }

            // Choices are read once the screen has faded in.
            _gameOverSeconds += deltaSeconds;
            if (_gameOverSeconds >= GameOverFadeSeconds)
            {
                UpdateGameOver();
            }

            return;
        }

        if (_deathSeconds >= 0f)
        {
            _deathSeconds += deltaSeconds;
            UpdateWorldWithoutPlayers(deltaSeconds);
            if (_deathSeconds >= DeathScreenDelay)
            {
                EndRun("GAME OVER");
            }

            return;
        }

        if (_victoryOpen)
        {
            UpdateVictory();
            return;
        }

        if (IsPaused)
        {
            UpdatePause();
            return;
        }

        // Opening the menu takes the whole frame; it starts reading choices on the next one.
        if (TryPause())
        {
            return;
        }

        // An unplugged controller stops everything until it is back.
        if (UpdateDisconnected())
        {
            return;
        }

        // Banked picks are spent between waves. The world stands still while anyone is choosing.
        var isResting = _waves is null || _waves.Phase == WavePhase.Rest;
        if (isResting && _upgrades.OpenOffers(_players))
        {
            _upgradeScreen.Update(deltaSeconds, _players, _cameras);
            return;
        }

        // After the last pick the cards fade out while play has already resumed.
        _upgradeScreen.Tick(deltaSeconds);

        _elapsedSeconds += deltaSeconds;
        _score.AddTime(deltaSeconds);

        for (var i = 0; i < _practiceTargets.Count; i++)
        {
            _practiceTargets[i].Update(deltaSeconds);
        }

        _waves?.Update(deltaSeconds);
        if (_lastWavePhase == WavePhase.Active && _waves?.Phase == WavePhase.Rest)
        {
            _score.AddClearedWave();
            ReviveDownedPlayers();
            if (!_victoryShown && _waves!.Wave - 1 >= _data.Game.CampaignWaves)
            {
                // The last boss wave is cleared: the run stops here until the players decide.
                _victoryShown = true;
                _victoryOpen = true;
                _victoryOverlay.Open(_waves.Wave - 1, _score.Total);
                _audio.PlaySfx(SfxIds.WavesSurvived);
            }
        }

        _lastWavePhase = _waves?.Phase;
        UnlockWeaponsForStartedWaves();
        _enemies.Update(deltaSeconds, _players, _arena);
        _bosses.Update(deltaSeconds, _players, _arena);
        _fireballs.Update(deltaSeconds, _players, _arena);
        RewardKills();

        for (var i = 0; i < _players.Count; i++)
        {
            var player = _players[i];
            var camera = _cameras[i];

            player.Input.Update(player.Position, camera);
            player.Update(deltaSeconds, _arena, _shotRouter);
            camera.Follow(player.Position, player.Input.AimScreen, deltaSeconds);
            ShakeOnHurt(i);
            LeaveBodyOnDeath(i);
            camera.UpdateShake(deltaSeconds, _visualRandom.Range(-1f, 1f), _visualRandom.Range(-1f, 1f));
            var view = camera.ViewRect;
            player.SetView(
                camera.ScreenToWorld(new Vector2(view.Left, view.Top)),
                camera.ScreenToWorld(new Vector2(view.Right, view.Bottom)));
        }

        _shots.Update(deltaSeconds);
        _explosives.Update(deltaSeconds, _players);
        _hazards.Update(deltaSeconds, _players);
        _mines.Update(deltaSeconds, _players);
        _flames.Update(deltaSeconds);
        UpdateDoorGlows(deltaSeconds);
        _particles.Update(deltaSeconds);
        _corpses.Update(deltaSeconds);
        _drops.Update(deltaSeconds, _players);

        LayOutViews();
        if (!AnyPlayerAlive())
        {
            // Not over at once: "YOU DIED" first, with the world still going, then the results.
            _deathSeconds = 0f;
            _audio.PlaySfx(SfxIds.GameOver);
        }
    }

    // After the players are gone the arena carries on: zombies wander, fire burns out, mines
    // and barrels still go off. Nothing is scored any more and no new zombies come in.
    private void UpdateWorldWithoutPlayers(float deltaSeconds)
    {
        _enemies.Update(deltaSeconds, _players, _arena);
        _bosses.Update(deltaSeconds, _players, _arena);
        _fireballs.Update(deltaSeconds, _players, _arena);
        _shots.Update(deltaSeconds);
        _explosives.Update(deltaSeconds, _players);
        _hazards.Update(deltaSeconds, _players);
        _mines.Update(deltaSeconds, _players);
        _flames.Update(deltaSeconds);
        UpdateDoorGlows(deltaSeconds);
        _particles.Update(deltaSeconds);
        _corpses.Update(deltaSeconds);
        for (var i = 0; i < _cameras.Count; i++)
        {
            _cameras[i].UpdateShake(deltaSeconds, _visualRandom.Range(-1f, 1f), _visualRandom.Range(-1f, 1f));
        }
    }

    // "YOU DIED" grows into place over a reddening view, and gives way to the results screen.
    private void DrawDeath(IRenderer renderer)
    {
        var shown = Ease.OutCubic(_deathSeconds / DeathTextFadeSeconds);
        var results = IsGameOver ? Ease.OutCubic(_gameOverSeconds / GameOverFadeSeconds) : 0f;
        var width = _data.Game.VirtualWidth;
        var height = _data.Game.VirtualHeight;
        renderer.FillRect(Vector2.Zero, new Vector2(width, height), DeathTintColor * (DeathTintOpacity * shown));
        var text = _players.Count > 1 ? "YOU ALL DIED" : "YOU DIED";
        var size = MathHelper.Lerp(DeathTextSize * DeathTextStartScale, DeathTextSize, shown);
        renderer.DrawTextCentered(
            text, new Vector2(width / 2f, height * 0.45f), size, DeathTextColor * (shown * (1f - results)));
    }

    /// <summary>Index of a player whose controller is unplugged, or -1; the game is paused while there is one.</summary>
    public int DisconnectedPlayer { get; private set; } = -1;

    // Polls the inputs while paused so a controller plugged back in is noticed.
    private bool UpdateDisconnected()
    {
        if (DisconnectedPlayer >= 0)
        {
            var input = _players[DisconnectedPlayer].Input;
            input.Update(_players[DisconnectedPlayer].Position, _cameras[DisconnectedPlayer]);
            if (input.IsConnected)
            {
                DisconnectedPlayer = -1;
            }
        }

        for (var i = 0; i < _players.Count && DisconnectedPlayer < 0; i++)
        {
            if (!_players[i].Input.IsConnected)
            {
                DisconnectedPlayer = i;
                _disconnectedNotice = $"PLAYER {i + 1}: CONTROLLER DISCONNECTED";
            }
        }

        return DisconnectedPlayer >= 0;
    }

    /// <summary>True while the pause menu is open; the world is frozen.</summary>
    public bool IsPaused => _pausedBy >= 0;

    /// <summary>True while the waves-survived screen waits for the players' choice.</summary>
    public bool IsShowingVictory => _victoryOpen;

    /// <summary>Set when the pause menu asked for the settings; the game shows them, then comes back.</summary>
    public bool SettingsRequested { get; private set; }

    /// <summary>Set when the pause menu asked to quit to the main menu.</summary>
    public bool QuitRequested { get; private set; }

    /// <summary>Call when coming back from the settings; the run stays paused.</summary>
    public void ReturnFromSettings()
    {
        SettingsRequested = false;
    }

    // Either player can pause (Esc / Start); the inputs were read last frame.
    private bool TryPause()
    {
        // The key that resumed is still marked as pressed until the inputs are read again.
        if (_justResumed)
        {
            _justResumed = false;
            return false;
        }

        for (var i = 0; i < _players.Count; i++)
        {
            if (_players[i].Input.PausePressed)
            {
                _pausedBy = i;
                _pauseOverlay.Open();
                _audio.PlaySfx(SfxIds.Pause);
                return true;
            }
        }

        return false;
    }

    // The player who paused drives the menu.
    private void UpdatePause()
    {
        if (_pausedBy < 0)
        {
            return;
        }

        var player = _players[_pausedBy];
        player.Input.Update(player.Position, _cameras[_pausedBy]);
        var before = _pauseOverlay.Selected;
        switch (_pauseOverlay.Update(player.Input))
        {
            case PauseChoice.Resume:
                _pausedBy = -1;
                _justResumed = true;
                _audio.PlaySfx(SfxIds.Resume);
                break;

            case PauseChoice.Settings:
                SettingsRequested = true;
                _audio.PlaySfx(SfxIds.UiConfirm);
                break;

            case PauseChoice.QuitToMenu:
                QuitRequested = true;
                _audio.PlaySfx(SfxIds.UiConfirm);
                break;

            default:
                if (_pauseOverlay.Selected != before)
                {
                    _audio.PlaySfx(SfxIds.UiMove);
                }

                break;
        }
    }

    // Player 1 answers for the team: carry on with ever harder waves, or end the run in triumph.
    private void UpdateVictory()
    {
        var playerOne = _players[0];
        playerOne.Input.Update(playerOne.Position, _cameras[0]);
        var before = _victoryOverlay.Selected;
        var choice = _victoryOverlay.Update(playerOne.Input);
        if (choice is null)
        {
            if (_victoryOverlay.Selected != before)
            {
                _audio.PlaySfx(SfxIds.UiMove);
            }

            return;
        }

        _victoryOpen = false;
        _audio.PlaySfx(SfxIds.UiConfirm);
        if (choice == false)
        {
            EndRun($"{_waves!.Wave - 1} WAVES SURVIVED");
        }
    }

    private void EndRun(string title)
    {
        IsGameOver = true;
        _gameOverSeconds = 0f;
        _gameOverOverlay.Title = title;
        _gameOverOverlay.SetStats(_waves?.Wave ?? 0, _enemies.KillCount, _elapsedSeconds, _score.Total, _rules.Mode.Name);
        _audio.PlayMusic(MusicIds.GameOver);
        if (_highScores is not null && _highScores.Qualifies(_rules.Mode.Key, _score.Total))
        {
            _initials = new InitialsEntry();
            _audio.PlaySfx(SfxIds.NewHighScore);
        }
    }

    private void UpdateGameOver()
    {
        // A new high score: player 1 enters the initials first (for the team in co-op).
        if (_initials is { IsDone: false })
        {
            var playerOne = _players[0];
            playerOne.Input.Update(playerOne.Position, _cameras[0]);
            var changesBefore = _initials.Changes;
            _initials.Update(playerOne.Input);
            if (_initials.IsSkipped)
            {
                _audio.PlaySfx(SfxIds.UiBack);
            }
            else if (_initials.IsDone)
            {
                RecordHighScore(_initials.Initials);
                _audio.PlaySfx(SfxIds.UiConfirm);
            }
            else if (_initials.Changes != changesBefore)
            {
                _audio.PlaySfx(SfxIds.UiMove);
            }

            return;
        }

        for (var i = 0; i < _players.Count; i++)
        {
            var input = _players[i].Input;
            input.Update(_players[i].Position, _cameras[i]);
            if (input.ConfirmPressed)
            {
                RestartRequested = true;
            }
        }
    }

    // Co-op: a downed player stays out for the rest of the wave. If the partner clears it,
    // the downed player comes back beside them with part of their HP and only the starting
    // weapon; weapon levels and upgrades are kept.
    private void ReviveDownedPlayers()
    {
        var partner = FirstLivingPlayer();
        if (partner is null)
        {
            return;
        }

        for (var i = 0; i < _players.Count; i++)
        {
            var player = _players[i];
            if (player.IsAlive)
            {
                continue;
            }

            var spot = _arena.ResolveMovement(partner.Position + new Vector2(PlayerSpawnSpacing, 0f), player.Radius);
            player.Revive(spot, _data.Game.Coop.ReviveHpFraction);
            _unlocks.Grant(player, _startingWeapon);
            LayOutViews();
            _cameras[i].SnapTo(player.Position, _cameras[i].ViewCenter);
        }
    }

    // With two players the screen is split; while only one is alive, their view takes the
    // whole screen.
    private void LayOutViews()
    {
        if (_players.Count < 2)
        {
            return;
        }

        var survivor = -1;
        var alive = 0;
        for (var i = 0; i < _players.Count; i++)
        {
            if (_players[i].IsAlive)
            {
                alive++;
                survivor = i;
            }
        }

        for (var i = 0; i < _players.Count; i++)
        {
            var alone = alive == 1;
            _viewShown[i] = !alone || i == survivor;
            _cameras[i].ViewRect = alone && i == survivor ? _fullView : _splitViews[i];
        }
    }

    private Player? FirstLivingPlayer()
    {
        for (var i = 0; i < _players.Count; i++)
        {
            if (_players[i].IsAlive)
            {
                return _players[i];
            }
        }

        return null;
    }

    // Each view shakes with the blasts near its own player, more for bigger and closer ones.
    private void OnExplosion(Vector2 center, float radius)
    {
        Scorches.AddBlast(center, radius);
        if (_settings is { ScreenShake: false })
        {
            return;
        }

        for (var i = 0; i < _players.Count; i++)
        {
            var distance = Vector2.Distance(_players[i].Position, center);
            var nearness = Math.Clamp(1f - distance / ShakeReach, 0f, 1f);
            _cameras[i].AddTrauma(Math.Clamp(radius / ShakeRadiusForFull, 0.15f, 1f) * nearness * ExplosionShake);
        }
    }

    // A knock and a red flash on the view of a player who was just hit.
    private void ShakeOnHurt(int index)
    {
        while (_lastHp.Count <= index)
        {
            _lastHp.Add(_players[_lastHp.Count].Hp);
        }

        while (_hurtFlashLeft.Count <= index)
        {
            _hurtFlashLeft.Add(0f);
        }

        // Fire nibbles a little every frame; only a real hit (a chunk of HP at once) counts.
        var hp = _players[index].Hp;
        if (_lastHp[index] - hp >= HurtFeedbackMinDamage)
        {
            _hurtFlashLeft[index] = HurtFlashSeconds;
            if (_settings is not { ScreenShake: false })
            {
                _cameras[index].AddTrauma(HurtShake);
            }
        }

        _lastHp[index] = hp;
    }

    private void RecordHighScore(string initials)
    {
        var seconds = (int)_elapsedSeconds;
        _newEntryPlace = _highScores!.Insert(_rules.Mode.Key, new HighScoreEntry
        {
            Score = _score.Total,
            Wave = _waves?.Wave ?? 0,
            Kills = _enemies.KillCount,
            Seconds = seconds,
            Players = _players.Count,
            Date = DateTime.Now.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            Initials = initials,
        });
        _highScoreStore?.Save(_highScores);
    }

    private bool AnyPlayerAlive()
    {
        for (var i = 0; i < _players.Count; i++)
        {
            if (_players[i].IsAlive)
            {
                return true;
            }
        }

        return false;
    }

    // When a weapon's unlock wave begins, the weapon joins the crate pool and one crate
    // holding it appears in the middle of the arena.
    private void UnlockWeaponsForStartedWaves()
    {
        var wave = _waves?.HighestStartedWave ?? 0;
        if (wave <= _unlocksAppliedUpToWave)
        {
            return;
        }

        _unlocksAppliedUpToWave = wave;

        var placed = 0;
        var weapons = _data.Weapons;
        for (var i = 0; i < weapons.Count; i++)
        {
            if (_rules.UnlockWave(weapons[i].UnlockWave) > wave || !_unlocks.Unlock(weapons[i]))
            {
                continue;
            }

            var position = _arena.Center + new Vector2(placed * WeaponCrateSpacing, 0f);
            _drops.SpawnWeaponCrate(weapons[i], _arena.PlaceOnFloor(position, Crate.HalfSize));
            placed++;
        }

        if (placed > 0)
        {
            _audio.PlaySfx(SfxIds.WeaponUnlocked);
        }
    }

    private void RewardKills()
    {
        var deaths = _enemies.Deaths;
        for (var i = 0; i < deaths.Count; i++)
        {
            LeaveBody(deaths[i]);
            _xp.Award(deaths[i].Config.Xp, deaths[i].KilledBy, _players);
            _score.AddKill(deaths[i].Config.ScoreValue);
            _drops.OnEnemyKilled(deaths[i], _players);
            if (deaths[i].Config.IsBoss)
            {
                DropBossReward(deaths[i].Position);
                _bosses.OnBossDied(deaths[i], _arena);
            }
        }
    }

    // A boss always leaves one health, one ammo and one upgrade crate.
    private void DropBossReward(Vector2 position)
    {
        _drops.Spawn(CrateType.Health, _arena.PlaceOnFloor(position + new Vector2(-BossCrateSpacing, 0f), Crate.HalfSize));
        _drops.Spawn(CrateType.Ammo, _arena.PlaceOnFloor(position, Crate.HalfSize));
        _drops.Spawn(CrateType.Upgrade, _arena.PlaceOnFloor(position + new Vector2(BossCrateSpacing, 0f), Crate.HalfSize));
    }

    /// <summary>Debug helper: unlocks every weapon and gives it to every player.</summary>
    public void DebugGrantAllWeapons()
    {
        GiveEveryWeapon();
    }

    // Nightmare starts with this; the debug key uses it too.
    private void GiveEveryWeapon()
    {
        var weapons = _data.Weapons;
        for (var i = 0; i < weapons.Count; i++)
        {
            _unlocks.Unlock(weapons[i]);
            for (var p = 0; p < _players.Count; p++)
            {
                _unlocks.Grant(_players[p], weapons[i]);
            }
        }
    }

    /// <summary>Debug helper: every weapon of every player (carried or not) to the top level.</summary>
    public void DebugMaxAllWeapons()
    {
        var weapons = _data.Weapons;
        for (var i = 0; i < _players.Count; i++)
        {
            for (var w = 0; w < weapons.Count; w++)
            {
                _players[i].SetWeaponLevel(weapons[w].Id, WeaponConfig.MaxLevel);
            }
        }
    }

    /// <summary>Debug helper: raises the level of every player's gun in hand and active equipment by one.</summary>
    public void DebugLevelUpCurrentWeapons()
    {
        for (var i = 0; i < _players.Count; i++)
        {
            var player = _players[i];
            if (player.CurrentWeapon is not null)
            {
                player.LevelUpWeapon(player.CurrentWeapon.Config.Id);
            }

            if (player.ActiveEquipment is not null)
            {
                player.LevelUpWeapon(player.ActiveEquipment.Config.Id);
            }
        }
    }

    /// <summary>Debug helper: switches one-shot kills on or off.</summary>
    public void DebugToggleOneShotKills()
    {
        _shots.OneShotKills = !_shots.OneShotKills;
        _explosives.OneShotKills = _shots.OneShotKills;
        _flames.OneShotKills = _shots.OneShotKills;
    }

    /// <summary>True while a player's view shows the red hurt flash.</summary>
    public bool IsHurtFlashing(int playerIndex)
    {
        return playerIndex < _hurtFlashLeft.Count && _hurtFlashLeft[playerIndex] > 0f;
    }

    /// <summary>How shaken a player's view is now (0–1).</summary>
    public float ViewTrauma(int playerIndex)
    {
        return _cameras[playerIndex].Trauma;
    }

    /// <summary>True while a new high score's initials are being entered.</summary>
    public bool IsEnteringInitials => _initials is { IsDone: false };

    /// <summary>The run's score so far (a team score in co-op).</summary>
    public int Score => _score.Total;

    /// <summary>The players of this run, in order (player 1 first).</summary>
    public IReadOnlyList<Player> Players => _players;

    /// <summary>The screen area showing a player's view; empty while it is hidden (that player is down).</summary>
    public Rectangle GetView(int playerIndex)
    {
        return _viewShown[playerIndex] ? _cameras[playerIndex].ViewRect : Rectangle.Empty;
    }

    /// <summary>Debug helper: shows a short message under the wave title for a moment.</summary>
    public void ShowDebugNotice(string text)
    {
        _debugNotice = text;
        _debugNoticeLeft = DebugNoticeSeconds;
    }

    /// <summary>Debug helper: drops one crate of each type in a row beside the first player.</summary>
    public void DebugDropCrates()
    {
        var origin = _players[0].Position + new Vector2(90f, -70f);
        _drops.Spawn(CrateType.Ammo, _arena.PlaceOnFloor(origin, Crate.HalfSize));
        _drops.Spawn(CrateType.Health, _arena.PlaceOnFloor(origin + new Vector2(0f, 70f), Crate.HalfSize));
        _drops.Spawn(CrateType.Upgrade, _arena.PlaceOnFloor(origin + new Vector2(0f, 140f), Crate.HalfSize));
    }

    /// <summary>Debug helper: gives every player XP.</summary>
    public void DebugGrantXp(int amount)
    {
        for (var i = 0; i < _players.Count; i++)
        {
            _players[i].Progression.AddXp(amount, _data.Game.Xp);
        }
    }

    /// <summary>Debug helper: spawns enemies of the given type at random doors.</summary>
    public void DebugSpawnEnemies(string enemyId, int count)
    {
        var config = _data.GetEnemy(enemyId);
        if (config.IsBoss)
        {
            // The boss system drives one boss at a time.
            if (_enemies.FindBoss() is not null)
            {
                ShowDebugNotice("A boss is already in the arena");
                return;
            }

            count = 1;
        }

        for (var i = 0; i < count; i++)
        {
            var door = _arena.Doors[_random.NextIndex(_arena.Doors.Count)];
            _enemies.Spawn(config, _arena.GetDoorSpawnPoint(door, config.Radius, _random.Range(-1f, 1f)));
        }
    }

    public void Draw(IRenderer renderer)
    {
        for (var i = 0; i < _cameras.Count; i++)
        {
            if (!_viewShown[i])
            {
                continue;
            }

            renderer.BeginWorld(_cameras[i]);
            DrawArena(renderer);
            Scorches.Draw(renderer);
            _corpses.Draw(renderer);
            _particles.DrawFloor(renderer);
            _hazards.Draw(renderer);
            _mines.Draw(renderer);
            for (var t = 0; t < _practiceTargets.Count; t++)
            {
                var target = _practiceTargets[t];
                if (_data.Game.Debug.PracticeTargetModel is { } dummy)
                {
                    // A training dummy facing the camera, wobbling when hit.
                    renderer.DrawModel(
                        new ModelPose(
                            dummy, target.Position, MathHelper.PiOver2, MathHelper.PiOver2, 0f, 0f, target.Visual.Color,
                            Flinch: target.IsFlashing ? 1f : 0f, ShadowRadius: target.Radius),
                        target.Visual);
                }
                else
                {
                    renderer.DrawVisual(target.Visual, target.Position, 0f);
                }
            }

            _drops.Draw(renderer);
            _enemies.DrawTelegraphs(renderer);
            _bosses.DrawMarkers(renderer);
            _enemies.Draw(renderer);
            _bosses.DrawEffects(renderer);
            _fireballs.Draw(renderer);
            _shots.Draw(renderer);
            _flames.Draw(renderer);
            _explosives.Draw(renderer);
            _particles.Draw(renderer);
            DrawPlayers(renderer);
            renderer.End();
        }

        renderer.BeginScreen();
        for (var i = 1; i < _cameras.Count; i++)
        {
            if (!_viewShown[i] || !_viewShown[i - 1])
            {
                continue;
            }

            var view = _cameras[i].ViewRect;
            renderer.FillRect(
                new Vector2(view.X - SplitLineThickness / 2f, view.Y), new Vector2(SplitLineThickness, view.Height),
                SplitLineColor);
        }

        for (var i = 0; i < _players.Count; i++)
        {
            if (!_viewShown[i])
            {
                // The partner's view shows that this player is out for now.
                var view = _fullView;
                renderer.DrawText(
                    _downedNotices[i], new Vector2(view.Width / 2f, DownedNoticeY), DownedNoticeSize, DownedNoticeColor,
                    TextAlign.Center);
                continue;
            }

            DrawDoorIndicators(renderer, _cameras[i]);
            DrawBossIndicator(renderer, _cameras[i]);
            DrawWeaponCrateIndicators(renderer, _cameras[i]);
            DrawHurtFlash(renderer, i);
            DrawLethalWarning(renderer, i);
            _playerHuds[i].Draw(renderer, _players[i], _cameras[i].ViewRect);
        }

        if (_waves is not null)
        {
            _waveHud.Draw(renderer, _waves, _data.Game.VirtualWidth);
        }

        if (_bosses is { Boss: { } boss, Config: { } bossConfig })
        {
            _bossHud.Draw(renderer, bossConfig.DisplayName, boss, _data.Game.VirtualWidth);
        }

        _scoreHud.Draw(renderer, _score.Total, _modeLabel, _data.Game.VirtualWidth);

        if (_debugNoticeLeft > 0f)
        {
            renderer.DrawText(
                _debugNotice, new Vector2(_data.Game.VirtualWidth / 2f, DebugNoticeY), DebugNoticeSize,
                DebugNoticeColor * Math.Min(1f, _debugNoticeLeft), TextAlign.Center);
        }

        if ((_upgrades.AnyOfferOpen || _upgradeScreen.IsAnimating) && !IsGameOver)
        {
            _upgradeScreen.Draw(renderer, _players, _cameras);
        }

        // The crosshair doubles as the menu pointer, so it goes on top of the cards.
        for (var i = 0; i < _players.Count; i++)
        {
            var input = _players[i].Input;
            if (input.HasPointer && !IsGameOver && !IsDying)
            {
                DrawCrosshair(renderer, input.AimScreen);
            }
        }

        if (_deathSeconds >= 0f)
        {
            DrawDeath(renderer);
        }

        if (IsGameOver)
        {
            _gameOverOverlay.Draw(
                renderer, _data.Game.VirtualWidth, _data.Game.VirtualHeight, _initials,
                _highScores?.Get(_rules.Mode.Key), _highScores?.Version ?? 0, _newEntryPlace,
                Ease.OutCubic(_gameOverSeconds / GameOverFadeSeconds));
        }
        else if (_victoryOpen)
        {
            _victoryOverlay.Draw(renderer);
        }
        else if (IsPaused)
        {
            _pauseOverlay.Draw(renderer);
        }
        else if (DisconnectedPlayer >= 0)
        {
            DrawDisconnected(renderer);
        }

        renderer.End();
    }

    // Points toward weapon crates that are outside a view, so a new weapon is found quickly.
    private void DrawWeaponCrateIndicators(IRenderer renderer, Camera2D camera)
    {
        var view = camera.ViewRect;
        for (var i = 0; i < _drops.Count; i++)
        {
            var crate = _drops[i];
            if (crate.Type != CrateType.Weapon)
            {
                continue;
            }

            var screen = camera.WorldToScreen(crate.Position);
            if (screen.X >= view.Left && screen.X <= view.Right && screen.Y >= view.Top && screen.Y <= view.Bottom)
            {
                continue;
            }

            var marker = new Vector2(
                Math.Clamp(screen.X, view.Left + EdgeIndicatorInset, view.Right - EdgeIndicatorInset),
                Math.Clamp(screen.Y, view.Top + EdgeIndicatorInset, view.Bottom - EdgeIndicatorInset));
            var half = new Vector2(EdgeIndicatorRadius * 0.8f);
            renderer.FillRect(marker - half - new Vector2(3f), half * 2f + new Vector2(6f), EdgeIndicatorCoreColor);
            renderer.FillRect(marker - half, half * 2f, WeaponCrateIndicatorColor);
        }
    }

    // Points toward glowing doors that are outside a view, so a spawn warning is never
    // missed just because the door is off screen.
    private void DrawDoorIndicators(IRenderer renderer, Camera2D camera)
    {
        if (_waves is null)
        {
            return;
        }

        var view = camera.ViewRect;
        var doors = _arena.Doors;
        for (var i = 0; i < doors.Count; i++)
        {
            var glow = _waves.Spawner.GetDoorGlow(i);
            if (glow <= 0f)
            {
                continue;
            }

            var screen = camera.WorldToScreen(doors[i].Center);
            if (screen.X >= view.Left && screen.X <= view.Right && screen.Y >= view.Top && screen.Y <= view.Bottom)
            {
                continue;
            }

            var marker = new Vector2(
                Math.Clamp(screen.X, view.Left + EdgeIndicatorInset, view.Right - EdgeIndicatorInset),
                Math.Clamp(screen.Y, view.Top + EdgeIndicatorInset, view.Bottom - EdgeIndicatorInset));
            var radius = EdgeIndicatorRadius * (0.6f + 0.4f * glow);
            renderer.FillCircle(marker, radius, GateGlowColor);
            renderer.FillCircle(marker, radius * 0.45f, EdgeIndicatorCoreColor);
        }
    }

    // Debug dummies near the spawn point: one still, one patrolling left and right.
    private void AddPracticeTargets(float radius)
    {
        var still = new PracticeTarget(_arena.Center + new Vector2(350f, -200f), radius);
        var moving = new PracticeTarget(
            _arena.Center + new Vector2(100f, 250f), _arena.Center + new Vector2(600f, 250f), 180f, radius);

        _practiceTargets.Add(still);
        _practiceTargets.Add(moving);
        _shots.AddTarget(still);
        _shots.AddTarget(moving);
        _explosives.AddTarget(still);
        _hazards.AddTarget(still);
        _hazards.AddTarget(moving);
        _mines.AddTarget(still);
        _mines.AddTarget(moving);
        _flames.AddTarget(still);
        _flames.AddTarget(moving);
        _explosives.AddTarget(moving);
    }

    private void AddPlayer(Player player, Rectangle viewRect, CameraConfig cameraConfig)
    {
        var camera = new Camera2D(viewRect, cameraConfig);
        var wall = new Vector2(_arena.WallThickness);
        camera.SetBounds(
            new Vector2(_arena.Left, _arena.Top) - wall,
            new Vector2(_arena.Right, _arena.Bottom) + wall);
        camera.SnapTo(player.Position, camera.ViewCenter);

        _players.Add(player);
        _cameras.Add(camera);
        _splitViews.Add(viewRect);
        _viewShown.Add(true);
        _downedNotices.Add($"PLAYER {player.Index + 1} IS DOWN  ·  BACK AFTER THIS WAVE");
        _playerHuds.Add(new PlayerHud(_data.Game.Xp));
    }

    private void DrawPlayers(IRenderer renderer)
    {
        for (var i = 0; i < _players.Count; i++)
        {
            var player = _players[i];
            if (!player.IsAlive)
            {
                continue;
            }

            if (renderer.IsThreeD && _data.Game.Player.Model is { } model)
            {
                // The body faces the aim; the gun is in its hands.
                var aim = MathF.Atan2(player.AimDirection.Y, player.AimDirection.X);
                var weapon = player.CurrentWeapon;
                renderer.DrawModel(
                    new ModelPose(
                        model, player.Position, aim, aim, player.WalkDistance, player.Speed, player.Visual.Color,
                        Recoil: player.RecoilAmount, Flinch: player.FlinchAmount,
                        GunId: weapon?.Config.Model ?? _data.Game.Player.DefaultGunModel,
                        ShadowRadius: player.Radius,
                        Hold: HoldFor(weapon), MoveHeading: player.MoveHeading),
                    player.Visual);
                continue;
            }

            renderer.DrawVisual(player.Visual, player.Position, 0f);

            var muzzle = player.Position + player.AimDirection * player.Radius;
            var barrel = player.AimDirection * FacingLineLength;
            if (player.CurrentWeapon is { Stats.DualWield: true })
            {
                // Two barrels for a dual-wielded weapon.
                var side = new Vector2(-player.AimDirection.Y, player.AimDirection.X) * DualBarrelOffset;
                renderer.DrawBar(muzzle + side, muzzle + side + barrel, DualBarrelThickness, GunBottom, GunTop, FacingColor);
                renderer.DrawBar(muzzle - side, muzzle - side + barrel, DualBarrelThickness, GunBottom, GunTop, FacingColor);
            }
            else
            {
                renderer.DrawBar(muzzle, muzzle + barrel, FacingLineThickness, GunBottom, GunTop, FacingColor);
            }
        }
    }

    // The floor grid shows the cells barrels snap to, so a player can see where one will go.
    private static float FloorCellSize(GameData data)
    {
        var weapons = data.Weapons;
        for (var i = 0; i < weapons.Count; i++)
        {
            var placed = weapons[i].Placed;
            if (placed is not null && placed.Solid && placed.GridSize > 0f)
            {
                return placed.GridSize;
            }
        }

        return GridSpacing;
    }

    private void DrawArena(IRenderer renderer)
    {
        var wall = new Vector2(_arena.WallThickness);
        var floorTopLeft = new Vector2(_arena.Left, _arena.Top);
        var floorSize = new Vector2(_arena.Size);

        // Four walls round the floor (boxes in 3D, flat in 2D), then the floor.
        var outer = floorSize + wall * 2f;
        renderer.FillBox(floorTopLeft - wall, new Vector2(outer.X, wall.Y), WallHeight, WallColor);
        renderer.FillBox(floorTopLeft + new Vector2(-wall.X, floorSize.Y), new Vector2(outer.X, wall.Y), WallHeight, WallColor);
        renderer.FillBox(floorTopLeft - new Vector2(wall.X, 0f), new Vector2(wall.X, floorSize.Y), WallHeight, WallColor);
        renderer.FillBox(floorTopLeft + new Vector2(floorSize.X, 0f), new Vector2(wall.X, floorSize.Y), WallHeight, WallColor);
        renderer.FillRect(floorTopLeft, floorSize, FloorColor);

        // Floor grid: gives the eye a reference for camera and player movement.
        // (Blood stains go on after it, so they darken the grid lines too.)
        for (var offset = _floorCellSize; offset < _arena.Size; offset += _floorCellSize)
        {
            renderer.FillRect(
                new Vector2(_arena.Left + offset - GridLineThickness / 2f, _arena.Top),
                new Vector2(GridLineThickness, _arena.Size),
                GridColor);
            renderer.FillRect(
                new Vector2(_arena.Left, _arena.Top + offset - GridLineThickness / 2f),
                new Vector2(_arena.Size, GridLineThickness),
                GridColor);
        }

        renderer.DrawFloorStains(floorTopLeft, floorSize);

        var pillars = _arena.Pillars;
        for (var i = 0; i < pillars.Count; i++)
        {
            renderer.FillBox(pillars[i].Center - pillars[i].HalfSize, pillars[i].HalfSize * 2f, PillarHeight, WallColor);
        }

        var doors = _arena.Doors;
        for (var i = 0; i < doors.Count; i++)
        {
            var glow = _doorGlow[i];
            if (glow > 0f && !renderer.IsThreeD)
            {
                DrawDoorHalo(renderer, doors[i], glow);
            }

            // A gate block slightly proud of the wall, so it shows in front of it in 3D; set into
            // the wall, it casts no shadow of its own. In 3D it throbs while it warns.
            var shown = renderer.IsThreeD && glow > 0f ? glow * DoorPulse(glow) : glow;
            renderer.FillBox(
                doors[i].TopLeft - new Vector2(GateInset), doors[i].Size + new Vector2(GateInset * 2f), WallHeight + GateInset,
                Color.Lerp(GateColor, GateGlowColor, shown), castShadow: false);
            if (renderer.IsThreeD && glow > 0f)
            {
                DrawDoorWarning3D(renderer, doors[i], glow);
            }
        }
    }

    // 0.55–1: a throb that quickens as the door's enemies get closer.
    private float DoorPulse(float glow)
    {
        var speed = MathHelper.Lerp(DoorPulseSlow, DoorPulseFast, glow);
        return 0.775f + 0.225f * MathF.Sin(_doorPulseTime * speed);
    }

    // In 3D: light spilled onto the floor, fading smoothly away from the door, a curtain of
    // light standing in the opening and lamps on both gate posts, all throbbing with the gate.
    private void DrawDoorWarning3D(IRenderer renderer, Door door, float glow)
    {
        var strength = glow * DoorPulse(glow);
        DoorInnerEdge(door, out var from, out var to);
        DrawDoorFloorLight(renderer, door, strength);
        renderer.DrawGlowWall(from, to, WallHeight * (0.4f + 0.6f * glow), GateGlowColor * (DoorCurtainOpacity * strength));
        var lampHeight = WallHeight + GateInset + DoorLampRadius * 0.5f;
        var lampRadius = DoorLampRadius * (0.6f + 0.6f * strength);
        renderer.FillCircleRaised(from, lampHeight, lampRadius * 2f, GateGlowColor * (0.25f * strength));
        renderer.FillCircleRaised(to, lampHeight, lampRadius * 2f, GateGlowColor * (0.25f * strength));
        renderer.FillCircleRaised(from, lampHeight, lampRadius, GateGlowColor * strength);
        renderer.FillCircleRaised(to, lampHeight, lampRadius, GateGlowColor * strength);
    }

    // Side by side bands, each dimmer than the last, so the light fades without visible layers.
    private static void DrawDoorFloorLight(IRenderer renderer, Door door, float strength)
    {
        var band = DoorFloorLightDepth / DoorFloorLightBands;
        for (var i = 0; i < DoorFloorLightBands; i++)
        {
            var near = 1f - (float)i / DoorFloorLightBands;
            var bandTopLeft = DoorFloorLightTopLeft(door, band) + door.Inward * (band * i);
            renderer.FillRect(
                bandTopLeft, DoorFloorLightSize(door, band), GateGlowColor * (DoorHaloOpacity * strength * near * near));
        }
    }

    // A door's warning follows the spawner while it rises and fades out quickly once its
    // enemies are out; sparks drift out of it while it glows (3D look only).
    private void UpdateDoorGlows(float deltaSeconds)
    {
        _doorPulseTime += deltaSeconds;
        var doors = _arena.Doors;
        for (var i = 0; i < doors.Count; i++)
        {
            var target = _waves?.Spawner.GetDoorGlow(i) ?? 0f;
            _doorGlow[i] = target >= _doorGlow[i] ? target : Math.Max(target, _doorGlow[i] - deltaSeconds / DoorFadeSeconds);
        }

        if (_settings is not { ThreeDView: true })
        {
            return;
        }

        for (var i = 0; i < doors.Count; i++)
        {
            var glow = _doorGlow[i];
            if (glow <= 0f)
            {
                continue;
            }

            DoorInnerEdge(doors[i], out var from, out var to);
            _particles.DoorSparks(from, to, doors[i].Inward, WallHeight, glow, GateGlowColor, GateColor, deltaSeconds);
        }
    }

    // The two ends of a door's edge facing the arena.
    private static void DoorInnerEdge(Door door, out Vector2 from, out Vector2 to)
    {
        var half = door.Size / 2f;
        var depth = MathF.Abs(door.Inward.X) * half.X + MathF.Abs(door.Inward.Y) * half.Y;
        var width = MathF.Abs(door.Inward.Y) * half.X + MathF.Abs(door.Inward.X) * half.Y;
        var edge = door.Center + door.Inward * (depth + GateInset);
        var across = new Vector2(-door.Inward.Y, door.Inward.X) * width;
        from = edge - across;
        to = edge + across;
    }

    // A lethal boss attack is coming: the view's edges throb crimson and a skull marked LETHAL
    // floats over where it will land (kept inside the view when that is off screen).
    private void DrawLethalWarning(IRenderer renderer, int index)
    {
        if (!_bosses.IsLethalWarning || !_players[index].IsAlive)
        {
            return;
        }

        var camera = _cameras[index];
        var view = camera.ViewRect;
        var pulse = 0.6f + 0.4f * MathF.Sin(_bosses.Time * LethalPulseSpeed);
        var edge = LethalEdgeColor * (LethalEdgeOpacity * pulse);
        var thick = LethalEdgeThickness;
        renderer.FillRect(new Vector2(view.Left, view.Top), new Vector2(view.Width, thick), edge);
        renderer.FillRect(new Vector2(view.Left, view.Bottom - thick), new Vector2(view.Width, thick), edge);
        renderer.FillRect(new Vector2(view.Left, view.Top + thick), new Vector2(thick, view.Height - 2f * thick), edge);
        renderer.FillRect(new Vector2(view.Right - thick, view.Top + thick), new Vector2(thick, view.Height - 2f * thick), edge);

        var at = camera.WorldToScreen(_bosses.LethalPoint) - new Vector2(0f, SkullLift);
        at = new Vector2(
            Math.Clamp(at.X, view.Left + SkullMargin, view.Right - SkullMargin),
            Math.Clamp(at.Y, view.Top + SkullMargin, view.Bottom - SkullMargin - LethalTextSize));
        DrawSkull(renderer, at, SkullRadius * (0.92f + 0.08f * pulse), LethalSkullColor);
        renderer.DrawTextCentered(
            "LETHAL", at + new Vector2(0f, SkullRadius * 1.9f), LethalTextSize, LethalSkullColor * (0.7f + 0.3f * pulse));
    }

    // A simple skull: a round head, a narrower jaw with teeth gaps, two dark eyes and a nose.
    private static void DrawSkull(IRenderer renderer, Vector2 center, float radius, Color color)
    {
        renderer.FillCircle(center, radius, color);
        var jawSize = new Vector2(radius * 1.1f, radius * 0.55f);
        var jawTopLeft = center + new Vector2(-jawSize.X / 2f, radius * 0.55f);
        renderer.FillRect(jawTopLeft, jawSize, color);
        renderer.FillCircle(center + new Vector2(-radius * 0.38f, radius * 0.05f), radius * 0.27f, SkullHoleColor);
        renderer.FillCircle(center + new Vector2(radius * 0.38f, radius * 0.05f), radius * 0.27f, SkullHoleColor);
        renderer.FillCircle(center + new Vector2(0f, radius * 0.45f), radius * 0.1f, SkullHoleColor);
        for (var i = 1; i < 4; i++)
        {
            var x = jawTopLeft.X + jawSize.X * i / 4f;
            renderer.FillRect(new Vector2(x - 1.5f, jawTopLeft.Y + jawSize.Y * 0.35f), new Vector2(3f, jawSize.Y * 0.65f), SkullHoleColor);
        }
    }

    // Dual wielding fills both hands; otherwise the weapon says how it is held.
    private static ModelHold HoldFor(Weapon? weapon)
    {
        if (weapon is null)
        {
            return ModelHold.OneHand;
        }

        if (weapon.Stats.DualWield)
        {
            return ModelHold.Dual;
        }

        return weapon.Config.Hold switch
        {
            WeaponHold.OneHand => ModelHold.OneHand,
            WeaponHold.Shoulder => ModelHold.Shoulder,
            _ => ModelHold.TwoHands,
        };
    }

    // A zombie with a model leaves a body that topples over and later shrinks away.
    private void LeaveBody(in EnemyDeath death)
    {
        if (death.Config.Model is not { } model)
        {
            return;
        }

        var color = death.Config.Color;
        var tint = new Color(color[0], color[1], color[2]) * CorpseDarkening;
        _corpses.Add(
            model, death.Position, death.Facing, new Color(tint.R, tint.G, tint.B), death.Config.Radius,
            death.Config.IsBoss ? BossCorpseSeconds : EnemyCorpseSeconds);
    }

    // A player who just went down leaves a body where they fell.
    private void LeaveBodyOnDeath(int index)
    {
        while (_wasAlive.Count <= index)
        {
            _wasAlive.Add(true);
        }

        var player = _players[index];
        if (_wasAlive[index] && !player.IsAlive && _data.Game.Player.Model is { } model)
        {
            var aim = MathF.Atan2(player.AimDirection.Y, player.AimDirection.X);
            _corpses.Add(model, player.Position, aim, player.Visual.Color, player.Radius, PlayerCorpseSeconds);
        }

        _wasAlive[index] = player.IsAlive;
    }

    // Red bands along the edges of a view, fading out, when its player was just hurt.
    private void DrawHurtFlash(IRenderer renderer, int index)
    {
        if (index >= _hurtFlashLeft.Count || _hurtFlashLeft[index] <= 0f)
        {
            return;
        }

        var view = _cameras[index].ViewRect;
        var color = HurtFlashColor * (0.45f * _hurtFlashLeft[index] / HurtFlashSeconds);
        var thick = HurtFlashThickness;
        renderer.FillRect(new Vector2(view.Left, view.Top), new Vector2(view.Width, thick), color);
        renderer.FillRect(new Vector2(view.Left, view.Bottom - thick), new Vector2(view.Width, thick), color);
        renderer.FillRect(new Vector2(view.Left, view.Top + thick), new Vector2(thick, view.Height - 2f * thick), color);
        renderer.FillRect(
            new Vector2(view.Right - thick, view.Top + thick), new Vector2(thick, view.Height - 2f * thick), color);
    }

    // A red marker at the edge of a view points to a boss outside it.
    private void DrawBossIndicator(IRenderer renderer, Camera2D camera)
    {
        if (_bosses.Boss is not { } boss)
        {
            return;
        }

        var view = camera.ViewRect;
        var screen = camera.WorldToScreen(boss.Position);
        if (screen.X >= view.Left && screen.X <= view.Right && screen.Y >= view.Top && screen.Y <= view.Bottom)
        {
            return;
        }

        var marker = new Vector2(
            Math.Clamp(screen.X, view.Left + EdgeIndicatorInset, view.Right - EdgeIndicatorInset),
            Math.Clamp(screen.Y, view.Top + EdgeIndicatorInset, view.Bottom - EdgeIndicatorInset));
        renderer.FillCircle(marker, BossIndicatorRadius, BossIndicatorColor);
        renderer.FillCircle(marker, BossIndicatorRadius * 0.45f, EdgeIndicatorCoreColor);
    }

    private void DrawDisconnected(IRenderer renderer)
    {
        var width = _data.Game.VirtualWidth;
        var height = _data.Game.VirtualHeight;
        renderer.FillRect(Vector2.Zero, new Vector2(width, height), PauseDimColor);
        renderer.DrawText(
            _disconnectedNotice, new Vector2(width / 2f, height * 0.42f), PauseTitleSize, PauseTitleColor, TextAlign.Center);
        renderer.DrawText(
            "Connect a gamepad to continue", new Vector2(width / 2f, height * 0.42f + 80f), PauseTextSize, PauseTextColor,
            TextAlign.Center);
    }

    // Light spilling from a glowing door onto the floor in front of it.
    private static void DrawDoorHalo(IRenderer renderer, Door door, float glow)
    {
        renderer.FillRect(
            DoorFloorLightTopLeft(door, DoorHaloDepth), DoorFloorLightSize(door, DoorHaloDepth),
            GateGlowColor * (DoorHaloOpacity * glow));
    }

    // A strip of floor in front of a door, reaching this deep into the arena.
    private static Vector2 DoorFloorLightSize(Door door, float depth)
    {
        return door.Inward.X == 0f ? new Vector2(door.Size.X, depth) : new Vector2(depth, door.Size.Y);
    }

    private static Vector2 DoorFloorLightTopLeft(Door door, float depth)
    {
        if (door.Inward.X == 0f)
        {
            return door.Inward.Y > 0f
                ? door.TopLeft + new Vector2(0f, door.Size.Y)
                : door.TopLeft - new Vector2(0f, depth);
        }

        return door.Inward.X > 0f
            ? door.TopLeft + new Vector2(door.Size.X, 0f)
            : door.TopLeft - new Vector2(depth, 0f);
    }

    private static void DrawCrosshair(IRenderer renderer, Vector2 position)
    {
        renderer.DrawLine(
            position - new Vector2(CrosshairArm, 0f), position + new Vector2(CrosshairArm, 0f),
            CrosshairThickness, CrosshairColor);
        renderer.DrawLine(
            position - new Vector2(0f, CrosshairArm), position + new Vector2(0f, CrosshairArm),
            CrosshairThickness, CrosshairColor);
    }
}
