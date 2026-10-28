using HarmonyLib;
using UnityEngine;

namespace PerfMod
{
    // Object.FindObjectsOfType scans every object in the scene and allocates a new array each call.
    // Share one scan per rendered frame between all callers; within a frame the result is the same.
    internal static class FrameCache<T> where T : Object
    {
        private static T[] items = new T[0];
        private static int frame = -1;

        internal static T[] Get()
        {
            if (frame != Time.frameCount)
            {
                items = Object.FindObjectsOfType<T>();
                frame = Time.frameCount;
            }
            return items;
        }
    }

    // Vanilla DynamicCam.Update calls FindObjectsOfType<Hip>() 2 + playerCount times per frame
    // (once for a count check, once per loop, including once per outer iteration of a nested loop),
    // plus once more per physics step in FixedUpdate. Same logic, one scan per frame.
    [HarmonyPatch(typeof(DynamicCam))]
    internal static class DynamicCamPatch
    {
        private static readonly AccessTools.FieldRef<DynamicCam, Camera> Cam = AccessTools.FieldRefAccess<DynamicCam, Camera>("cam");

        [HarmonyPrefix]
        [HarmonyPatch("Update")]
        private static bool UpdatePrefix(DynamicCam __instance)
        {
            if (!Plugin.CameraOptimization.Value)
            {
                return true;
            }
            Hip[] hips = FrameCache<Hip>.Get();
            if (hips.Length < 2)
            {
                return false;
            }
            float maxDistance = 0f;
            for (int i = 0; i < hips.Length; i++)
            {
                if (!hips[i])
                {
                    continue;
                }
                Vector3 a = hips[i].transform.position * hips[i].cameraImportance;
                for (int j = 0; j < hips.Length; j++)
                {
                    if (!hips[j])
                    {
                        continue;
                    }
                    float distance = Vector3.Distance(a, hips[j].transform.position * hips[j].cameraImportance);
                    if (maxDistance < distance)
                    {
                        maxDistance = distance;
                    }
                }
            }
            Camera cam = Cam(__instance);
            if (GameManager.inFight)
            {
                cam.orthographicSize = Mathf.Lerp(cam.orthographicSize, Mathf.Clamp(5f + maxDistance / 5f, 0f, 10f), Time.deltaTime * 2f);
            }
            else
            {
                cam.orthographicSize = Mathf.Lerp(cam.orthographicSize, 10f, Time.deltaTime * 2f);
            }
            return false;
        }

        [HarmonyPrefix]
        [HarmonyPatch("FixedUpdate")]
        private static bool FixedUpdatePrefix(DynamicCam __instance)
        {
            if (!Plugin.CameraOptimization.Value)
            {
                return true;
            }
            float allowed = __instance.cameraAllowedMovement.Evaluate(Cam(__instance).orthographicSize);
            Vector3 target = Vector3.zero;
            float totalImportance = 0f;
            Hip[] hips = FrameCache<Hip>.Get();
            for (int i = 0; i < hips.Length; i++)
            {
                if (!hips[i])
                {
                    continue;
                }
                target += (hips[i].transform.position - Vector3.up) * allowed * hips[i].cameraImportance;
                totalImportance += hips[i].cameraImportance;
            }
            target /= totalImportance;
            target.x = -5f;
            Transform transform = __instance.transform;
            Vector3 position = transform.position;
            position.x = -5f;
            if (GameManager.inFight)
            {
                transform.position = Vector3.Lerp(position, target, Time.deltaTime * 2f);
            }
            else
            {
                transform.position = Vector3.Lerp(position, Vector3.right * -5f, Time.deltaTime * 2f);
            }
            return false;
        }
    }

    // Moving platforms: vanilla scans the scene for every Controller on every physics step.
    [HarmonyPatch(typeof(MovePlayers), "FixedUpdate")]
    internal static class MovePlayersPatch
    {
        private static readonly AccessTools.FieldRef<MovePlayers, Vector3> LastPosition = AccessTools.FieldRefAccess<MovePlayers, Vector3>("lastPosition");

        private static bool Prefix(MovePlayers __instance)
        {
            if (!Plugin.CameraOptimization.Value)
            {
                return true;
            }
            Vector3 position = __instance.transform.position;
            ref Vector3 lastPosition = ref LastPosition(__instance);
            if (GameManager.inFight)
            {
                Controller[] controllers = FrameCache<Controller>.Get();
                for (int i = 0; i < controllers.Length; i++)
                {
                    if (controllers[i])
                    {
                        controllers[i].transform.position += position - lastPosition;
                    }
                }
            }
            lastPosition = position;
            return false;
        }
    }
}
