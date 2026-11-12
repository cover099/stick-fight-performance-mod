using System.Collections.Generic;
using UnityEngine;

namespace PerfMod
{
    // Optional low-detail switches (all off by default): hide things that are only decoration. Measured on Woods 1
    // at 3440x1440 (with the other fixes): decor ~+5%, sprites ~+2%, HUD canvases up to ~+6%; map particles depend on
    // the map. Players, weapons and anything you can stand on or hit are never hidden. A light scan twice a second
    // catches new maps and spawned effects; turning an option off shows everything it hid again.
    internal class LowDetail : MonoBehaviour
    {
        private enum Kind { Decor, Sprites, MapParticles, FightEffects, Hud }

        private readonly Dictionary<Component, Kind> hidden = new Dictionary<Component, Kind>();
        private readonly HashSet<int> checkedIds = new HashSet<int>();
        private float nextScan;

        private static bool On(Kind kind)
        {
            switch (kind)
            {
                case Kind.Decor: return Plugin.HideDecor.Value;
                case Kind.Sprites: return Plugin.HideSprites.Value;
                case Kind.MapParticles: return Plugin.HideMapParticles.Value;
                case Kind.FightEffects: return Plugin.HideFightEffects.Value;
                default: return Plugin.HideHUD.Value;
            }
        }

        private void LateUpdate()
        {
            float now = Time.realtimeSinceStartup;
            if (now < nextScan)
            {
                return;
            }
            nextScan = now + 0.5f;
            Restore();
            bool decor = Plugin.HideDecor.Value, sprites = Plugin.HideSprites.Value, map = Plugin.HideMapParticles.Value,
                 fight = Plugin.HideFightEffects.Value, hud = Plugin.HideHUD.Value;
            if (!decor && !sprites && !map && !fight && !hud)
            {
                return;
            }
            if (decor)
            {
                foreach (MeshRenderer r in FindObjectsOfType<MeshRenderer>())
                {
                    if (r.enabled && IsScenery(r))
                    {
                        Hide(r, Kind.Decor);
                    }
                }
            }
            if (sprites)
            {
                foreach (SpriteRenderer r in FindObjectsOfType<SpriteRenderer>())
                {
                    if (r.enabled && IsScenery(r))
                    {
                        Hide(r, Kind.Sprites);
                    }
                }
            }
            if (map || fight)
            {
                foreach (ParticleSystemRenderer r in FindObjectsOfType<ParticleSystemRenderer>())
                {
                    if (!r.enabled)
                    {
                        continue;
                    }
                    bool onPlayer = IsPlayer(r);
                    if (onPlayer && fight)
                    {
                        Hide(r, Kind.FightEffects);
                    }
                    else if (!onPlayer && map && r.GetComponentInParent<Rigidbody>() == null && r.transform.root.GetComponent<Camera>() == null
                             && r.transform.root.name != "RigidCam")
                    {
                        Hide(r, Kind.MapParticles);   // (not weapon effects like muzzle flashes, not the camera's own effects)
                    }
                    else if (!onPlayer && fight && r.GetComponentInParent<Rigidbody>() != null)
                    {
                        Hide(r, Kind.FightEffects);   // weapon and projectile effects
                    }
                }
            }
            if (hud)
            {
                foreach (Canvas c in FindObjectsOfType<Canvas>())
                {
                    if (c.enabled && c.isRootCanvas && c.name == "GameCanvas" || c.enabled && c.isRootCanvas && c.name == "Canvas" && IsPlayer(c))
                    {
                        Hide(c, Kind.Hud);
                    }
                }
            }
        }

        // Scenery: not part of a player, no Rigidbody (weapons, props) and no collider on it or above it (platforms,
        // walls, anything you can touch draw through a collider object or its children).
        private bool IsScenery(Renderer r)
        {
            int id = r.GetInstanceID();
            if (checkedIds.Contains(id))
            {
                return false;   // looked at before and kept
            }
            bool scenery = !IsPlayer(r) && r.GetComponentInParent<Rigidbody>() == null && r.GetComponentInParent<Collider>() == null
                && r.GetComponentInChildren<Collider>() == null;
            if (!scenery)
            {
                if (checkedIds.Count > 20000)
                {
                    checkedIds.Clear();
                }
                checkedIds.Add(id);
            }
            return scenery;
        }

        private static bool IsPlayer(Component c)
        {
            return c.transform.root.GetComponentInChildren<Controller>(true) != null;
        }

        private void Hide(Component c, Kind kind)
        {
            if (c is Renderer r)
            {
                r.enabled = false;
            }
            else if (c is Behaviour b)
            {
                b.enabled = false;
            }
            hidden[c] = kind;
        }

        // Show again whatever an option that is now off had hidden.
        private void Restore()
        {
            if (hidden.Count == 0)
            {
                return;
            }
            List<Component> back = null;
            foreach (KeyValuePair<Component, Kind> kv in hidden)
            {
                if (kv.Key == null || !On(kv.Value))
                {
                    (back ?? (back = new List<Component>())).Add(kv.Key);
                }
            }
            if (back == null)
            {
                return;
            }
            foreach (Component c in back)
            {
                hidden.Remove(c);
                if (c == null)
                {
                    continue;
                }
                if (c is Renderer r)
                {
                    r.enabled = true;
                }
                else if (c is Behaviour b)
                {
                    b.enabled = true;
                }
            }
        }
    }
}
