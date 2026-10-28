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
| `QuietLogging` | on | The game writes a log line on most events (several per hit), and Unity captures a full stack trace for each one. The lines are still written, just without the traces (errors keep theirs). Reduces small hitches on slow CPUs. |

### Graphics

| Setting | Default | What it does |
|---|---|---|
| `DisablePostProcessing` | off | Turns off the camera's post-processing effects (anti-aliasing, bloom, colour grading, etc.). A big help on weak or integrated GPUs. The game looks a bit plainer and loses its pause blur. |

### Profiler

`[Profiler] Enabled = true` times every `Update`/`LateUpdate`/`FixedUpdate` in the game's scripts and logs the most expensive ones to `BepInEx/LogOutput.log` every `Interval` seconds. Each report also splits the frame into scripts, physics, rendering and other/waiting (GPU, vsync, frame cap), and shows the worst frame and how many frames took over 20 ms. At startup it logs the CPU, GPU, display and graphics settings, so a log from someone else's PC shows what it's running on. It adds overhead, so only turn it on to measure.

## Install

### Easy install (Windows)

1. Download `PerformanceMod-windows-installer.zip` from the [latest release](https://github.com/cover099/stick-fight-performance-mod/releases/latest) and extract it anywhere.
2. Close the game, then double-click `Install.bat`.

It finds the game through Steam (or asks for the folder), installs BepInEx 5 if it isn't there yet, and puts the mod in place. If Windows shows "Windows protected your PC", click **More info** → **Run anyway**.

### Manual install

1. Install [BepInEx 5](https://github.com/BepInEx/BepInEx/releases) (`BepInEx_win_x64`) into the game folder.
2. Copy `PerformanceMod.dll` into `BepInEx/plugins` (delete an older `PerfMod.dll` there first).

On Linux, set the game's Steam launch options to `WINEDLLOVERRIDES="winhttp=n,b" %command%` so BepInEx loads under Proton.

## Build

Requires the .NET SDK and a copy of the game with BepInEx installed. The project compiles against the game's own assemblies (`StickFight_Data/Managed`) and BepInEx's `core` folder; none of them are included here.

```sh
dotnet build -c Release -p:GameDir=/path/to/StickFightTheGame/
```

By default `GameDir` is `../StickFightTheGame/` next to this folder. The built DLL is copied into that install's `BepInEx/plugins` folder.
