using System;
using System.Collections.Generic;
using Fourgate.Core.Audio;
using Fourgate.Core.Data;
using Fourgate.Core.Rng;
using Fourgate.Entities;

namespace Fourgate.Systems;

/// <summary>Runs the wave loop: rest countdown, wave, all enemies dead, rest again.</summary>
public sealed class WaveSystem
{
    private readonly GameData _data;
    private readonly EnemySystem _enemies;
    private readonly GameRandom _random;
    private readonly IAudioManager _audio;
    private readonly List<EnemyConfig> _composition = new();

    public WaveSystem(GameData data, EnemySystem enemies, Arena arena, GameRandom random, IAudioManager audio)
    {
        _data = data;
        _enemies = enemies;
        _random = random;
        _audio = audio;

        Spawner = new SpawnSystem(data.Waves, data.Game.MaxAliveEnemies, enemies, arena, random, audio);
        Wave = data.Game.Debug.StartWave;
        Phase = WavePhase.Rest;
        RestSecondsLeft = data.Game.RestCountdownSeconds;
    }

    public SpawnSystem Spawner { get; }

    /// <summary>Multiplies every wave's enemy budget (co-op scaling); 1 for one player.</summary>
    public float BudgetMultiplier { get; set; } = 1f;

    /// <summary>Every enemy planned for the current wave, spawned or not.</summary>
    public IReadOnlyList<EnemyConfig> Composition => _composition;

    /// <summary>Fast / Nightmare rules for this run.</summary>
    public ModeRules Rules { get; set; } = ModeRules.Normal;

    /// <summary>The wave being fought, or the one about to start during a rest.</summary>
    public int Wave { get; private set; }

    public WavePhase Phase { get; private set; }

    /// <summary>The highest wave that has begun so far; 0 before the first wave.</summary>
    public int HighestStartedWave { get; private set; }

    /// <summary>Seconds until the next wave starts; only meaningful during a rest.</summary>
    public float RestSecondsLeft { get; private set; }

    /// <summary>Enemies of the current wave still to be killed, including ones not yet spawned.</summary>
    public int EnemiesRemaining => Spawner.PendingCount + _enemies.Count;

    public void Update(float deltaSeconds)
    {
        if (Phase == WavePhase.Rest)
        {
            RestSecondsLeft = Math.Max(0f, RestSecondsLeft - deltaSeconds);
            if (RestSecondsLeft <= 0f)
            {
                StartWave();
            }

            return;
        }

        Spawner.Update(deltaSeconds);
        if (Spawner.IsFinished && _enemies.Count == 0)
        {
            EndWave();
        }
    }

    private void StartWave()
    {
        var waves = _data.Waves;

        // A boss wave sends the boss first, with a smaller budget of other enemies alongside.
        // Nightmare builds each wave as if it were a later one; bosses keep their own waves.
        _composition.Clear();
        var difficultyWave = Wave + Rules.DifficultyWaveOffset;
        var budget = WaveMath.Budget(difficultyWave, waves) * BudgetMultiplier * Rules.BudgetMultiplier;
        var boss = _data.GetBossForWave(Wave);
        if (boss is not null)
        {
            _composition.Add(boss.AsEnemy);
            budget *= boss.AddsBudgetFraction;
        }

        WaveMath.Compose(
            difficultyWave, budget, _data.Enemies, waves.NewerTypeBias, _random, _composition, Rules.UnlockWaveMultiplier);
        Spawner.Begin(
            _composition,
            WaveMath.SpawnInterval(difficultyWave, waves) * Rules.SpawnIntervalMultiplier,
            WaveMath.GroupSize(difficultyWave, waves));

        Phase = WavePhase.Active;
        HighestStartedWave = Math.Max(HighestStartedWave, Wave);
        _audio.PlaySfx(SfxIds.WaveStart);
    }

    private void EndWave()
    {
        Wave++;
        Phase = WavePhase.Rest;
        RestSecondsLeft = _data.Game.RestCountdownSeconds;
        _audio.PlaySfx(SfxIds.WaveEnd);
    }
}
