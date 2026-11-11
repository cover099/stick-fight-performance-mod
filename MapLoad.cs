using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using UnityEngine.SceneManagement;

namespace PerfMod
{
    // GameManager.LoadMap loads the next map with the synchronous SceneManager.LoadScene (additive), which
    // Unity runs at the start of the next frame: a ~40 ms freeze in the map-change animation. The game then
    // waits for SceneManager.sceneLoaded (OnLevelFinishedLoading) before it touches the map, which the async
    // load raises too, so LoadSceneAsync gives the same sequence with the loading done in the background.
    [HarmonyPatch]
    internal static class MapLoadPatch
    {
        private static MethodBase TargetMethod()
        {
            return AccessTools.EnumeratorMoveNext(AccessTools.Method(typeof(GameManager), "LoadMap"));
        }

        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            MethodInfo sync = AccessTools.Method(typeof(SceneManager), "LoadScene", new[] { typeof(int), typeof(LoadSceneMode) });
            MethodInfo replacement = AccessTools.Method(typeof(MapLoadPatch), nameof(Load));
            bool replaced = false;
            foreach (CodeInstruction instruction in instructions)
            {
                if (instruction.Calls(sync))
                {
                    replaced = true;
                    yield return new CodeInstruction(OpCodes.Call, replacement);
                    continue;
                }
                yield return instruction;
            }
            if (!replaced)
            {
                Plugin.Log.LogWarning("MapLoad: LoadScene call not found in GameManager.LoadMap; not applied.");
            }
        }

        private static void Load(int sceneBuildIndex, LoadSceneMode mode)
        {
            if (Plugin.AsyncMapLoad.Value)
            {
                SceneManager.LoadSceneAsync(sceneBuildIndex, mode);
            }
            else
            {
                SceneManager.LoadScene(sceneBuildIndex, mode);
            }
        }
    }
}
