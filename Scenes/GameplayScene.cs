using System;
using System.Collections.Generic;
using Fourgate.Core.Audio;
using Fourgate.Core.Data;
using Fourgate.Core.Input;
using Fourgate.Core.Rendering;
using Fourgate.Core.Rng;
using Fourgate.Entities;
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
    private const float FacingLineLength = 14f;
    private const float FacingLineThickness = 6f;
    private const float DualBarrelOffset = 7f;
    private const float DualBarrelThickness = 5f;
    private const float CrosshairArm = 14f;
    private const float CrosshairThickness = 2f;
    private const float ReloadBarExtraWidth = 12f;
    private const float ReloadBarHeight = 6f;
    private const float ReloadBarGap = 10f;
    private const float DoorHaloDepth = 110f;
    private const float DoorHaloOpacity = 0.3f;
    private const float EdgeIndicatorInset = 40f;
    private const float EdgeIndicatorRadius = 18f;
    private const float WeaponCrateSpacing = 70f;
    private const float DebugNoticeSeconds = 2f;
    private const float PlayerSpawnSpacing = 90f;
    private const float SplitLineThickness = 4f;
    private const float DownedNoticeY = 200f;
    private const float DownedNoticeSize = 30f;
    private const float DebugNoticeY = 140f;
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
    private static readonly Color ReloadBarBackColor = new(12, 13, 18);
    private static readonly Color ReloadBarFillColor = new(255, 225, 120);
    private static readonly Color DebugNoticeColor = new(255, 225, 120);
    private static readonly Color SplitLineColor = new(10, 10, 14);
    private static readonly Color DownedNoticeColor = new(255, 120, 100);

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
    private string _debugNotice = string.Empty;
    private float _debugNoticeLeft;
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
    public GameplayScene(GameData data, IReadOnlyList<IPlayerInput> inputs, IAudioManager audio)
    {
        _data = data;
        _floorCellSize = FloorCellSize(data);
        _audio = audio;
        var config = data.Game;
        _random = new GameRandom(config.Debug.Seed ?? Environment.TickCount);
        _arena = new Arena(config.Arena);
        _enemies = new EnemySystem(audio) { KnockbackDamping = config.Knockback.Damping, Fireballs = _fireballs };
        _shots = new ShotSystem(_arena);
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
        _flames.AddSource(_enemies);
        _flames.AddSource(_mines);
        _flames.OneShotKills = config.Debug.OneShotKills;
        _shotRouter = new ShotRouter(_shots, _explosives, _mines, _flames);
        _unlocks = new WeaponUnlocks(config, audio, _random);
        _drops = new DropSystem(config.Crates, _random, audio, _unlocks);
        _upgrades = new UpgradeSystem(data.Upgrades, _unlocks, _random, audio);
        _upgradeScreen = new UpgradeScreen(_upgrades);
        _waves = config.Debug.DisableWaves ? null : new WaveSystem(data, _enemies, _arena, _random, audio);

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

        _lastWavePhase = _waves?.Phase;

        if (config.Debug.PracticeTargets)
        {
            AddPracticeTargets(config.Player.Radius);
        }
    }

    /// <summary>True once every player is dead; the simulation is frozen from then on.</summary>
    public bool IsGameOver { get; private set; }

    /// <summary>True when a player asked to start over from the game over screen.</summary>
    public bool RestartRequested { get; private set; }

    public void Update(float deltaSeconds)
    {
        _debugNoticeLeft = Math.Max(0f, _debugNoticeLeft - deltaSeconds);
        if (IsGameOver)
        {
            UpdateGameOver();
            return;
        }

        // Banked picks are spent between waves. The world stands still while anyone is choosing.
        var isResting = _waves is null || _waves.Phase == WavePhase.Rest;
        if (isResting && _upgrades.OpenOffers(_players))
        {
            _upgradeScreen.Update(deltaSeconds, _players, _cameras);
            return;
        }

        _elapsedSeconds += deltaSeconds;

        for (var i = 0; i < _practiceTargets.Count; i++)
        {
            _practiceTargets[i].Update(deltaSeconds);
        }

        _waves?.Update(deltaSeconds);
        if (_lastWavePhase == WavePhase.Active && _waves?.Phase == WavePhase.Rest)
        {
            ReviveDownedPlayers();
        }

        _lastWavePhase = _waves?.Phase;
        UnlockWeaponsForStartedWaves();
        _enemies.Update(deltaSeconds, _players, _arena);
        _fireballs.Update(deltaSeconds, _players, _arena);
        RewardKills();

        for (var i = 0; i < _players.Count; i++)
        {
            var player = _players[i];
            var camera = _cameras[i];

            player.Input.Update(player.Position, camera);
            player.Update(deltaSeconds, _arena, _shotRouter);
            camera.Follow(player.Position, player.Input.AimScreen, deltaSeconds);
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
        _drops.Update(deltaSeconds, _players);

        LayOutViews();
        if (!AnyPlayerAlive())
        {
            IsGameOver = true;
            _gameOverOverlay.SetStats(_waves?.Wave ?? 0, _enemies.KillCount, _elapsedSeconds);
            _audio.PlaySfx(SfxIds.GameOver);
        }
    }

    private void UpdateGameOver()
    {
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
            if (weapons[i].UnlockWave > wave || !_unlocks.Unlock(weapons[i]))
            {
                continue;
            }

            var position = _arena.Center + new Vector2(placed * WeaponCrateSpacing, 0f);
            _drops.SpawnWeaponCrate(weapons[i], _arena.PlaceOnFloor(position, Crate.HalfSize));
            placed++;
        }
    }

    private void RewardKills()
    {
        var deaths = _enemies.Deaths;
        for (var i = 0; i < deaths.Count; i++)
        {
            _xp.Award(deaths[i].Config.Xp, deaths[i].KilledBy, _players);
            _drops.OnEnemyKilled(deaths[i], _players);
        }
    }

    /// <summary>Debug helper: unlocks every weapon and gives it to every player.</summary>
    public void DebugGrantAllWeapons()
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
            _hazards.Draw(renderer);
            _mines.Draw(renderer);
            for (var t = 0; t < _practiceTargets.Count; t++)
            {
                renderer.DrawVisual(_practiceTargets[t].Visual, _practiceTargets[t].Position, 0f);
            }

            _drops.Draw(renderer);
            _enemies.DrawTelegraphs(renderer);
            _enemies.Draw(renderer);
            _fireballs.Draw(renderer);
            _shots.Draw(renderer);
            _flames.Draw(renderer);
            _explosives.Draw(renderer);
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
            DrawWeaponCrateIndicators(renderer, _cameras[i]);
            _playerHuds[i].Draw(renderer, _players[i], _cameras[i].ViewRect);
        }

        if (_waves is not null)
        {
            _waveHud.Draw(renderer, _waves, _data.Game.VirtualWidth);
        }

        if (_debugNoticeLeft > 0f)
        {
            renderer.DrawText(
                _debugNotice, new Vector2(_data.Game.VirtualWidth / 2f, DebugNoticeY), DebugNoticeSize,
                DebugNoticeColor * Math.Min(1f, _debugNoticeLeft), TextAlign.Center);
        }

        if (_upgrades.AnyOfferOpen && !IsGameOver)
        {
            _upgradeScreen.Draw(renderer, _players, _cameras);
        }

        // The crosshair doubles as the menu pointer, so it goes on top of the cards.
        for (var i = 0; i < _players.Count; i++)
        {
            var input = _players[i].Input;
            if (input.HasPointer && !IsGameOver)
            {
                DrawCrosshair(renderer, input.AimScreen);
            }
        }

        if (IsGameOver)
        {
            _gameOverOverlay.Draw(renderer, _data.Game.VirtualWidth, _data.Game.VirtualHeight);
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

            renderer.DrawVisual(player.Visual, player.Position, 0f);

            var muzzle = player.Position + player.AimDirection * player.Radius;
            var barrel = player.AimDirection * FacingLineLength;
            if (player.CurrentWeapon is { Stats.DualWield: true })
            {
                // Two barrels for a dual-wielded weapon.
                var side = new Vector2(-player.AimDirection.Y, player.AimDirection.X) * DualBarrelOffset;
                renderer.DrawLine(muzzle + side, muzzle + side + barrel, DualBarrelThickness, FacingColor);
                renderer.DrawLine(muzzle - side, muzzle - side + barrel, DualBarrelThickness, FacingColor);
            }
            else
            {
                renderer.DrawLine(muzzle, muzzle + barrel, FacingLineThickness, FacingColor);
            }

            var weapon = player.CurrentWeapon;
            if (weapon is { IsReloading: true })
            {
                DrawReloadBar(renderer, player, weapon.ReloadProgress);
            }
        }
    }

    private static void DrawReloadBar(IRenderer renderer, Player player, float progress)
    {
        var size = new Vector2(player.Radius * 2f + ReloadBarExtraWidth, ReloadBarHeight);
        var topLeft = player.Position + new Vector2(-size.X / 2f, player.Radius + ReloadBarGap);

        renderer.FillRect(topLeft, size, ReloadBarBackColor);
        renderer.FillRect(topLeft, new Vector2(size.X * progress, size.Y), ReloadBarFillColor);
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

        renderer.FillRect(floorTopLeft - wall, floorSize + wall * 2f, WallColor);
        renderer.FillRect(floorTopLeft, floorSize, FloorColor);

        // Floor grid: gives the eye a reference for camera and player movement.
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

        var pillars = _arena.Pillars;
        for (var i = 0; i < pillars.Count; i++)
        {
            renderer.FillRect(pillars[i].Center - pillars[i].HalfSize, pillars[i].HalfSize * 2f, WallColor);
        }

        var doors = _arena.Doors;
        for (var i = 0; i < doors.Count; i++)
        {
            var glow = _waves?.Spawner.GetDoorGlow(i) ?? 0f;
            if (glow > 0f)
            {
                DrawDoorHalo(renderer, doors[i], glow);
            }

            renderer.FillRect(doors[i].TopLeft, doors[i].Size, Color.Lerp(GateColor, GateGlowColor, glow));
        }
    }

    // Light spilling from a glowing door onto the floor in front of it.
    private static void DrawDoorHalo(IRenderer renderer, Door door, float glow)
    {
        Vector2 topLeft;
        Vector2 size;
        if (door.Inward.X == 0f)
        {
            size = new Vector2(door.Size.X, DoorHaloDepth);
            topLeft = door.Inward.Y > 0f
                ? door.TopLeft + new Vector2(0f, door.Size.Y)
                : door.TopLeft - new Vector2(0f, DoorHaloDepth);
        }
        else
        {
            size = new Vector2(DoorHaloDepth, door.Size.Y);
            topLeft = door.Inward.X > 0f
                ? door.TopLeft + new Vector2(door.Size.X, 0f)
                : door.TopLeft - new Vector2(DoorHaloDepth, 0f);
        }

        renderer.FillRect(topLeft, size, GateGlowColor * (DoorHaloOpacity * glow));
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
