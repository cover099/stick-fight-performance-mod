using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Text;
using HarmonyLib;
using UnityEngine;

namespace PerfMod
{
    // Opt-in: times every Update/LateUpdate/FixedUpdate in the game's scripts and periodically logs
    // where the frame goes. "Scripts" vs "frame" tells whether the game is limited by its own code
    // or by the engine (rendering, physics, overlays). Timing adds overhead, so only enable to measure.
    internal static class ScriptProfiler
    {
        private class Entry
        {
            public string Name;
            public long Ticks;
            public long Calls;
        }

        private static readonly Dictionary<MethodBase, Entry> Entries = new Dictionary<MethodBase, Entry>();
        private static int frames;
        private static float frameSeconds;
        private static float nextReport;
        private static int gcAtLastReport;

        internal static void Install(Harmony harmony)
        {
            HarmonyMethod prefix = new HarmonyMethod(AccessTools.Method(typeof(ScriptProfiler), nameof(Prefix)));
            HarmonyMethod postfix = new HarmonyMethod(AccessTools.Method(typeof(ScriptProfiler), nameof(Postfix)));
            int patched = 0;
            foreach (Type type in AccessTools.GetTypesFromAssembly(typeof(GameManager).Assembly))
            {
                if (!typeof(MonoBehaviour).IsAssignableFrom(type) || type.IsGenericTypeDefinition)
                {
                    continue;
                }
                foreach (MethodInfo method in AccessTools.GetDeclaredMethods(type))
                {
                    if ((method.Name != "Update" && method.Name != "LateUpdate" && method.Name != "FixedUpdate")
                        || method.IsAbstract || method.IsStatic || method.GetParameters().Length != 0 || method.ReturnType != typeof(void))
                    {
                        continue;
                    }
                    Entries[method] = new Entry { Name = type.Name + "." + method.Name };
                    try
                    {
                        harmony.Patch(method, prefix, postfix);
                        patched++;
                    }
                    catch (Exception e)
                    {
                        Plugin.Log.LogWarning($"Profiler: could not patch {type.Name}.{method.Name}: {e.Message}");
                    }
                }
            }
            nextReport = Time.realtimeSinceStartup + Plugin.ProfilerInterval.Value;
            gcAtLastReport = GC.CollectionCount(0);
            Plugin.Log.LogInfo($"Profiler: timing {patched} methods, reporting every {Plugin.ProfilerInterval.Value}s");
        }

        private static void Prefix(out long __state)
        {
            __state = Stopwatch.GetTimestamp();
        }

        private static void Postfix(MethodBase __originalMethod, long __state)
        {
            if (Entries.TryGetValue(__originalMethod, out Entry entry))
            {
                entry.Ticks += Stopwatch.GetTimestamp() - __state;
                entry.Calls++;
            }
        }

        // Called from Plugin.Update once per frame.
        internal static void Tick()
        {
            frames++;
            frameSeconds += Time.unscaledDeltaTime;
            if (Time.realtimeSinceStartup < nextReport)
            {
                return;
            }
            nextReport = Time.realtimeSinceStartup + Plugin.ProfilerInterval.Value;

            List<Entry> sorted = new List<Entry>(Entries.Values);
            sorted.Sort((a, b) => b.Ticks.CompareTo(a.Ticks));
            double msPerTick = 1000.0 / Stopwatch.Frequency;
            long totalTicks = 0;
            foreach (Entry entry in sorted)
            {
                totalTicks += entry.Ticks;
            }
            int gc = GC.CollectionCount(0);

            StringBuilder sb = new StringBuilder();
            sb.AppendLine($"Profiler: {frames} frames, avg {frameSeconds * 1000f / frames:F3} ms/frame ({frames / frameSeconds:F0} FPS), "
                + $"scripts {totalTicks * msPerTick / frames:F3} ms/frame, GCs {gc - gcAtLastReport}");
            for (int i = 0; i < Math.Min(20, sorted.Count) && sorted[i].Ticks > 0; i++)
            {
                Entry e = sorted[i];
                sb.AppendLine($"  {e.Ticks * msPerTick / frames,8:F4} ms/frame  {(double)e.Calls / frames,7:F1} calls/frame  {e.Name}");
            }
            Plugin.Log.LogInfo(sb.ToString());

            foreach (Entry entry in sorted)
            {
                entry.Ticks = 0;
                entry.Calls = 0;
            }
            frames = 0;
            frameSeconds = 0f;
            gcAtLastReport = gc;
        }
    }
}
