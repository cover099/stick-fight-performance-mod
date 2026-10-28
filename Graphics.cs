using HarmonyLib;
using UnityEngine;
using UnityEngine.PostProcessing;

namespace PerfMod
{
    // Optional: turn off the camera's post-processing (anti-aliasing, bloom, colour grading, etc.,
    // whatever the game's profile has on). It renders the whole screen several extra times per frame,
    // which is expensive on weak or integrated GPUs. Visual only; the game also loses its pause blur.
    [HarmonyPatch(typeof(PostProcessingBehaviour), "OnEnable")]
    internal static class PostProcessingPatch
    {
        private static void Postfix(PostProcessingBehaviour __instance)
        {
            if (Plugin.DisablePostProcessing.Value)
            {
                __instance.enabled = false;
            }
        }
    }

    internal static class QuietLogging
    {
        // The game logs a line on most events (every hit logs ~4). Unity captures a full stack trace
        // for each one, which is far slower than writing the line and shows up as small hitches on
        // slow CPUs. Keep the lines in output_log.txt but drop the traces; errors keep theirs.
        internal static void Apply()
        {
            Application.SetStackTraceLogType(LogType.Log, StackTraceLogType.None);
            Application.SetStackTraceLogType(LogType.Warning, StackTraceLogType.None);
        }
    }
}
