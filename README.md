# Fourgate

A wave-based, top-down arena shooter in the spirit of *Boxhead*, built with C# and MonoGame.

You are trapped in a square arena. Zombies pour in through four doors, one in the middle of
each wall. Survive wave after wave, collect crates, level up, and upgrade a growing arsenal.

**Status: in development.** The core loop is playable; more weapons, enemy types, bosses,
local co-op and menus are still to come.

## What works today

- A 3000×3000 arena with a camera that follows the player and leans toward the aim point
- Waves that grow with a spawn budget; doors glow before a group emerges, and an
  edge-of-screen marker points to a glowing door outside the view
- Four guns (pistol, SMG, shotgun, marksman rifle) and the hand grenade, each with levels
  1–10 and a special ability at level 10
- An equipment button: grenades are thrown with their own key while the gun stays in hand
- Explosions that hurt the thrower too (at half strength)
- Fast bullets with swept collision, so nothing is skipped at high speed
- Player health, contact damage, and a dash with invincibility frames
- XP and levels; every level banks an upgrade pick
- Upgrade cards between waves: three random cards per pick, covering weapon levels and
  player stats
- Health, ammo, upgrade and weapon crates
- A game over screen with wave reached, kills and time

## Planned

- More guns: assault rifle, sniper rifle, flamethrower, rocket launcher and railgun
- More equipment: molotov, mine and explosive barrel
- More enemy types with telegraphed ranged attacks
- A boss every tenth wave
- Local two-player split-screen co-op
- Fast and Nightmare modes, local high scores, menus and settings
- Sound and sprite art (the game currently draws geometric shapes and is silent)

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
| Dash | Space | A |
| Reload | R | X |
| Next / previous gun | Mouse wheel, E / Q | RB / LB |
| Select gun directly | 1–9 | |
| Use equipment (grenade) | G | Left trigger |
| Next equipment | T | D-pad left / right |
| Choose an upgrade card | Click, or A / D then Enter | Left / right, then A |
| Restart after game over | Enter | A or Start |
| Borderless fullscreen | F11 | |
| Cycle window size | F9 | |

Gamepad support is implemented but has not been tested yet.

## Tuning

All balance values live in JSON files under `Data/` and are read when the game starts, so the
game can be tuned without touching code:

| File | Contents |
|---|---|
| `game.json` | Arena, camera, player, XP curve, crates and global rules |
| `weapons.json` | Weapons, their per-level bonuses and level-10 abilities |
| `enemies.json` | Enemy types |
| `waves.json` | Spawn budget curve and wave pacing |
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

## Project layout

```
Program.cs, FourgateGame.cs   Entry point and main loop
Core/                         Input, rendering, audio stub, data loading, pooling, spatial hash
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

- Built with [MonoGame](https://monogame.net/) and
  [FontStashSharp](https://github.com/FontStashSharp/FontStashSharp)
- Text is set in Rajdhani by Indian Type Foundry, used under the SIL Open Font License; the
  license text is in `Assets/Fonts/Rajdhani-OFL.txt`
