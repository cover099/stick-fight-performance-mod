# Stick Fight Performance Mod

A [BepInEx 5](https://github.com/BepInEx/BepInEx) plugin for **Stick Fight: The Game** that removes wasted work from the game's per-frame scripts. It doesn't change gameplay, and works on both Windows and Linux (Proton).

## Optimizations

Each one can be turned off in `BepInEx/config/perfmod.stickfight.cfg`.

| Setting | Default | What it does |
|---|---|---|
| `CameraOptimization` | on | The camera and moving platforms search the whole scene for players several times per frame (6+ with 4 players). They now share one search per frame. |
| `SkipDeadKickCheck` | on | Skips the online AFK-kick check, which makes 2 Steam API calls per player per frame but can never kick anyone (the input check it relies on always returns true). |
| `PunchForceCache` | on | Every punchable body part re-fetches 6 component references each frame. They're now cached and only refreshed when the part changes owner. |
| `KeybindCache` | on | In menus and local play, all 16 keybinds are read from PlayerPrefs every frame. They're now cached and refreshed when a binding is saved or reset. |
| `NetworkPollRate` | 0 (off) | Optionally limits how often per second Steam is polled for incoming packets. For example 500 = at most 2 ms of extra latency on incoming packets. |

### Profiler

`[Profiler] Enabled = true` times every `Update`/`LateUpdate`/`FixedUpdate` in the game's scripts and logs the most expensive ones to `BepInEx/LogOutput.log` every `Interval` seconds. It adds overhead, so only turn it on to measure.

## Install

1. Install [BepInEx 5](https://github.com/BepInEx/BepInEx/releases) (x64) into the game folder.
2. Copy `PerformanceMod.dll` into `BepInEx/plugins`.

## Build

Requires the .NET SDK and a copy of the game with BepInEx installed. The project compiles against the game's own assemblies (`StickFight_Data/Managed`) and BepInEx's `core` folder; none of them are included here.

```sh
dotnet build -c Release -p:GameDir=/path/to/StickFightTheGame/
```

By default `GameDir` is `../StickFightTheGame/` next to this folder. The built DLL is copied into that install's `BepInEx/plugins` folder.
