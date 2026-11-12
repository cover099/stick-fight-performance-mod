using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using UnityEngine;

namespace PerfMod
{
    // LoadingScreenManager.Update calls part.Stop() on its spinner effect every frame it isn't loading,
    // i.e. all the time. ParticleSystem.Stop() walks the effect's children through a new delegate each call
    // (garbage every frame). Once the effect and all its children are fully stopped, Stop() can't change
    // anything, so it's skipped; while anything is still playing or has particles left it runs as vanilla.
    [HarmonyPatch(typeof(LoadingScreenManager), "Update")]
    internal static class LoadingScreenPatch
    {
        private static readonly Dictionary<int, ParticleSystem[]> Children = new Dictionary<int, ParticleSystem[]>();

        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            MethodInfo stop = AccessTools.Method(typeof(ParticleSystem), "Stop", new System.Type[0]);
            MethodInfo replacement = AccessTools.Method(typeof(LoadingScreenPatch), nameof(Stop));
            bool replaced = false;
            foreach (CodeInstruction instruction in instructions)
            {
                if (instruction.Calls(stop))
                {
                    replaced = true;
                    yield return new CodeInstruction(OpCodes.Call, replacement);
                    continue;
                }
                yield return instruction;
            }
            if (!replaced)
            {
                Plugin.Log.LogWarning("LoadingScreen: ParticleSystem.Stop() call not found in LoadingScreenManager.Update; not applied.");
            }
        }

        private static void Stop(ParticleSystem part)
        {
            if (Plugin.LoadingScreenCheck.Value && part != null && FullyStopped(part))
            {
                return;
            }
            part.Stop();
        }

        private static bool FullyStopped(ParticleSystem part)
        {
            int id = part.GetInstanceID();
            if (!Children.TryGetValue(id, out ParticleSystem[] all))
            {
                all = part.GetComponentsInChildren<ParticleSystem>(true);
                Children[id] = all;
            }
            for (int i = 0; i < all.Length; i++)
            {
                if (all[i] == null)
                {
                    Children.Remove(id);   // hierarchy changed: look it up again next time
                    return false;
                }
                if (!all[i].isStopped)
                {
                    return false;
                }
            }
            return true;
        }
    }
}
