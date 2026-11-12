using System.Collections.Generic;
using UnityEngine;

namespace PerfMod
{
    // Some maps' glowing orbs (e.g. Woods' "BlueLight"/"Fre" lanterns, ~120 particles) draw every particle as
    // Unity's built-in 515-vertex Sphere with the Standard shader. Unity builds mesh particles on the CPU every
    // frame (~60,000 vertices here). Each orb is a few pixels across, so a 42-vertex icosphere of the same size
    // looks the same: measured +~37% FPS on Woods 1 at 3440x1440.
    internal class LowPolyParticles : MonoBehaviour
    {
        private float nextScan;
        private readonly Dictionary<ParticleSystemRenderer, Mesh> swapped = new Dictionary<ParticleSystemRenderer, Mesh>();

        private void LateUpdate()
        {
            float now = Time.realtimeSinceStartup;
            if (now < nextScan)
            {
                return;
            }
            nextScan = now + 1f;   // effects and maps appear over time: a light scan once a second
            if (!Plugin.LowPolyParticles.Value)
            {
                foreach (KeyValuePair<ParticleSystemRenderer, Mesh> kv in swapped)
                {
                    if (kv.Key != null)
                    {
                        kv.Key.mesh = kv.Value;
                    }
                }
                swapped.Clear();
                return;
            }
            foreach (ParticleSystemRenderer renderer in FindObjectsOfType<ParticleSystemRenderer>())
            {
                Mesh mesh = renderer.mesh;
                if (renderer.renderMode == ParticleSystemRenderMode.Mesh && mesh != null && mesh.name == "Sphere" && mesh.vertexCount > 200)
                {
                    swapped[renderer] = mesh;
                    renderer.mesh = LowPoly.Sphere(1);
                }
            }
            if (swapped.Count > 256)
            {
                List<ParticleSystemRenderer> dead = new List<ParticleSystemRenderer>();
                foreach (ParticleSystemRenderer r in swapped.Keys)
                {
                    if (r == null) dead.Add(r);
                }
                foreach (ParticleSystemRenderer r in dead) swapped.Remove(r);
            }
        }
    }

    // An icosphere the size of Unity's built-in Sphere (radius 0.5): subdivisions 1 = 42 vertices, 2 = 162 (vs 515).
    internal static class LowPoly
    {
        static readonly Dictionary<int, Mesh> Cache = new Dictionary<int, Mesh>();

        internal static Mesh Sphere(int subdivisions)
        {
            if (Cache.TryGetValue(subdivisions, out Mesh cached) && cached != null)
                return cached;
            float t = (1f + Mathf.Sqrt(5f)) / 2f;
            var v = new List<Vector3>
            {
                new Vector3(-1, t, 0), new Vector3(1, t, 0), new Vector3(-1, -t, 0), new Vector3(1, -t, 0),
                new Vector3(0, -1, t), new Vector3(0, 1, t), new Vector3(0, -1, -t), new Vector3(0, 1, -t),
                new Vector3(t, 0, -1), new Vector3(t, 0, 1), new Vector3(-t, 0, -1), new Vector3(-t, 0, 1),
            };
            for (int i = 0; i < v.Count; i++) v[i] = v[i].normalized;
            var f = new List<int>
            {
                0,11,5, 0,5,1, 0,1,7, 0,7,10, 0,10,11, 1,5,9, 5,11,4, 11,10,2, 10,7,6, 7,1,8,
                3,9,4, 3,4,2, 3,2,6, 3,6,8, 3,8,9, 4,9,5, 2,4,11, 6,2,10, 8,6,7, 9,8,1,
            };
            for (int s = 0; s < subdivisions; s++)
            {
                var mid = new Dictionary<long, int>();
                var nf = new List<int>();
                System.Func<int, int, int> m = (a, b) =>
                {
                    long key = a < b ? ((long)a << 32) | (uint)b : ((long)b << 32) | (uint)a;
                    if (mid.TryGetValue(key, out int idx)) return idx;
                    v.Add(((v[a] + v[b]) * 0.5f).normalized);
                    mid[key] = v.Count - 1;
                    return v.Count - 1;
                };
                for (int i = 0; i < f.Count; i += 3)
                {
                    int a = f[i], b = f[i + 1], c = f[i + 2];
                    int ab = m(a, b), bc = m(b, c), ca = m(c, a);
                    nf.AddRange(new[] { a, ab, ca, b, bc, ab, c, ca, bc, ab, bc, ca });
                }
                f = nf;
            }
            var normals = new Vector3[v.Count];
            var uv = new Vector2[v.Count];
            var verts = new Vector3[v.Count];
            for (int i = 0; i < v.Count; i++)
            {
                normals[i] = v[i];
                verts[i] = v[i] * 0.5f;
                uv[i] = new Vector2(0.5f + Mathf.Atan2(v[i].z, v[i].x) / (2f * Mathf.PI), 0.5f + Mathf.Asin(v[i].y) / Mathf.PI);
            }
            var mesh = new Mesh { name = "LowPolySphere" + subdivisions, vertices = verts, normals = normals, uv = uv, triangles = f.ToArray() };
            mesh.RecalculateBounds();
            Cache[subdivisions] = mesh;
            return mesh;
        }
    }
}
