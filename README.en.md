# Arabella · ArabellaMod

[中文](README.md) | English

**Arabella** is a custom character mod for **Slay the Spire 2**, built on RitsuLib. Her combat revolves around a serpent whip, Laceration, and cards that cycle through the Exhaust Pile and return to the Hand.

Exhaust cards to trigger **Indulgence**, retrieve them through **Control**, and activate **Sensing** when they enter your Hand. Build Lust and Excitement along the way to sustain your combos.

## Features

- A custom character with her own card pool, starter relic, and upgraded relic.
- Control, Indulgence, and Sensing card mechanics; Lust and Excitement combat resources.
- Laceration damage, generated cards, and Exhaust-and-recovery synergies.
- Character frame animations, back-pose animation, card and combat effects, voice lines, and sound effects.
- Lust and Excitement displays, Control progress, and card glow feedback.
- Simplified Chinese and English localization, with settings accessible from the main menu and run pause menu.

The current mod version is `0.1.0`. Development and balancing are ongoing; some secondary text and asset configuration still retain template content. Current code and in-game behavior determine the actual effects.

## Starting configuration

| Item | Value |
| --- | --- |
| Character | Arabella |
| Starting HP | 75 |
| Starting gold | 99 |
| Starting deck | 4 Raking Strikes, 4 Crimson Domains, 1 A Moment of Indulgence, 1 First Taming, and 1 Serpentine Nature |
| Starter relic | Crimson Serpent Whip |
| Author | Kalipei |

**Crimson Serpent Whip:** After playing and resolving a Power, you may Exhaust up to 1 other card in your Hand, enabling Indulgence triggers and Control recovery.

## Core mechanics

| Mechanic | Effect |
| --- | --- |
| **Control X** | While this card is in the Exhaust Pile, it independently tracks Energy and Lust actually spent to play other cards. At X, return it to your Hand and reduce its cost by 1 this turn, to a minimum of 0. Its own play cost does not count; progress resets each time it enters the Exhaust Pile. |
| **Indulgence** | Trigger the card's additional effect after it is Exhausted. |
| **Sensing** | Trigger the card's effect whenever it enters your Hand by any means, including the opening draw. |
| **Laceration** | At the end of the enemy turn, deal damage equal to its stacks, bypassing Block, then halve its stacks, rounding down. Other Powers can further modify its benefits. |

### Lust

Lust is an additional combat resource with an initial maximum of **9**. Gain 1 whenever a card leaves the Exhaust Pile. Cards and Powers can also grant or spend it.

Once per combat, taking lethal damage while you have Lust consumes all Lust and prevents death, healing 3 HP per Lust actually consumed. Each rescue permanently lowers your Lust maximum by 1 for the current run, down to 0. This reduction persists between combats and through saving and loading; a new run restores the maximum to 9.

### Excitement

Excitement has a maximum of **10**. Gain 1 for each Lust actually spent and each Indulgence trigger. Lose 2 after taking unblocked damage.

| Threshold | Benefit |
| --- | --- |
| 3 | Gain 2 Block whenever Sensing triggers. |
| 6 | Each Control return reduces that card's cost by an additional 1, to a minimum of 0. These reductions stack across turns. Falling below 6 clears all reductions from this effect; reaching 6 again starts a new accumulation. |
| 10 | Draw 2 cards on entering this state. When playing a card, each missing Energy can be replaced with 1 additional Lust. |

## Installation

You need the game, RitsuLib, and the built Arabella mod files. The current [mod manifest](ArabellaMod.json) declares a minimum game version of `0.110.0` and a RitsuLib dependency version of `0.5.20`. These are repository configuration values; compatibility with other versions requires testing.

Place the built files under your game installation:

```text
Slay the Spire 2/
└── mods/
    ├── STS2-RitsuLib/          # RitsuLib framework files
    └── ArabellaMod/
        ├── ArabellaMod.dll
        ├── ArabellaMod.json
        └── ArabellaMod.pck
```

Start the game, confirm that the framework and mod load, then select Arabella. This repository contains source code and assets; a downloaded source ZIP must be built to produce the runtime files above.

## Building from source

The project uses **.NET 9, C# 13, and Godot.NET.Sdk 4.5.1**. A full resource export also requires a matching MegaDot/Godot .NET executable and export environment. See [local.props.template](local.props.template) for local path configuration.

```powershell
git clone https://github.com/mosharrafhabbal-png/ArabellaMod.git
cd ArabellaMod
Copy-Item .\local.props.template .\local.props
```

Edit `local.props`:

| Field | Purpose |
| --- | --- |
| `Sts2Dir` | Game installation directory. |
| `Sts2DataDir` | Game assembly directory; defaults to `data_sts2_windows_x86_64` under the game directory. |
| `GodotExe` | MegaDot/Godot .NET executable used to export the PCK. |
| `RitsuLibDeployDir` | Optional deployment directory for the RitsuLib framework. |

Run a full build:

```powershell
dotnet build .\ArabellaMod.csproj
```

By default, this compiles C#, copies the DLL and manifest to `mods/ArabellaMod/`, and invokes Godot to export the PCK to the same directory.

To check C# compilation while skipping this mod's copy step and PCK export:

```powershell
dotnet build .\ArabellaMod.csproj /p:RunPckExport=false /p:CopyModOnBuild=false
```

A valid game assembly path is still required. RitsuLib is currently referenced with `Version="*"`; builds synchronize the resolved dependency version into the manifest. Check the game version and runtime environment after updating dependencies.

## Visual and audio settings

Arabella's settings page is available from the main menu and run pause menu:

- Enable or disable back-pose animation and adjust its size from 80% to 150%.
- Enable or disable frame-sequence effects.
- Toggle character voice and sound effects independently, with separate volume controls from 50% to 150%.

Settings are saved, and reset options restore their defaults.

## Source map

| Path | Contents |
| --- | --- |
| [ArabellaModCode/Cards](ArabellaModCode/Cards) | Card behavior and art configuration. |
| [ArabellaModCode/Mechanics](ArabellaModCode/Mechanics) | Control, Indulgence, Sensing, Lust, Excitement, and related UI. |
| [ArabellaModCode/Powers](ArabellaModCode/Powers) | Laceration and other combat Powers. |
| [ArabellaModCode/Relics](ArabellaModCode/Relics) | Starter and upgraded relic behavior. |
| [ArabellaModCode/Animation](ArabellaModCode/Animation) | Character animations and playback control. |
| [ArabellaModCode/Audio](ArabellaModCode/Audio), [Vfx](ArabellaModCode/Vfx) | Voice, sound, and combat effects. |
| [ArabellaMod](ArabellaMod) | Godot scenes, images, audio, and localization. |
| [tools](tools) | Deployment helpers and focused regression checks. |

Run the existing focused checks from the repository root:

```powershell
powershell -File .\tools\card-rewrite-tests\Run.ps1
powershell -File .\tools\indulgent-nature-tests\Run.ps1
powershell -File .\tools\lust-survival-tests\Run.ps1
```

These cover selected card resolution behavior, Indulgent Nature, and Lust death prevention. Full behavior still needs in-game verification.

## Repository scope

The repository includes source code, visual and audio assets, build configuration, and development tools. Card design spreadsheets in `design/` remain local and are excluded from uploads. Local paths in `local.props`, build caches, `artifacts/`, and `tmp/` are also excluded through `.gitignore`.

Built on RitsuLib and its mod template, with thanks to the framework and tooling authors.
