using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using SteamDataSuite;
using UnityEngine;

namespace PerfMod
{
    // Controller.LateUpdate is an AFK-kick check: in online matches (as client) it calls
    // MultiplayerManager.IsServer, which makes two Steam API calls (lobby owner + own Steam ID), for
    // every player every frame. Steam API calls are not free (and cost more under Proton). The check can never fire:
    // CheckForInput always returns true, so the kick timer is reset every frame. Skipping the method
    // is exactly equivalent (the timer simply stays at 0). Other mods' prefixes on it still run.
    [HarmonyPatch(typeof(Controller), "LateUpdate")]
    internal static class KickCheckPatch
    {
        // Run after other mods' prefixes (QOL-Mod counts time alive/dead here) so skipping can't skip them.
        [HarmonyPriority(Priority.Last)]
        private static bool Prefix()
        {
            return !Plugin.SkipDeadKickCheck.Value;
        }
    }

    // PunchForce.Update re-fetches six component references from its root every frame (10+ body
    // parts per player). They only change if the part is moved under a different root, so refresh
    // only then (or if a cached reference was destroyed).
    [HarmonyPatch(typeof(PunchForce), "Update")]
    internal static class PunchForceCachePatch
    {
        private static readonly AccessTools.FieldRef<PunchForce, float> MeCounter = AccessTools.FieldRefAccess<PunchForce, float>("meCounter");
        private static readonly AccessTools.FieldRef<PunchForce, float> EffectCd = AccessTools.FieldRefAccess<PunchForce, float>("effectCd");
        private static readonly AccessTools.FieldRef<PunchForce, CharacterInformation> Info = AccessTools.FieldRefAccess<PunchForce, CharacterInformation>("info");
        private static readonly AccessTools.FieldRef<PunchForce, Standing> Stand = AccessTools.FieldRefAccess<PunchForce, Standing>("stand");
        private static readonly AccessTools.FieldRef<PunchForce, Controller> Ctrl = AccessTools.FieldRefAccess<PunchForce, Controller>("controller");
        private static readonly AccessTools.FieldRef<PunchForce, Rigidbody> Torso = AccessTools.FieldRefAccess<PunchForce, Rigidbody>("torso");
        private static readonly AccessTools.FieldRef<PunchForce, Rigidbody> Rig = AccessTools.FieldRefAccess<PunchForce, Rigidbody>("rig");

        private static readonly Dictionary<int, Transform> CachedRoot = new Dictionary<int, Transform>();

        private static bool Prefix(PunchForce __instance)
        {
            if (!Plugin.PunchForceCache.Value)
            {
                return true;
            }
            Transform root = __instance.transform.root;
            int id = __instance.GetInstanceID();
            if (CachedRoot.TryGetValue(id, out Transform cached) && cached == root
                && Info(__instance) && Stand(__instance) && Ctrl(__instance) && Torso(__instance) && Rig(__instance))
            {
                MeCounter(__instance) += Time.deltaTime;
                EffectCd(__instance) += Time.deltaTime;
                return false;
            }
            // First frame, new root, or something was destroyed: let vanilla do the full lookup.
            if (CachedRoot.Count > 2048)
            {
                CachedRoot.Clear(); // drop entries of destroyed players/weapons
            }
            CachedRoot[id] = root;
            return true;
        }
    }

    // In local play and menus, ControllerHandler.Update checks every frame whether any keybind is
    // pressed, which reads all 16 keybinds from PlayerPrefs, i.e. the registry on Windows and the
    // emulated registry under Proton/Wine, so that is slow. Cache each lookup; clear the cache
    // whenever bindings are written.
    [HarmonyPatch(typeof(CharacterActions), nameof(CharacterActions.GetKeybinding))]
    internal static class KeybindCachePatch
    {
        internal static readonly Dictionary<string, KeybindingInfo> Cache = new Dictionary<string, KeybindingInfo>();

        private static bool Prefix(string pref, ref KeybindingInfo __result)
        {
            return !Plugin.KeybindCache.Value || !Cache.TryGetValue(pref, out __result);
        }

        private static void Postfix(string pref, KeybindingInfo __result)
        {
            if (Plugin.KeybindCache.Value && __result.key != -1)
            {
                Cache[pref] = __result;
            }
        }
    }

    [HarmonyPatch]
    internal static class KeybindCacheInvalidatePatch
    {
        private static IEnumerable<MethodBase> TargetMethods()
        {
            yield return AccessTools.Method(typeof(CharacterActions), "UpdateKeyInPref");
            yield return AccessTools.Method(typeof(CharacterActions), nameof(CharacterActions.ResetKeybindings));
            yield return AccessTools.Method(typeof(SteamDataAttribution), "ClearAllPlayerprefs");
        }

        private static void Postfix()
        {
            KeybindCachePatch.Cache.Clear();
        }
    }

    // Optional: the game polls Steam for incoming packets on ~10 channels every frame. At very high
    // FPS that is most of the network cost. With a poll rate set, polling happens at most that many
    // times per second; packets wait in Steam's queue and are all drained on the next poll.
    // Costs up to 1/rate seconds of extra latency on incoming packets (e.g. 500 Hz -> max 2 ms).
    [HarmonyPatch]
    internal static class NetworkPollPatch
    {
        private static int frame = -1;
        private static bool allowed = true;
        private static float lastPoll;

        private static IEnumerable<MethodBase> TargetMethods()
        {
            yield return AccessTools.Method(typeof(NetworkPlayer), "ListenForPositionPackages");
            yield return AccessTools.Method(typeof(NetworkPlayer), "ListenForEventPackages");
            yield return AccessTools.Method(typeof(NetworkPlayer), "ListenForDeadPackages");
            yield return AccessTools.Method(typeof(NetworkPlayer), "ListenForChatMessages");
            yield return AccessTools.Method(typeof(SyncableObjectManager), "ListenForPackages");
            yield return AccessTools.Method(typeof(P2PPackageHandler), "CheckForPackagesOnChannel");
        }

        private static bool Prefix()
        {
            float rate = Plugin.NetworkPollRate.Value;
            if (rate <= 0f)
            {
                return true;
            }
            // One decision per frame, shared by every listener, so all channels stay in step.
            if (frame != Time.frameCount)
            {
                frame = Time.frameCount;
                float now = Time.realtimeSinceStartup;
                allowed = now - lastPoll >= 1f / rate;
                if (allowed)
                {
                    lastPoll = now;
                }
            }
            return allowed;
        }
    }
}
