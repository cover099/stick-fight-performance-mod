using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;

namespace PerfMod
{
    [BepInPlugin(Guid, Name, Version)]
    public class Plugin : BaseUnityPlugin
    {
        public const string Guid = "perfmod.stickfight";
        public const string Name = "PerformanceMod";
        public const string Version = "0.7.0";

        internal static ManualLogSource Log;
        internal static ConfigEntry<bool> CameraOptimization;
        internal static ConfigEntry<bool> SkipDeadKickCheck;
        internal static ConfigEntry<bool> PunchForceCache;
        internal static ConfigEntry<bool> KeybindCache;
        internal static ConfigEntry<bool> MenuButtonCheck;
        internal static ConfigEntry<bool> KeyJoinCheck;
        internal static ConfigEntry<bool> AsyncMapLoad;
        internal static ConfigEntry<bool> LoadingScreenCheck;
        internal static ConfigEntry<float> NetworkPollRate;
        internal static ConfigEntry<bool> QuietLogging;
        internal static ConfigEntry<bool> DisablePostProcessing;
        internal static ConfigEntry<bool> RedundantCameras;
        internal static ConfigEntry<bool> LowPolyParticles;
        internal static ConfigEntry<bool> HideDecor, HideSprites, HideMapParticles, HideFightEffects, HideHUD;
        internal static ConfigEntry<bool> Profiler;
        internal static ConfigEntry<float> ProfilerInterval;

        private void Awake()
        {
            Log = Logger;
            CameraOptimization = Config.Bind("Optimizations", "CameraOptimization", true,
                "Share one scene search per frame for the camera and moving platforms (vanilla searches the whole scene 6+ times per frame with 4 players).");
            SkipDeadKickCheck = Config.Bind("Optimizations", "SkipDeadKickCheck", true,
                "Skip the online AFK-kick check, which makes 2 Steam calls per player per frame but can never kick anyone (vanilla bug). No behaviour change.");
            PunchForceCache = Config.Bind("Optimizations", "PunchForceCache", true,
                "Stop every punchable body part from re-fetching 6 component references each frame.");
            KeybindCache = Config.Bind("Optimizations", "KeybindCache", true,
                "Cache keybinds instead of reading all 16 from PlayerPrefs (stored in the registry) every frame in menus and local play.");
            MenuButtonCheck = Config.Bind("Optimizations", "MenuButtonCheck", true,
                "Skip the pause menu buttons' per-frame selection check while not paused (it only acts while paused, but first reads object names, creating garbage every frame).");
            KeyJoinCheck = Config.Bind("Optimizations", "KeyJoinCheck", true,
                "Precompute the keys of the per-frame 'does a keyboard player want to join' check (local play and menus) instead of rebuilding them every frame (about a third of the game's garbage, so fewer GC stutters).");
            LoadingScreenCheck = Config.Bind("Optimizations", "LoadingScreenCheck", true,
                "Stop re-stopping the (already stopped) loading screen spinner every frame, which makes garbage each time.");
            AsyncMapLoad = Config.Bind("Optimizations", "AsyncMapLoad", true,
                "Load the next map in the background instead of freezing for it (vanilla: a ~40 ms freeze during the map-change animation).");
            NetworkPollRate = Config.Bind("Optimizations", "NetworkPollRate", 0f,
                "Max times per second to poll Steam for incoming packets. 0 = every frame (vanilla). E.g. 500 saves ~10 Steam calls on frames in between, at the cost of up to 2 ms extra latency on incoming packets.");
            QuietLogging = Config.Bind("Optimizations", "QuietLogging", true,
                "Don't capture a stack trace for every game log line (the game logs several per hit). Lines still go to output_log.txt; errors keep their traces.");
            DisablePostProcessing = Config.Bind("Graphics", "DisablePostProcessing", false,
                "Turn off the camera's post-processing effects (anti-aliasing, bloom, colour grading...). Big help on weak or integrated GPUs; the game looks a bit plainer and loses its pause blur.");
            RedundantCameras = Config.Bind("Graphics", "RedundantCameras", true,
                "Skip two cameras that render every frame for nothing: the level editor's screenshot camera (draws the whole scene, which the main camera then redraws) and the pause menu camera and its menu canvas while you're not paused. Looks the same; measured +~22% FPS (cameras) and +~19% more (canvas).");
            LowPolyParticles = Config.Bind("Graphics", "LowPolyParticles", true,
                "Draw particles that use Unity's 515-vertex sphere (e.g. the glowing orbs on Woods maps) with a 42-vertex sphere of the same size. They're a few pixels across, so it looks the same; measured +~37% FPS on Woods 1.");
            HideDecor = Config.Bind("Graphics", "HideDecor", false,
                "Low detail: hide background scenery (meshes you can't touch: no collider, not a player or weapon). ~+5% FPS on Woods.");
            HideSprites = Config.Bind("Graphics", "HideSprites", false,
                "Low detail: hide flat sprite decorations that aren't part of players, weapons or anything solid. ~+2% FPS.");
            HideMapParticles = Config.Bind("Graphics", "HideMapParticles", false,
                "Low detail: hide maps' ambient particle effects (glowing orbs, leaves, fireflies...). Gain depends on the map.");
            HideFightEffects = Config.Bind("Graphics", "HideFightEffects", false,
                "Low detail: hide players' and weapons' particle effects (dust, punch/block/damage puffs, muzzle effects). You lose some hit feedback.");
            HideHUD = Config.Bind("Graphics", "HideHUD", false,
                "Low detail: hide the score display and the canvases above players. ~+5% FPS.");
            Profiler = Config.Bind("Profiler", "Enabled", false,
                "Time every per-frame game script and log the most expensive ones to LogOutput.log. Adds overhead; only enable to measure.");
            ProfilerInterval = Config.Bind("Profiler", "Interval", 10f,
                "Seconds between profiler reports.");

            if (QuietLogging.Value)
            {
                PerfMod.QuietLogging.Apply();
            }
            Harmony harmony = new Harmony(Guid);
            harmony.PatchAll(typeof(Plugin).Assembly);
            gameObject.AddComponent<RedundantCameras>();
            gameObject.AddComponent<LowPolyParticles>();
            gameObject.AddComponent<LowDetail>();
            if (Profiler.Value)
            {
                ScriptProfiler.Install(harmony);
                gameObject.AddComponent<FrameTimer>();
            }
            Log.LogInfo($"{Name} {Version} loaded (CameraOptimization={CameraOptimization.Value}, SkipDeadKickCheck={SkipDeadKickCheck.Value}, PunchForceCache={PunchForceCache.Value}, KeybindCache={KeybindCache.Value}, MenuButtonCheck={MenuButtonCheck.Value}, KeyJoinCheck={KeyJoinCheck.Value}, AsyncMapLoad={AsyncMapLoad.Value}, LoadingScreenCheck={LoadingScreenCheck.Value}, NetworkPollRate={NetworkPollRate.Value}, QuietLogging={QuietLogging.Value}, DisablePostProcessing={DisablePostProcessing.Value}, RedundantCameras={RedundantCameras.Value}, LowPolyParticles={LowPolyParticles.Value}, Profiler={Profiler.Value})");
        }
    }
}
