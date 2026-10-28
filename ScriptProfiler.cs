using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Text;
using HarmonyLib;
using UnityEngine;
using UnityEngine.PostProcessing;

namespace PerfMod
{
    // Opt-in: times every Update/LateUpdate/FixedUpdate (and camera callbacks) in the game's scripts
    // and periodically logs where the frame goes:
    //   scripts   game Update/LateUpdate/FixedUpdate code
    //   physics   physics simulation + collision callbacks (between the last FixedUpdate and WaitForFixedUpdate)
    //   render    after the last LateUpdate until the end of the frame: cameras, post-processing, OnGUI overlays
    //   other     the rest: waiting for the GPU / vsync / frame cap, input, animation, engine work
    // plus the worst frame and how many frames took over 20 ms (hitches). Once at startup it logs the
    // hardware and display settings so logs from other people's PCs can be compared.
    // Timing adds overhead, so only enable to measure.
    internal static class ScriptProfiler
    {
        private enum Kind { Update, Fixed, Late, Render }

        private class Entry
        {
            public string Name;
            public Kind Kind;
            public long Ticks;
            public long Calls;
        }

        private const float HitchMs = 20f;
        private static readonly double MsPerTick = 1000.0 / Stopwatch.Frequency;

        private static readonly Dictionary<MethodBase, Entry> Entries = new Dictionary<MethodBase, Entry>();
        private static int frames;
        private static float frameSeconds;
        private static float worstFrame;
        private static int hitches;
        private static float nextReport;
        private static int gcAtLastReport;

        // Frame breakdown, in Stopwatch ticks.
        private static long lastFixedEnd = -1;
        private static long lastLateEnd = -1;
        private static long lastFrameEnd = -1;
        private static long physicsTicks;
        private static long renderTicks;
        private static long measuredFrameTicks;
        private static int physicsSteps;
        private static bool settingsLogged;

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
                    if (method.IsAbstract || method.IsStatic || method.ReturnType != typeof(void) || !KindOf(method, out Kind kind))
                    {
                        continue;
                    }
                    Entries[method] = new Entry { Name = type.Name + "." + method.Name, Kind = kind };
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
            Plugin.Log.LogInfo($"Profiler: system: CPU {SystemInfo.processorType} ({SystemInfo.processorCount} threads), RAM {SystemInfo.systemMemorySize} MB, "
                + $"GPU {SystemInfo.graphicsDeviceName} ({SystemInfo.graphicsMemorySize} MB, {SystemInfo.graphicsDeviceVersion}), OS {SystemInfo.operatingSystem}");
        }

        private static bool KindOf(MethodInfo method, out Kind kind)
        {
            int parameters = method.GetParameters().Length;
            switch (method.Name)
            {
                case "Update" when parameters == 0: kind = Kind.Update; return true;
                case "FixedUpdate" when parameters == 0: kind = Kind.Fixed; return true;
                case "LateUpdate" when parameters == 0: kind = Kind.Late; return true;
                case "OnPreCull" when parameters == 0:
                case "OnPreRender" when parameters == 0:
                case "OnPostRender" when parameters == 0:
                case "OnRenderImage" when parameters == 2:
                    kind = Kind.Render;
                    return true;
                default: kind = Kind.Update; return false;
            }
        }

        private static void Prefix(out long __state)
        {
            __state = Stopwatch.GetTimestamp();
        }

        private static void Postfix(MethodBase __originalMethod, long __state)
        {
            if (!Entries.TryGetValue(__originalMethod, out Entry entry))
            {
                return;
            }
            long now = Stopwatch.GetTimestamp();
            entry.Ticks += now - __state;
            entry.Calls++;
            if (entry.Kind == Kind.Fixed)
            {
                lastFixedEnd = now;
            }
            else if (entry.Kind == Kind.Late)
            {
                lastLateEnd = now;
            }
        }

        // FrameTimer: physics for this step has run (after FixedUpdate scripts and collision callbacks).
        internal static void AfterPhysicsStep()
        {
            if (lastFixedEnd < 0)
            {
                return;
            }
            physicsTicks += Stopwatch.GetTimestamp() - lastFixedEnd;
            physicsSteps++;
            lastFixedEnd = -1;
        }

        // FrameTimer: end of frame, after rendering and OnGUI.
        internal static void EndOfFrame()
        {
            long now = Stopwatch.GetTimestamp();
            if (lastLateEnd >= 0)
            {
                renderTicks += now - lastLateEnd;
                lastLateEnd = -1;
            }
            if (lastFrameEnd >= 0)
            {
                measuredFrameTicks += now - lastFrameEnd;
            }
            lastFrameEnd = now;

            float frameMs = Time.unscaledDeltaTime * 1000f;
            frames++;
            frameSeconds += Time.unscaledDeltaTime;
            worstFrame = Mathf.Max(worstFrame, frameMs);
            if (frameMs > HitchMs)
            {
                hitches++;
            }
            if (Time.realtimeSinceStartup >= nextReport)
            {
                Report();
            }
        }

        private static void Report()
        {
            nextReport = Time.realtimeSinceStartup + Plugin.ProfilerInterval.Value;
            if (!settingsLogged && Camera.main != null)
            {
                settingsLogged = true;
                LogSettings();
            }

            List<Entry> sorted = new List<Entry>(Entries.Values);
            sorted.Sort((a, b) => b.Ticks.CompareTo(a.Ticks));
            long scriptTicks = 0;
            foreach (Entry entry in sorted)
            {
                if (entry.Kind != Kind.Render)
                {
                    scriptTicks += entry.Ticks;
                }
            }
            int gc = GC.CollectionCount(0);
            double frameMs = frameSeconds * 1000.0 / frames;
            double scripts = scriptTicks * MsPerTick / frames;
            double physics = physicsTicks * MsPerTick / frames;
            double render = renderTicks * MsPerTick / frames;
            double measured = measuredFrameTicks * MsPerTick / frames;

            StringBuilder sb = new StringBuilder();
            sb.AppendLine($"Profiler: {frames} frames, avg {frameMs:F3} ms/frame ({frames / frameSeconds:F0} FPS), "
                + $"scripts {scripts:F3} ms/frame, GCs {gc - gcAtLastReport}");
            sb.AppendLine($"  frame: scripts {scripts:F2} + physics {physics:F2} ({(double)physicsSteps / frames:F1} steps) + render {render:F2} "
                + $"+ other/waiting {Math.Max(0.0, measured - scripts - physics - render):F2} ms;  worst frame {worstFrame:F1} ms, {hitches} frames over {HitchMs:F0} ms");
            for (int i = 0; i < Math.Min(20, sorted.Count) && sorted[i].Ticks > 0; i++)
            {
                Entry e = sorted[i];
                sb.AppendLine($"  {e.Ticks * MsPerTick / frames,8:F4} ms/frame  {(double)e.Calls / frames,7:F1} calls/frame  {e.Name}");
            }
            Plugin.Log.LogInfo(sb.ToString());

            foreach (Entry entry in sorted)
            {
                entry.Ticks = 0;
                entry.Calls = 0;
            }
            frames = 0;
            frameSeconds = 0f;
            worstFrame = 0f;
            hitches = 0;
            physicsTicks = 0;
            renderTicks = 0;
            measuredFrameTicks = 0;
            physicsSteps = 0;
            gcAtLastReport = gc;
        }

        private static void LogSettings()
        {
            string post = "none";
            PostProcessingBehaviour behaviour = Camera.main.GetComponent<PostProcessingBehaviour>();
            if (behaviour != null && behaviour.profile != null)
            {
                PostProcessingProfile p = behaviour.profile;
                List<string> on = new List<string>();
                if (p.antialiasing.enabled) on.Add("antialiasing");
                if (p.ambientOcclusion.enabled) on.Add("ambientOcclusion");
                if (p.screenSpaceReflection.enabled) on.Add("reflections");
                if (p.depthOfField.enabled) on.Add("depthOfField");
                if (p.motionBlur.enabled) on.Add("motionBlur");
                if (p.eyeAdaptation.enabled) on.Add("eyeAdaptation");
                if (p.bloom.enabled) on.Add("bloom");
                if (p.colorGrading.enabled) on.Add("colorGrading");
                if (p.userLut.enabled) on.Add("userLut");
                if (p.chromaticAberration.enabled) on.Add("chromaticAberration");
                if (p.grain.enabled) on.Add("grain");
                if (p.vignette.enabled) on.Add("vignette");
                if (p.dithering.enabled) on.Add("dithering");
                post = (behaviour.enabled ? "on: " : "disabled (") + (on.Count > 0 ? string.Join(", ", on.ToArray()) : "no effects") + (behaviour.enabled ? string.Empty : ")");
            }
            Plugin.Log.LogInfo($"Profiler: display {Screen.width}x{Screen.height}{(Screen.fullScreen ? " fullscreen" : " windowed")}, vsync {QualitySettings.vSyncCount}, "
                + $"frame cap {Application.targetFrameRate}, quality '{QualitySettings.names[QualitySettings.GetQualityLevel()]}', "
                + $"physics step {Time.fixedDeltaTime * 1000f:F1} ms, post-processing {post}");
        }
    }

    // Drives the profiler's frame breakdown with Unity's WaitForFixedUpdate / WaitForEndOfFrame points.
    internal class FrameTimer : MonoBehaviour
    {
        private void Start()
        {
            StartCoroutine(PhysicsSteps());
            StartCoroutine(FrameEnds());
        }

        private static IEnumerator PhysicsSteps()
        {
            WaitForFixedUpdate wait = new WaitForFixedUpdate();
            while (true)
            {
                yield return wait;
                ScriptProfiler.AfterPhysicsStep();
            }
        }

        private static IEnumerator FrameEnds()
        {
            WaitForEndOfFrame wait = new WaitForEndOfFrame();
            while (true)
            {
                yield return wait;
                ScriptProfiler.EndOfFrame();
            }
        }
    }
}
