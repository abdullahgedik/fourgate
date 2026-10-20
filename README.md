# Fourgate

A wave-based, top-down arena shooter in the spirit of *Boxhead*, built with C# and MonoGame.

You are trapped in a square arena. Zombies pour in through four doors, one in the middle of
each wall. Survive wave after wave, collect crates, level up, and upgrade a growing arsenal.

**Status: in development.** The core loop is playable; more weapons, enemy types, bosses,
local co-op and menus are still to come.

## What works today

- A 3000×3000 arena with four symmetric pillars, and a camera that follows the player and
  leans toward the aim point
- Waves that grow with a spawn budget; doors glow before a group emerges, and an
  edge-of-screen marker points to a glowing door outside the view
- Five zombie types (normal, fast, ranged with telegraphed fireballs, tank, elite with a
  fireball volley) and bosses: The Butcher on wave 10 (charge, ground slam, summons),
  the Pyre Witch on wave 20 (fireball rings and spirals, teleport, summons), The
  Juggernaut on wave 30 (shield, sweeping fire stream, charge), the Swarm Mother on
  wave 40 (summons every zombie kind, each followed by a matching attack; splits on death)
  and The Warden on wave 50 (three phases, borrowed attacks, a sweeping laser)
- Knockback on hits; enemy bodies are solid, and a dash passes through them
- Enemies find their way round pillars and barrels (flow fields), break through barrels only
  when a player is walled in, and in co-op each type picks its target its own way
- A Boxhead-style kill combo (up to x20 score) with the best combo kept in the high scores
- Particle effects: explosion bursts with smoke, flames over fire pools, burning enemies, ember trails,
  hit puffs and dust; screen shake on explosions and hits (can be switched off in Settings)
- Nine guns (pistol, SMG, shotgun, marksman rifle, assault rifle, sniper rifle,
  flamethrower, rocket launcher, railgun) and four pieces of equipment (hand grenade, molotov, mine,
  explosive barrel), each with levels
  1–10 and a special ability at level 10
- An equipment button: grenades are thrown with their own key while the gun stays in hand
- Explosions that hurt the thrower too (at half strength)
- Fast bullets with swept collision, so nothing is skipped at high speed
- Player health, contact damage, and a dash with invincibility frames
- XP and levels; every level banks an upgrade pick
- Upgrade cards between waves: three random cards per pick, covering weapon levels and
  player stats
- Health, ammo, upgrade and weapon crates
- A start screen to choose 1 player or 2-player local co-op (vertical split-screen; player 2 on a
  gamepad), with Fast and Nightmare mode toggles
- A score (a team score in co-op, multiplied by the mode) and a game over screen with the score,
  wave reached, kills and time
- Local high scores: a top 10 per mode with initials, saved in `%APPDATA%/Fourgate/scores.json`
- A main menu and a settings screen (window mode, window size, screen shake, master / music /
  effects volume, rebindable keyboard controls), saved in `%APPDATA%/Fourgate/settings.json`
- Sound: plays WAV / OGG files dropped into `Assets/Audio/` (see Sound below; no files ship yet)
- A pause menu (Esc / gamepad Start) and a "50 waves survived" screen after the last boss

## Planned

- Sound files (the player is ready; the files are still being chosen) and possibly richer models
- Status effects (slows, poison, stuns and the like) for new enemies, boss attacks and weapons
- Waves 51–100 and an endless mode, once the 50-wave game is finished

## Requirements

- Windows
- [.NET SDK](https://dotnet.microsoft.com/download) 9 or newer

MonoGame and the other dependencies are NuGet packages and are restored automatically on the
first build.

## Build and run

```powershell
dotnet run
```

Run the unit tests:

```powershell
dotnet test tests/Fourgate.Tests
```

## Controls

| Action | Keyboard and mouse | Gamepad (Xbox layout) |
|---|---|---|
| Move | W A S D | Left stick |
| Aim | Mouse | Right stick |
| Fire | Left mouse button | Right trigger |
| Dash (passes through enemies) | Left Shift | A |
| Reload | R | X |
| Next / previous gun | Mouse wheel, E / Q | RB / LB |
| Select gun directly | 1–9 | |
| Use equipment: hold to aim a grenade, molotov or mine (its landing spot shows), release to throw | G (lands at the cursor) | Left trigger (right stick: direction, and tilt for distance) |
| Next equipment | T | D-pad left / right |
| Choose an upgrade card | Click, or A / D then Enter | Left / right, then A |
| Pause / resume a run | Esc | Start |
| Menus | Arrows / WASD to move, Enter to pick (or start on a player card, switch a mode toggle), Esc to go back, or click | D-pad or left stick, A, B to go back |
| Back to the start screen after game over | Enter | A or Start |
| Borderless fullscreen | F11 | |
| Cycle window size | F9 | |

Player 1's keyboard keys can be changed in Settings → CONTROLS (fire, the mouse wheel, 1–9, Esc and Enter stay fixed).

Gamepad support is implemented but has not been tested yet.

## Sound

Put sound files in `Assets/Audio/` (create the folder if it is missing). Each file is named after
the event it plays for; events without a file are simply silent, so sounds can be added one at a
time.

- **Effects are `.wav`**, **music is `.ogg`** (music loops).
- **Variants:** add `-2`, `-3`… (`enemy_death.wav`, `enemy_death-2.wav`) and one is picked at
  random each time.
- **Guns:** `weapon_fire.wav` is used by every gun without its own `weapon_fire_<gun>.wav`; the
  same for `weapon_throw.wav` and the equipment.
- Rapid fire is kept in check (at most 4 copies of one sound at once) and each play is pitched a
  little up or down; see `audio` in `Data/game.json`.

| Group | File names (without extension) |
|---|---|
| Guns | `weapon_fire`, or one per gun: `weapon_fire_pistol`, `weapon_fire_smg`, `weapon_fire_shotgun`, `weapon_fire_marksman`, `weapon_fire_assault`, `weapon_fire_sniper`, `weapon_fire_flamethrower`, `weapon_fire_rocket`, `weapon_fire_railgun` |
| Equipment | `weapon_throw`, or one each: `weapon_throw_grenade`, `weapon_throw_molotov`, `weapon_throw_mine`, `weapon_throw_barrel` |
| Weapons | `weapon_empty`, `weapon_reload_start`, `weapon_reload_end`, `weapon_switch`, `equipment_switch`, `weapon_level_up`, `weapon_unlocked`, `bullet_ricochet`, `explosion`, `fire_start`, `mine_placed`, `barrel_struck`, `armor_hit` |
| Player | `player_dash`, `player_hurt`, `player_death`, `player_revive`, `level_up`, `upgrade_picked` |
| Enemies and bosses | `enemy_hit`, `enemy_death`, `enemy_telegraph`, `enemy_fireball`, `door_warning`, `boss_appear`, `boss_telegraph`, `boss_lethal_warning`, `boss_attack`, `boss_phase`, `boss_death` |
| Run | `wave_start`, `wave_end`, `crate_drop`, `pickup_ammo`, `pickup_health`, `pickup_upgrade`, `pickup_weapon`, `pause`, `resume`, `game_over`, `new_high_score`, `waves_survived` |
| Menus | `ui_move`, `ui_confirm`, `ui_back` |
| Music (`.ogg`) | `music_menu`, `music_gameplay`, `music_boss`, `music_game_over` |

## Tuning

All balance values live in JSON files under `Data/` and are read when the game starts, so the
game can be tuned without touching code:

| File | Contents |
|---|---|
| `game.json` | Arena, camera, player, XP curve, crates and global rules |
| `weapons.json` | Weapons, their per-level bonuses and level-10 abilities |
| `enemies.json` | Enemy types |
| `bosses.json` | Bosses, the wave each leads and their attack patterns |
| `waves.json` | Spawn budget curve and wave pacing |
| `modes.json` | Fast and Nightmare mode rules |
| `upgrades.json` | Upgrade cards |

A bad value is reported at startup with the file, the field and the reason.

### Debug options

The `debug` section of `Data/game.json` has switches for testing:

| Setting | Effect |
|---|---|
| `hotkeys` | Enables the debug keys below |
| `practiceTargets` | Adds a still and a moving dummy target |
| `oneShotKills` | Every bullet kills what it hits |
| `disableWaves` | Leaves the arena empty |
| `showAudioEvents` | Lists the latest sound events on screen (handy for finding which sounds are still missing) |
| `keyboardPlayerTwo` | In co-op, player 2 can also use IJKL to move, U fire, O dash, P reload, Y equipment, H next equipment, N / M guns |
| `startWave` | Starts the run at the given wave |
| `seed` | Fixes the random seed so a run can be reproduced |

| Key | Effect |
|---|---|
| F1 / F2 | Spawn one / ten enemies |
| F3 | Give 50 XP |
| F4 | Drop one crate of each type |
| F5 | Unlock and give every weapon |
| F6 | Toggle one-shot kills |
| F7 | Level up the weapon in hand |
| Ctrl + F7 | Every weapon to level 10 |
| F8 | Switch which enemy (or boss) F1/F2 spawn |
| Ctrl + F8 | Show / hide the frame-time readout (FPS, update and draw time, worst frame, enemies, particles) |

## Project layout

```
Program.cs, FourgateGame.cs   Entry point and main loop
Core/                         Input, rendering, audio player, data loading, pooling, spatial hash
Entities/                     Player, enemies, projectiles, pickups, arena
Weapons/                      Weapon logic and stats
Systems/                      Waves, spawning, shots, drops, XP, upgrades
Scenes/                       Gameplay scene
UI/                           HUD, upgrade screen, game over screen
Data/                         Balance and configuration (JSON)
Assets/Fonts/                 Font used for all text
tests/Fourgate.Tests/         Unit tests
```

The game renders to a fixed 1920×1080 virtual screen that is scaled to the window with black
bars, and the simulation runs at a fixed 60 steps per second.

## Credits

- Built with [MonoGame](https://monogame.net/) and [FontStashSharp](https://github.com/FontStashSharp/FontStashSharp)
- Font: Rajdhani (SIL Open Font License)
