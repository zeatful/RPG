using System.Collections.Generic;
using RPG.Core;
using UnityEngine;

namespace RPG.World
{
    /// <summary>
    /// The ground: a height function (gentle noise per zone, flat towns, sunken ponds, a mountain ring)
    /// and a flat-shaded low-poly mesh coloured per zone, with dirt roads and cobbled town squares.
    /// </summary>
    public static class WorldTerrain
    {
        public const float Cell = 2.5f;
        public const int ChunkCells = 32;
        public const float WaterLevel = 1.0f;
        const float TownHeight = 1.6f;

        static readonly Color Rock = Hex("#7A746C");
        static readonly Color Snow = Hex("#E6ECF0");
        static readonly Color Basalt = Hex("#34302E");
        static readonly Color Road = Hex("#9C8058");
        static readonly Color AshRoad = Hex("#9A7A5A");
        static readonly Color Cobble = Hex("#8C8476");
        static readonly Color CobbleDark = Hex("#7A7366");
        static readonly Color Mud = Hex("#5E5038");

        static Color Hex(string hex)
        {
            Color c;
            ColorUtility.TryParseHtmlString(hex, out c);
            return c;
        }

        static float Smooth(float edge0, float edge1, float x)
        {
            float t = Mathf.Clamp01((x - edge0) / (edge1 - edge0));
            return t * t * (3f - 2f * t);
        }

        /// <summary>Soft zone weights so zone borders blend over ~15 m.</summary>
        static void Weights(Vector2 p, float[] weights)
        {
            List<ZoneInfo> zones = WorldLayout.Zones;
            float min = float.MaxValue;
            for (int i = 0; i < zones.Count; i++)
            {
                weights[i] = (zones[i].Center - p).magnitude;
                min = Mathf.Min(min, weights[i]);
            }
            float total = 0;
            for (int i = 0; i < zones.Count; i++)
            {
                weights[i] = Mathf.Exp(-(weights[i] - min) / 9f);
                total += weights[i];
            }
            for (int i = 0; i < zones.Count; i++) weights[i] /= total;
        }

        static readonly float[] scratch = new float[16];

        static float BaseHeightOf(ZoneInfo zone)
        {
            switch (zone.Id)
            {
                case "swamp": return 0.9f;
                case "ash": return 2.2f;
                case "hills": return 2.6f;
            }
            return 1.6f;
        }

        public static float Height(float x, float z)
        {
            Vector2 p = new Vector2(x, z);
            Weights(p, scratch);
            float hills = 0, baseHeight = 0;
            for (int i = 0; i < WorldLayout.Zones.Count; i++)
            {
                hills += scratch[i] * WorldLayout.Zones[i].Hills;
                baseHeight += scratch[i] * BaseHeightOf(WorldLayout.Zones[i]);
            }

            float n = (Mathf.PerlinNoise(x * 0.016f + 31.7f, z * 0.016f + 77.1f) - 0.5f) * 2f * 0.65f
                    + (Mathf.PerlinNoise(x * 0.045f + 5.3f, z * 0.045f + 9.9f) - 0.5f) * 2f * 0.35f;
            float h = baseHeight + n * hills;

            // dirt roads are worn a little flatter
            float road = WorldLayout.DistanceToRoad(p);
            h = Mathf.Lerp(h, baseHeight + n * hills * 0.35f, Smooth(9f, 3f, road));

            foreach (TownInfo town in WorldLayout.Towns)
            {
                float d = (town.Center - p).magnitude;
                h = Mathf.Lerp(h, TownHeight, Smooth(TownInfo.FlatRadius + 20f, TownInfo.FlatRadius, d));
            }

            foreach (Pond pond in WorldLayout.Ponds)
            {
                float d = (pond.Center - p).magnitude;
                // raise the surroundings to just above the water, then dig the bowl
                h = Mathf.Lerp(h, Mathf.Clamp(h, WaterLevel + 0.5f, WaterLevel + 2.5f), Smooth(pond.Radius + 16f, pond.Radius + 4f, d));
                h = Mathf.Lerp(h, WaterLevel - 1.8f, Smooth(pond.Radius + 3f, pond.Radius * 0.55f, d));
            }

            float edge = Mathf.Max(Mathf.Abs(x), Mathf.Abs(z));
            if (edge > WorldLayout.MountainStart)
            {
                float k = (edge - WorldLayout.MountainStart) / (WorldLayout.Extent - WorldLayout.MountainStart);
                float ridge = Mathf.PerlinNoise(x * 0.03f + 3f, z * 0.03f + 11f);
                h += k * k * 40f * (0.7f + ridge * 0.6f);
            }
            return h;
        }

        public static Color ColorAt(float x, float z, float h, float normalY)
        {
            Vector2 p = new Vector2(x, z);
            Weights(p, scratch);
            float variation = Mathf.PerlinNoise(x * 0.07f + 13f, z * 0.07f + 3f);
            Color c = Color.black;
            float ash = 0;
            for (int i = 0; i < WorldLayout.Zones.Count; i++)
            {
                ZoneInfo zone = WorldLayout.Zones[i];
                c += Color.Lerp(zone.Ground, zone.GroundAlt, variation) * scratch[i];
                if (zone.Id == "ash") ash = scratch[i];
            }

            // ash: glowing-ember speckle in the ground colour
            if (ash > 0.3f)
            {
                float ember = Mathf.PerlinNoise(x * 0.22f, z * 0.22f);
                if (ember > 0.72f) c = Color.Lerp(c, Hex("#7A3422"), (ember - 0.72f) * 3f * ash);
            }

            // steep ground shows rock
            c = Color.Lerp(c, Rock, Mathf.Clamp01((0.86f - normalY) * 3.5f));

            float edge = Mathf.Max(Mathf.Abs(x), Mathf.Abs(z));
            if (edge > WorldLayout.MountainStart)
            {
                float k = (edge - WorldLayout.MountainStart) / (WorldLayout.Extent - WorldLayout.MountainStart);
                c = Color.Lerp(c, ash > 0.4f ? Basalt : Rock, Mathf.Clamp01(k * 1.6f));
                if (h > 20f) c = Color.Lerp(c, ash > 0.4f ? Hex("#2A2624") : Snow, Smooth(20f, 26f, h));
            }

            foreach (Pond pond in WorldLayout.Ponds)
            {
                float d = (pond.Center - p).magnitude;
                c = Color.Lerp(c, Mud, Smooth(pond.Radius + 4f, pond.Radius + 0.5f, d));
            }

            float road = WorldLayout.DistanceToRoad(p);
            c = Color.Lerp(c, ash > 0.5f ? AshRoad : Road, Smooth(3.4f, 2.0f, road) * 0.9f);

            foreach (TownInfo town in WorldLayout.Towns)
            {
                float d = (town.Center - p).magnitude;
                if (d > TownInfo.FlatRadius + 6f) continue;
                c = Color.Lerp(c, Color.Lerp(c, Road, 0.35f), Smooth(TownInfo.FlatRadius + 4f, TownInfo.FlatRadius - 6f, d));
                bool checker = (Mathf.FloorToInt(x / 1.25f) + Mathf.FloorToInt(z / 1.25f)) % 2 == 0;
                c = Color.Lerp(c, checker ? Cobble : CobbleDark, Smooth(TownInfo.PlazaRadius + 1.5f, TownInfo.PlazaRadius - 0.5f, d));
            }
            return c;
        }

        /// <summary>Builds the ground as flat-shaded chunks with mesh colliders under parent.</summary>
        public static void Build(Transform parent, Material material)
        {
            int cells = Mathf.CeilToInt(WorldLayout.Extent * 2f / Cell);
            int chunks = Mathf.CeilToInt(cells / (float)ChunkCells);
            float origin = -WorldLayout.Extent;

            // heights on the shared grid so chunks line up exactly
            int verts = cells + 1;
            float[,] heights = new float[verts, verts];
            for (int iz = 0; iz < verts; iz++)
                for (int ix = 0; ix < verts; ix++)
                    heights[ix, iz] = Height(origin + ix * Cell, origin + iz * Cell);

            System.Random random = new System.Random(7);
            for (int cz = 0; cz < chunks; cz++)
                for (int cx = 0; cx < chunks; cx++)
                {
                    int x0 = cx * ChunkCells, z0 = cz * ChunkCells;
                    int x1 = Mathf.Min(cells, x0 + ChunkCells), z1 = Mathf.Min(cells, z0 + ChunkCells);
                    List<Vector3> positions = new List<Vector3>();
                    List<Color> colors = new List<Color>();
                    List<int> indices = new List<int>();

                    for (int iz = z0; iz < z1; iz++)
                        for (int ix = x0; ix < x1; ix++)
                        {
                            Vector3 a = new Vector3(origin + ix * Cell, heights[ix, iz], origin + iz * Cell);
                            Vector3 b = new Vector3(origin + (ix + 1) * Cell, heights[ix + 1, iz], origin + iz * Cell);
                            Vector3 c = new Vector3(origin + ix * Cell, heights[ix, iz + 1], origin + (iz + 1) * Cell);
                            Vector3 d = new Vector3(origin + (ix + 1) * Cell, heights[ix + 1, iz + 1], origin + (iz + 1) * Cell);
                            // alternate the diagonal for a less gridded look
                            if (((ix + iz) & 1) == 0)
                            {
                                Triangle(a, c, d, positions, colors, indices, random);
                                Triangle(a, d, b, positions, colors, indices, random);
                            }
                            else
                            {
                                Triangle(a, c, b, positions, colors, indices, random);
                                Triangle(b, c, d, positions, colors, indices, random);
                            }
                        }

                    Mesh mesh = new Mesh();
                    mesh.name = "Ground " + cx + "," + cz;
                    mesh.SetVertices(positions);
                    mesh.SetColors(colors);
                    mesh.SetTriangles(indices, 0);
                    mesh.RecalculateNormals();
                    mesh.RecalculateBounds();

                    GameObject chunk = new GameObject(mesh.name);
                    chunk.transform.SetParent(parent, false);
                    chunk.AddComponent<MeshFilter>().sharedMesh = mesh;
                    MeshRenderer renderer = chunk.AddComponent<MeshRenderer>();
                    renderer.sharedMaterial = material;
                    chunk.AddComponent<MeshCollider>().sharedMesh = mesh;
                    chunk.layer = GroundLayer;
                    chunk.isStatic = true;
                }
        }

        static void Triangle(Vector3 a, Vector3 b, Vector3 c, List<Vector3> positions, List<Color> colors, List<int> indices, System.Random random)
        {
            Vector3 normal = Vector3.Cross(b - a, c - a).normalized;
            Vector3 centre = (a + b + c) / 3f;
            Color color = ColorAt(centre.x, centre.z, centre.y, Mathf.Abs(normal.y));
            float shade = 0.95f + (float)random.NextDouble() * 0.1f;
            color = new Color(color.r * shade, color.g * shade, color.b * shade, 1f);
            int start = positions.Count;
            positions.Add(a); positions.Add(b); positions.Add(c);
            colors.Add(color); colors.Add(color); colors.Add(color);
            indices.Add(start); indices.Add(start + 1); indices.Add(start + 2);
        }

        /// <summary>Exact ground height under a point (raycast against the built mesh), falling back to the formula.</summary>
        public static float GroundY(float x, float z)
        {
            RaycastHit hit;
            if (Physics.Raycast(new Vector3(x, 200f, z), Vector3.down, out hit, 400f, GroundMask)) return hit.point.y;
            return Height(x, z);
        }

        public const int GroundLayer = 8;
        public static int GroundMask { get { return 1 << GroundLayer; } }

        /// <summary>
        /// Colour map of the world for the minimap, turned so that a camera facing yawDegrees sees its
        /// forward direction as "up" (texture v grows along forward, u along the camera's right).
        /// </summary>
        public static Texture2D MapTexture(int size, float yawDegrees)
        {
            float yaw = yawDegrees * Mathf.Deg2Rad;
            Vector2 forward = new Vector2(Mathf.Sin(yaw), Mathf.Cos(yaw));
            Vector2 right = new Vector2(Mathf.Cos(yaw), -Mathf.Sin(yaw));
            float step = WorldLayout.Extent * 2f / size;

            float[] heights = new float[size * size];
            Vector2[] points = new Vector2[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float mx = -WorldLayout.Extent + (x + 0.5f) * step;
                    float my = -WorldLayout.Extent + (y + 0.5f) * step;
                    Vector2 p = right * mx + forward * my;
                    points[y * size + x] = p;
                    heights[y * size + x] = Height(p.x, p.y);
                }

            Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            texture.wrapMode = TextureWrapMode.Clamp;
            Color[] pixels = new Color[size * size];
            Color water = Hex("#3A7A9A");
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    int i = y * size + x;
                    Vector2 p = points[i];
                    float h = heights[i];
                    float dx = heights[y * size + Mathf.Min(size - 1, x + 1)] - h;
                    float dy = heights[Mathf.Min(size - 1, y + 1) * size + x] - h;
                    Vector3 normal = new Vector3(-dx / step, 1f, -dy / step).normalized;
                    Color c = ColorAt(p.x, p.y, h, normal.y);
                    foreach (Pond pond in WorldLayout.Ponds)
                    {
                        if ((pond.Center - p).magnitude < pond.Radius + 0.5f) c = water;
                    }
                    if (WorldLayout.DistanceToRoad(p) < 2.2f) c = Color.Lerp(c, Hex("#C8A878"), 0.6f);
                    float light = Mathf.Clamp(1f + (dy - dx) * 0.35f, 0.75f, 1.2f);
                    pixels[i] = new Color(c.r * light, c.g * light, c.b * light, 1f);
                }
            texture.SetPixels(pixels);
            texture.Apply();
            return texture;
        }
    }
}
