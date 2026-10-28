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
        public const string Version = "0.4.0";

        internal static ManualLogSource Log;
        internal static ConfigEntry<bool> CameraOptimization;
        internal static ConfigEntry<bool> SkipDeadKickCheck;
        internal static ConfigEntry<bool> PunchForceCache;
        internal static ConfigEntry<bool> KeybindCache;
        internal static ConfigEntry<float> NetworkPollRate;
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
            NetworkPollRate = Config.Bind("Optimizations", "NetworkPollRate", 0f,
                "Max times per second to poll Steam for incoming packets. 0 = every frame (vanilla). E.g. 500 saves ~10 Steam calls on frames in between, at the cost of up to 2 ms extra latency on incoming packets.");
            Profiler = Config.Bind("Profiler", "Enabled", false,
                "Time every per-frame game script and log the most expensive ones to LogOutput.log. Adds overhead; only enable to measure.");
            ProfilerInterval = Config.Bind("Profiler", "Interval", 10f,
                "Seconds between profiler reports.");

            Harmony harmony = new Harmony(Guid);
            harmony.PatchAll(typeof(Plugin).Assembly);
            if (Profiler.Value)
            {
                ScriptProfiler.Install(harmony);
            }
            Log.LogInfo($"{Name} {Version} loaded (CameraOptimization={CameraOptimization.Value}, SkipDeadKickCheck={SkipDeadKickCheck.Value}, PunchForceCache={PunchForceCache.Value}, KeybindCache={KeybindCache.Value}, NetworkPollRate={NetworkPollRate.Value}, Profiler={Profiler.Value})");
        }

        private void Update()
        {
            if (Profiler.Value)
            {
                ScriptProfiler.Tick();
            }
        }
    }
}
