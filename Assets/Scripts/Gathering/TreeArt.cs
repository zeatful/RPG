using RPG.Content;
using RPG.Core;
using UnityEngine;

namespace RPG.Gathering
{
    /// <summary>
    /// Looks for gatherable trees and rocks. Each tree type has its own silhouette and colour:
    /// pine = the pack's fir model in deep blue-green, oak = round autumn-amber canopy,
    /// willow = pale lime with hanging strands. Rocks use the LowPoly rock with glowing ore crystals.
    /// </summary>
    public static class TreeArt
    {
        static readonly Color CutWood = new Color(0.79f, 0.63f, 0.42f);

        public static void Build(NodeInfo info, Transform full, Transform depleted, int seed)
        {
            Color main = RpgUtil.FromHex(info.ColorHex, Color.green);
            Color bark = RpgUtil.FromHex(info.AccentHex, new Color(0.4f, 0.28f, 0.18f));
            System.Random random = new System.Random(seed);

            if (info.Id == "pine") Pine(full, main, bark, random);
            else if (info.Id == "willow") Willow(full, main, bark, random);
            else Oak(full, main, bark, random);

            // stump left behind when chopped
            LowPoly.Part(LowPoly.Prism(7, 0.9f), depleted, Vector3.zero, new Vector3(0.62f, 0.42f, 0.62f), bark);
            LowPoly.Part(LowPoly.Prism(7), depleted, new Vector3(0, 0.42f, 0), new Vector3(0.54f, 0.03f, 0.54f), CutWood);
            LowPoly.Part(LowPoly.Ico(0, 0.2f, seed), depleted, new Vector3(0.5f, 0.05f, 0.2f), new Vector3(0.2f, 0.08f, 0.14f), CutWood);
        }

        static void Pine(Transform parent, Color main, Color bark, System.Random random)
        {
            RpgArt art = RpgArt.Instance;
            GameObject prefab = art == null ? null : random.Next(2) == 0 ? art.FirTreeA : art.FirTreeB;
            if (prefab == null) prefab = art != null ? art.FirTreeA : null;
            if (prefab != null)
            {
                GameObject tree = RpgUtil.FitModel(prefab, new Vector3(0, 5.2f + (float)random.NextDouble() * 0.8f, 0));
                tree.transform.SetParent(parent, false);
                RpgUtil.Recolor(tree, "Fir", main);
                RpgUtil.Recolor(tree, "Trunk", bark);
                return;
            }

            // fallback: stacked cones
            LowPoly.Limb(parent, Vector3.zero, new Vector3(0, 1.4f, 0), 0.22f, bark, 6, 0.8f);
            for (int i = 0; i < 3; i++)
            {
                float y = 1.0f + i * 1.15f;
                float radius = 1.5f - i * 0.4f;
                LowPoly.Part(LowPoly.Cone(7), parent, new Vector3(0, y, 0), new Vector3(radius * 2f, 1.9f, radius * 2f), Shade(main, i * 0.06f));
            }
        }

        static void Oak(Transform parent, Color main, Color bark, System.Random random)
        {
            LowPoly.Part(LowPoly.Prism(6, 0.7f), parent, Vector3.zero, new Vector3(0.6f, 2.5f, 0.6f), bark);
            LowPoly.Limb(parent, new Vector3(0, 1.8f, 0), new Vector3(0.9f, 2.7f, 0.2f), 0.12f, bark, 5, 0.6f);
            LowPoly.Limb(parent, new Vector3(0, 2.0f, 0), new Vector3(-0.8f, 2.9f, -0.3f), 0.11f, bark, 5, 0.6f);

            Vector3[] clumps =
            {
                new Vector3(0, 3.4f, 0), new Vector3(1.0f, 3.0f, 0.3f), new Vector3(-0.9f, 3.1f, -0.2f),
                new Vector3(0.2f, 3.1f, -1.0f), new Vector3(-0.3f, 3.2f, 0.9f), new Vector3(0.1f, 4.3f, 0.1f)
            };
            float[] sizes = { 2.7f, 1.9f, 2.0f, 1.8f, 1.8f, 1.7f };
            for (int i = 0; i < clumps.Length; i++)
            {
                Color color = Color.Lerp(Shade(main, (float)random.NextDouble() * 0.14f), new Color(0.95f, 0.75f, 0.25f), (float)random.NextDouble() * 0.22f);
                LowPoly.Part(LowPoly.Ico(1, 0.18f, random.Next(1000)), parent, clumps[i], Vector3.one * sizes[i], color);
            }
            // a few fallen leaves at the base
            for (int i = 0; i < 5; i++)
            {
                float angle = (float)random.NextDouble() * Mathf.PI * 2f;
                float distance = 0.8f + (float)random.NextDouble() * 1.2f;
                LowPoly.Part(LowPoly.Ico(0), parent, new Vector3(Mathf.Cos(angle) * distance, 0.02f, Mathf.Sin(angle) * distance), new Vector3(0.22f, 0.03f, 0.16f), Shade(main, 0.1f));
            }
        }

        static void Willow(Transform parent, Color main, Color bark, System.Random random)
        {
            LowPoly.Part(LowPoly.Prism(6, 0.75f), parent, Vector3.zero, Quaternion.Euler(4f, 0, 3f), new Vector3(0.55f, 3.0f, 0.55f), bark);
            LowPoly.Part(LowPoly.Ico(1, 0.15f, random.Next(1000)), parent, new Vector3(0, 3.5f, 0), new Vector3(3.4f, 1.5f, 3.4f), main);
            LowPoly.Part(LowPoly.Ico(1, 0.15f, random.Next(1000)), parent, new Vector3(0.1f, 4.2f, 0), new Vector3(2.2f, 1.1f, 2.2f), Color.Lerp(main, Color.white, 0.12f));

            // hanging strands
            int strands = 16;
            for (int i = 0; i < strands; i++)
            {
                float angle = i * Mathf.PI * 2f / strands + (float)random.NextDouble() * 0.2f;
                float radius = 1.25f + (float)random.NextDouble() * 0.35f;
                float length = 1.6f + (float)random.NextDouble() * 0.9f;
                Vector3 from = new Vector3(Mathf.Cos(angle) * radius, 3.3f, Mathf.Sin(angle) * radius);
                Color color = i % 2 == 0 ? Color.Lerp(main, Color.white, 0.18f) : Shade(main, 0.08f);
                LowPoly.Spike(parent, from, Vector3.down, length, 0.2f, color, 4);
            }
        }

        public static void BuildRock(NodeInfo info, Transform full, Transform depleted, int seed)
        {
            Color stone = RpgUtil.FromHex(info.ColorHex, Color.gray);
            Color ore = RpgUtil.FromHex(info.AccentHex, Color.white);
            System.Random random = new System.Random(seed);
            RpgArt art = RpgArt.Instance;

            GameObject prefab = art != null ? (random.Next(2) == 0 ? art.RockA : art.RockC) : null;
            if (prefab == null && art != null) prefab = art.RockA;

            Rock(full, prefab, stone, seed, 1f);
            Rock(depleted, prefab, Shade(stone, 0.35f), seed, 0.8f);

            // glowing ore crystals poking out of the top
            int crystals = 5 + random.Next(3);
            for (int i = 0; i < crystals; i++)
            {
                float angle = (float)random.NextDouble() * Mathf.PI * 2f;
                float distance = 0.15f + (float)random.NextDouble() * 0.45f;
                Vector3 basePoint = new Vector3(Mathf.Cos(angle) * distance, 0.55f + (float)random.NextDouble() * 0.25f, Mathf.Sin(angle) * distance * 0.8f);
                Vector3 direction = (new Vector3(Mathf.Cos(angle) * 0.6f, 1f, Mathf.Sin(angle) * 0.6f)).normalized;
                float length = 0.3f + (float)random.NextDouble() * 0.3f;
                GameObject crystal = LowPoly.Spike(full, basePoint, direction, length, 0.09f + (float)random.NextDouble() * 0.05f, ore, 4);
                crystal.GetComponent<Renderer>().sharedMaterial = LowPoly.Mat(ore, 0.35f);
            }
        }

        static void Rock(Transform parent, GameObject prefab, Color stone, int seed, float size)
        {
            if (prefab != null)
            {
                GameObject rock = RpgUtil.FitModel(prefab, new Vector3(1.7f * size, 1.0f * size, 1.4f * size));
                rock.transform.SetParent(parent, false);
                RpgUtil.Recolor(rock, null, stone);
                return;
            }
            LowPoly.Part(LowPoly.Ico(1, 0.2f, seed), parent, new Vector3(0, 0.45f * size, 0), new Vector3(1.6f, 1.0f, 1.4f) * size, stone);
        }

        static Color Shade(Color color, float amount)
        {
            return Color.Lerp(color, Color.black, amount);
        }
    }
}
