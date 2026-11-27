using System.Collections.Generic;
using UnityEngine;

namespace RPG.Core
{
    /// <summary>
    /// Flat-shaded low-poly meshes (cones, prisms, rocky icospheres) and matte materials, used to build
    /// creatures, trees and crystals in code so they match the faceted style of the Synty/LowPoly packs.
    /// Every mesh is unit-sized (radius 0.5, height 1, base at y = 0) and cached.
    /// </summary>
    public static class LowPoly
    {
        static Material baseMaterial;
        static readonly Dictionary<string, Mesh> meshes = new Dictionary<string, Mesh>();
        static readonly Dictionary<string, Material> materials = new Dictionary<string, Material>();

        static Material BaseMaterial
        {
            get
            {
                if (baseMaterial == null)
                {
                    GameObject temp = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    baseMaterial = new Material(temp.GetComponent<Renderer>().sharedMaterial);
                    Object.Destroy(temp);
                    if (baseMaterial.HasProperty("_Glossiness")) baseMaterial.SetFloat("_Glossiness", 0.12f);
                }
                return baseMaterial;
            }
        }

        /// <summary>A shared matte material of this colour, optionally glowing.</summary>
        public static Material Mat(Color color, float glow = 0f)
        {
            string key = ColorUtility.ToHtmlStringRGBA(color) + "/" + glow.ToString("0.00");
            Material material;
            if (materials.TryGetValue(key, out material) && material != null) return material;
            material = new Material(BaseMaterial);
            material.color = color;
            material.enableInstancing = true;
            if (glow > 0f && material.HasProperty("_EmissionColor"))
            {
                material.EnableKeyword("_EMISSION");
                material.SetColor("_EmissionColor", color * glow);
            }
            materials[key] = material;
            return material;
        }

        /// <summary>Child object showing a mesh, without a collider.</summary>
        public static GameObject Part(Mesh mesh, Transform parent, Vector3 position, Quaternion rotation, Vector3 scale, Color color, float glow = 0f)
        {
            GameObject part = new GameObject(mesh.name);
            part.transform.SetParent(parent, false);
            part.transform.localPosition = position;
            part.transform.localRotation = rotation;
            part.transform.localScale = scale;
            part.AddComponent<MeshFilter>().sharedMesh = mesh;
            part.AddComponent<MeshRenderer>().sharedMaterial = Mat(color, glow);
            return part;
        }

        public static GameObject Part(Mesh mesh, Transform parent, Vector3 position, Vector3 scale, Color color, float glow = 0f)
        {
            return Part(mesh, parent, position, Quaternion.identity, scale, color, glow);
        }

        /// <summary>A cone/spike from <paramref name="from"/> pointing along <paramref name="direction"/>.</summary>
        public static GameObject Spike(Transform parent, Vector3 from, Vector3 direction, float length, float radius, Color color, int sides = 5)
        {
            return Part(Cone(sides), parent, from, Quaternion.FromToRotation(Vector3.up, direction.normalized), new Vector3(radius * 2f, length, radius * 2f), color);
        }

        /// <summary>A prism (cylinder) from a to b.</summary>
        public static GameObject Limb(Transform parent, Vector3 a, Vector3 b, float radius, Color color, int sides = 6, float taper = 1f)
        {
            Vector3 direction = b - a;
            return Part(Prism(sides, taper), parent, a, Quaternion.FromToRotation(Vector3.up, direction.normalized), new Vector3(radius * 2f, direction.magnitude, radius * 2f), color);
        }

        public static Mesh Cone(int sides)
        {
            string key = "cone" + sides;
            Mesh mesh;
            if (meshes.TryGetValue(key, out mesh)) return mesh;
            List<Vector3> tris = new List<Vector3>();
            Vector3 apex = new Vector3(0, 1, 0);
            for (int i = 0; i < sides; i++)
            {
                Vector3 a = Ring(i, sides, 0.5f, 0f);
                Vector3 b = Ring(i + 1, sides, 0.5f, 0f);
                tris.Add(a); tris.Add(apex); tris.Add(b);
                tris.Add(Vector3.zero); tris.Add(a); tris.Add(b);
            }
            return Store(key, tris);
        }

        public static Mesh Prism(int sides, float taper = 1f)
        {
            string key = "prism" + sides + "_" + taper.ToString("0.00");
            Mesh mesh;
            if (meshes.TryGetValue(key, out mesh)) return mesh;
            List<Vector3> tris = new List<Vector3>();
            Vector3 top = new Vector3(0, 1, 0);
            for (int i = 0; i < sides; i++)
            {
                Vector3 a = Ring(i, sides, 0.5f, 0f);
                Vector3 b = Ring(i + 1, sides, 0.5f, 0f);
                Vector3 c = Ring(i, sides, 0.5f * taper, 1f);
                Vector3 d = Ring(i + 1, sides, 0.5f * taper, 1f);
                tris.Add(a); tris.Add(c); tris.Add(b);
                tris.Add(b); tris.Add(c); tris.Add(d);
                tris.Add(Vector3.zero); tris.Add(a); tris.Add(b);
                tris.Add(top); tris.Add(d); tris.Add(c);
            }
            return Store(key, tris);
        }

        /// <summary>Axis-aligned box: x and z from -0.5 to 0.5, y from 0 to 1.</summary>
        public static Mesh Block()
        {
            const string key = "block";
            Mesh mesh;
            if (meshes.TryGetValue(key, out mesh)) return mesh;
            List<Vector3> tris = new List<Vector3>();
            Vector3[] ring = { new Vector3(0.5f, 0, 0.5f), new Vector3(-0.5f, 0, 0.5f), new Vector3(-0.5f, 0, -0.5f), new Vector3(0.5f, 0, -0.5f) };
            Vector3 up = Vector3.up;
            Vector3 top = new Vector3(0, 1, 0);
            for (int i = 0; i < 4; i++)
            {
                Vector3 a = ring[i], b = ring[(i + 1) % 4];
                Vector3 c = a + up, d = b + up;
                tris.Add(a); tris.Add(c); tris.Add(b);
                tris.Add(b); tris.Add(c); tris.Add(d);
                tris.Add(Vector3.zero); tris.Add(a); tris.Add(b);
                tris.Add(top); tris.Add(d); tris.Add(c);
            }
            return Store(key, tris);
        }

        /// <summary>Icosphere of radius 0.5 centred on the origin. jitter &gt; 0 makes it lumpy (rocks, foliage).</summary>
        public static Mesh Ico(int subdivisions, float jitter = 0f, int seed = 0)
        {
            string key = "ico" + subdivisions + "_" + jitter.ToString("0.00") + "_" + seed;
            Mesh mesh;
            if (meshes.TryGetValue(key, out mesh)) return mesh;

            float t = (1f + Mathf.Sqrt(5f)) / 2f;
            List<Vector3> verts = new List<Vector3>
            {
                new Vector3(-1, t, 0), new Vector3(1, t, 0), new Vector3(-1, -t, 0), new Vector3(1, -t, 0),
                new Vector3(0, -1, t), new Vector3(0, 1, t), new Vector3(0, -1, -t), new Vector3(0, 1, -t),
                new Vector3(t, 0, -1), new Vector3(t, 0, 1), new Vector3(-t, 0, -1), new Vector3(-t, 0, 1)
            };
            int[] faces =
            {
                0, 11, 5, 0, 5, 1, 0, 1, 7, 0, 7, 10, 0, 10, 11, 1, 5, 9, 5, 11, 4, 11, 10, 2, 10, 7, 6, 7, 1, 8,
                3, 9, 4, 3, 4, 2, 3, 2, 6, 3, 6, 8, 3, 8, 9, 4, 9, 5, 2, 4, 11, 6, 2, 10, 8, 6, 7, 9, 8, 1
            };
            List<int> indices = new List<int>(faces);
            for (int s = 0; s < subdivisions; s++)
            {
                Dictionary<long, int> midpoints = new Dictionary<long, int>();
                List<int> next = new List<int>();
                for (int i = 0; i < indices.Count; i += 3)
                {
                    int a = indices[i], b = indices[i + 1], c = indices[i + 2];
                    int ab = Midpoint(a, b, verts, midpoints);
                    int bc = Midpoint(b, c, verts, midpoints);
                    int ca = Midpoint(c, a, verts, midpoints);
                    next.AddRange(new[] { a, ab, ca, b, bc, ab, c, ca, bc, ab, bc, ca });
                }
                indices = next;
            }

            System.Random random = new System.Random(seed);
            for (int i = 0; i < verts.Count; i++)
            {
                float radius = 0.5f * (1f + (float)(random.NextDouble() * 2 - 1) * jitter);
                verts[i] = verts[i].normalized * radius;
            }

            List<Vector3> tris = new List<Vector3>();
            foreach (int index in indices) tris.Add(verts[index]);
            return Store(key, tris);
        }

        static int Midpoint(int a, int b, List<Vector3> verts, Dictionary<long, int> cache)
        {
            long key = a < b ? ((long)a << 32) + b : ((long)b << 32) + a;
            int index;
            if (cache.TryGetValue(key, out index)) return index;
            verts.Add((verts[a] + verts[b]) * 0.5f);
            index = verts.Count - 1;
            cache[key] = index;
            return index;
        }

        static Vector3 Ring(int i, int sides, float radius, float y)
        {
            float angle = (i % sides) * Mathf.PI * 2f / sides;
            return new Vector3(Mathf.Cos(angle) * radius, y, Mathf.Sin(angle) * radius);
        }

        // Each triangle gets its own vertices so normals are per-face: the faceted low-poly look.
        static Mesh Store(string key, List<Vector3> tris)
        {
            Mesh mesh = new Mesh();
            mesh.name = key;
            mesh.SetVertices(tris);
            int[] triangles = new int[tris.Count];
            // Triangles are listed so cross(v1 - v0, v2 - v0) points outward, which is Unity's front face.
            for (int i = 0; i < triangles.Length; i++) triangles[i] = i;
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            meshes[key] = mesh;
            return mesh;
        }
    }
}
