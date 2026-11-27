using RPG.Core;
using UnityEngine;

namespace RPG.World
{
    /// <summary>
    /// Low-poly props built in code (bank chest, shop stall, furnace, anvil, lamp posts, tents, dead trees)
    /// plus helpers to place Synty models with a solid box collider and to make townsfolk NPCs.
    /// Everything is built at the origin facing +z, then placed by the caller.
    /// </summary>
    public static class Props
    {
        static readonly Color Wood = RpgUtil.FromHex("#6B4A2B", Color.gray);
        static readonly Color WoodDark = RpgUtil.FromHex("#4A321E", Color.gray);
        static readonly Color Gold = RpgUtil.FromHex("#D9B23A", Color.yellow);
        static readonly Color Stone = RpgUtil.FromHex("#8A8580", Color.gray);
        static readonly Color StoneDark = RpgUtil.FromHex("#5E5A56", Color.gray);
        static readonly Color Iron = RpgUtil.FromHex("#3A3C40", Color.gray);
        static readonly Color Fire = RpgUtil.FromHex("#FF8A2A", Color.yellow);

        public static void Place(GameObject go, Transform parent, Vector3 position, float yaw)
        {
            go.transform.SetParent(parent, true);
            go.transform.position = position;
            go.transform.rotation = Quaternion.Euler(0, yaw, 0);
        }

        static BoxCollider Box(GameObject go, Vector3 center, Vector3 size)
        {
            BoxCollider box = go.AddComponent<BoxCollider>();
            box.center = center;
            box.size = size;
            return box;
        }

        /// <summary>A model fitted to a height with a box collider around it (built at the origin).</summary>
        public static GameObject Model(GameObject prefab, float height, bool solid)
        {
            if (prefab == null) return null;
            GameObject root = RpgUtil.FitModel(prefab, new Vector3(0, height, 0));
            if (!solid) return root;
            Bounds bounds = new Bounds();
            bool first = true;
            foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>())
            {
                if (first) { bounds = renderer.bounds; first = false; }
                else bounds.Encapsulate(renderer.bounds);
            }
            if (!first) Box(root, bounds.center, bounds.size * 0.92f);
            return root;
        }

        public static GameObject BankChest()
        {
            GameObject root = new GameObject("Bank chest");
            Transform t = root.transform;
            LowPoly.Part(LowPoly.Block(), t, new Vector3(0, 0, 0), Quaternion.Euler(0, 0, 0), new Vector3(1.6f, 0.7f, 1.0f), WoodDark);
            LowPoly.Part(LowPoly.Block(), t, new Vector3(0, 0.7f, 0), Quaternion.Euler(0, 0, 0), new Vector3(1.62f, 0.08f, 1.02f), Gold);
            // rounded lid
            LowPoly.Part(LowPoly.Prism(8), t, new Vector3(-0.57f, 0.78f, 0), Quaternion.Euler(0, 0, -90), new Vector3(0.7f, 1.14f, 0.7f), Wood);
            foreach (float x in new[] { -0.4f, 0.4f })
            {
                LowPoly.Part(LowPoly.Block(), t, new Vector3(x, 0, 0), Quaternion.Euler(0, 0, 0), new Vector3(0.1f, 1.08f, 1.04f), Gold);
            }
            LowPoly.Part(LowPoly.Block(), t, new Vector3(0, 0.45f, 0.37f), Quaternion.Euler(0, 0, 0), new Vector3(0.18f, 0.24f, 0.08f), Gold, 0.4f);
            Box(root, new Vector3(0, 0.55f, 0), new Vector3(1.2f, 1.1f, 0.8f));
            return root;
        }

        public static GameObject Stall(Color awning)
        {
            GameObject root = new GameObject("Shop stall");
            Transform t = root.transform;
            Color stripe = Color.Lerp(awning, Color.white, 0.75f);
            // counter
            LowPoly.Part(LowPoly.Block(), t, Vector3.zero, Quaternion.Euler(0, 0, 0), new Vector3(3.6f, 1.0f, 1.2f), Wood);
            LowPoly.Part(LowPoly.Block(), t, new Vector3(0, 1.0f, 0), Quaternion.Euler(0, 0, 0), new Vector3(3.8f, 0.08f, 1.4f), WoodDark);
            // posts and striped awning
            foreach (Vector3 post in new[] { new Vector3(-1.25f, 0, -0.45f), new Vector3(1.25f, 0, -0.45f), new Vector3(-1.25f, 0, -1.9f), new Vector3(1.25f, 0, -1.9f) })
            {
                LowPoly.Limb(t, post, post + Vector3.up * 2.6f, 0.06f, WoodDark, 5);
            }
            for (int i = 0; i < 6; i++)
            {
                float x = -1.4f + i * 0.56f + 0.28f;
                LowPoly.Part(LowPoly.Block(), t, new Vector3(x, 2.55f, -1.2f), Quaternion.Euler(-14f, 0, 0), new Vector3(0.56f, 0.06f, 2.2f), i % 2 == 0 ? awning : stripe);
            }
            // goods on the counter
            LowPoly.Part(LowPoly.Ico(0, 0.1f, 1), t, new Vector3(-0.9f, 1.15f, 0), new Vector3(0.3f, 0.22f, 0.3f), RpgUtil.FromHex("#C98B4A", Color.yellow));
            LowPoly.Part(LowPoly.Ico(0, 0.1f, 2), t, new Vector3(-0.55f, 1.13f, 0.1f), new Vector3(0.24f, 0.18f, 0.24f), RpgUtil.FromHex("#F2C94C", Color.yellow));
            LowPoly.Limb(t, new Vector3(0.6f, 1.08f, 0), new Vector3(1.2f, 1.2f, 0.05f), 0.05f, RpgUtil.FromHex("#C8CDD2", Color.white), 4);
            LowPoly.Part(LowPoly.Prism(6), t, new Vector3(0.2f, 1.04f, 0.1f), new Vector3(0.2f, 0.3f, 0.2f), RpgUtil.FromHex("#B8433E", Color.red));
            Box(root, new Vector3(0, 0.6f, 0), new Vector3(3.6f, 1.2f, 1.2f));
            return root;
        }

        public static GameObject Furnace()
        {
            GameObject root = new GameObject("Furnace");
            Transform t = root.transform;
            LowPoly.Part(LowPoly.Prism(6, 0.85f), t, Vector3.zero, new Vector3(2.2f, 1.8f, 2.0f), Stone);
            LowPoly.Part(LowPoly.Prism(6, 0.6f), t, new Vector3(0, 1.8f, -0.1f), new Vector3(1.1f, 1.6f, 1.1f), StoneDark);
            LowPoly.Part(LowPoly.Prism(6), t, new Vector3(0, 3.35f, -0.1f), new Vector3(0.8f, 0.15f, 0.8f), Stone);
            // glowing mouth on the front
            LowPoly.Part(LowPoly.Block(), t, new Vector3(0, 0.7f, 0.8f), Quaternion.Euler(90, 0, 0), new Vector3(0.9f, 0.14f, 0.75f), Fire, 2.2f);
            LowPoly.Part(LowPoly.Block(), t, new Vector3(0, 1.14f, 0.82f), Quaternion.Euler(90, 0, 0), new Vector3(1.1f, 0.16f, 0.14f), StoneDark);
            for (int i = 0; i < 6; i++)
            {
                float a = i * Mathf.PI / 3f;
                LowPoly.Part(LowPoly.Ico(0, 0.25f, i), t, new Vector3(Mathf.Cos(a) * 1.05f, 0.1f, Mathf.Sin(a) * 0.95f), new Vector3(0.35f, 0.25f, 0.3f), StoneDark);
            }
            GameObject lightObject = new GameObject("Glow");
            lightObject.transform.SetParent(t, false);
            lightObject.transform.localPosition = new Vector3(0, 0.6f, 1.4f);
            Light light = lightObject.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = new Color(1f, 0.55f, 0.25f);
            light.range = 6f;
            light.intensity = 1.4f;
            Box(root, new Vector3(0, 1.0f, 0), new Vector3(2.0f, 2.0f, 1.8f));
            return root;
        }

        public static GameObject Anvil()
        {
            GameObject root = new GameObject("Anvil");
            Transform t = root.transform;
            LowPoly.Part(LowPoly.Prism(7, 0.9f), t, Vector3.zero, new Vector3(0.8f, 0.55f, 0.8f), Wood);
            LowPoly.Part(LowPoly.Prism(7), t, new Vector3(0, 0.55f, 0), new Vector3(0.66f, 0.03f, 0.66f), RpgUtil.FromHex("#C9A06A", Color.yellow));
            LowPoly.Part(LowPoly.Block(), t, new Vector3(0, 0.58f, 0), Quaternion.Euler(0, 0, 0), new Vector3(0.62f, 0.14f, 0.42f), Iron);
            LowPoly.Part(LowPoly.Block(), t, new Vector3(0, 0.72f, 0), Quaternion.Euler(0, 0, 0), new Vector3(0.3f, 0.2f, 0.26f), Iron);
            LowPoly.Part(LowPoly.Block(), t, new Vector3(0, 0.92f, 0), Quaternion.Euler(0, 0, 0), new Vector3(0.95f, 0.16f, 0.34f), Iron);
            LowPoly.Spike(t, new Vector3(0.33f, 1.0f, 0), Vector3.right, 0.4f, 0.1f, Iron, 4);
            // a hammer resting on top
            LowPoly.Limb(t, new Vector3(-0.15f, 1.1f, 0.1f), new Vector3(-0.15f, 1.1f, -0.4f), 0.025f, WoodDark, 4);
            LowPoly.Part(LowPoly.Block(), t, new Vector3(-0.15f, 1.04f, 0.14f), Quaternion.Euler(0, 0, 0), new Vector3(0.16f, 0.12f, 0.16f), StoneDark);
            Box(root, new Vector3(0, 0.55f, 0), new Vector3(1.0f, 1.1f, 0.8f));
            return root;
        }

        /// <summary>
        /// The spawn point: a glowing rune circle ringed by standing stones. You start here and wake up
        /// here after dying. No colliders, so you can stand in it.
        /// </summary>
        public static GameObject SpawnShrine()
        {
            GameObject root = new GameObject("Spawn point");
            Transform t = root.transform;
            Color rune = RpgUtil.FromHex("#7FE0FF", Color.cyan);
            LowPoly.Part(LowPoly.Prism(24), t, new Vector3(0, 0.02f, 0), new Vector3(4.2f, 0.04f, 4.2f), Stone);
            LowPoly.Part(LowPoly.Prism(24), t, new Vector3(0, 0.04f, 0), new Vector3(3.6f, 0.03f, 3.6f), rune, 1.6f);
            LowPoly.Part(LowPoly.Prism(24), t, new Vector3(0, 0.05f, 0), new Vector3(3.1f, 0.03f, 3.1f), StoneDark);
            for (int i = 0; i < 6; i++)
            {
                float a = i * Mathf.PI / 3f;
                Vector3 p = new Vector3(Mathf.Cos(a) * 2.6f, 0, Mathf.Sin(a) * 2.6f);
                LowPoly.Part(LowPoly.Prism(5, 0.7f), t, p, Quaternion.Euler(0, i * 37f, 0), new Vector3(0.45f, 1.3f + (i % 2) * 0.35f, 0.4f), Stone);
                LowPoly.Part(LowPoly.Ico(0), t, p + Vector3.up * (1.45f + (i % 2) * 0.35f), Vector3.one * 0.18f, rune, 2f);
            }
            GameObject lightObject = new GameObject("Glow");
            lightObject.transform.SetParent(t, false);
            lightObject.transform.localPosition = new Vector3(0, 1.2f, 0);
            Light light = lightObject.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = rune;
            light.range = 6f;
            light.intensity = 0.9f;
            light.shadows = LightShadows.None;
            return root;
        }

        public static GameObject Lamp()
        {
            GameObject root = new GameObject("Lamp post");
            Transform t = root.transform;
            LowPoly.Limb(t, Vector3.zero, new Vector3(0, 2.6f, 0), 0.07f, Iron, 6);
            LowPoly.Limb(t, new Vector3(0, 2.5f, 0), new Vector3(0.45f, 2.55f, 0), 0.04f, Iron, 4);
            LowPoly.Part(LowPoly.Ico(0), t, new Vector3(0.45f, 2.35f, 0), new Vector3(0.22f, 0.3f, 0.22f), RpgUtil.FromHex("#FFD98A", Color.yellow), 2.5f);
            return root;
        }

        public static GameObject Signpost(int arms)
        {
            GameObject root = new GameObject("Signpost");
            Transform t = root.transform;
            LowPoly.Limb(t, Vector3.zero, new Vector3(0, 2.2f, 0), 0.08f, Wood, 5);
            for (int i = 0; i < arms; i++)
            {
                LowPoly.Part(LowPoly.Block(), t, new Vector3(0, 1.9f - i * 0.35f, 0), Quaternion.Euler(0, i * 70f, 90), new Vector3(0.22f, 0.9f, 0.06f), WoodDark);
            }
            return root;
        }

        public static GameObject Tent(Color cloth)
        {
            GameObject root = new GameObject("Tent");
            Transform t = root.transform;
            LowPoly.Part(LowPoly.Prism(3), t, new Vector3(1.4f, 0.7f, 0), Quaternion.Euler(0, 0, 90), new Vector3(2.8f, 2.8f, 3.2f), cloth);
            LowPoly.Limb(t, new Vector3(1.45f, 0, 0), new Vector3(1.45f, 2.3f, 0), 0.05f, WoodDark, 4);
            LowPoly.Limb(t, new Vector3(-1.45f, 0, 0), new Vector3(-1.45f, 2.3f, 0), 0.05f, WoodDark, 4);
            Box(root, new Vector3(0, 1.0f, 0), new Vector3(2.4f, 2.0f, 2.6f));
            return root;
        }

        public static GameObject DeadTree(System.Random random, Color bark)
        {
            GameObject root = new GameObject("Dead tree");
            Transform t = root.transform;
            float height = 2.6f + (float)random.NextDouble() * 2f;
            LowPoly.Limb(t, Vector3.zero, new Vector3(0.1f, height, 0), 0.22f, bark, 6, 0.4f);
            for (int i = 0; i < 4; i++)
            {
                float a = (float)random.NextDouble() * Mathf.PI * 2f;
                float y = height * (0.45f + 0.15f * i);
                Vector3 from = new Vector3(0, y, 0);
                Vector3 to = from + new Vector3(Mathf.Cos(a) * 1.1f, 0.6f + (float)random.NextDouble() * 0.6f, Mathf.Sin(a) * 1.1f);
                LowPoly.Limb(t, from, to, 0.07f, bark, 5, 0.3f);
            }
            return root;
        }

        /// <summary>
        /// A standing townsperson made from the enemy soldier model with all game logic stripped, tinted
        /// by role. Idles using its animator. Built under an inactive parent so no game scripts ever run.
        /// </summary>
        public static GameObject Npc(GameObject enemyPrefab, Color tint, string name)
        {
            if (enemyPrefab == null) return null;
            GameObject holder = new GameObject("npc holder");
            holder.SetActive(false);
            GameObject npc = Object.Instantiate(enemyPrefab, holder.transform);
            npc.name = name;
            npc.tag = "Untagged";

            // CombatTarget requires Health, Mover requires the agent: remove scripts first, then the rest
            for (int pass = 0; pass < 3; pass++)
            {
                foreach (MonoBehaviour behaviour in npc.GetComponentsInChildren<MonoBehaviour>(true))
                {
                    if (behaviour is RPG.Combat.CombatTarget) Object.DestroyImmediate(behaviour);
                }
                foreach (MonoBehaviour behaviour in npc.GetComponentsInChildren<MonoBehaviour>(true))
                {
                    if (behaviour != null) Object.DestroyImmediate(behaviour);
                }
            }
            UnityEngine.AI.NavMeshAgent agent = npc.GetComponent<UnityEngine.AI.NavMeshAgent>();
            if (agent != null) Object.DestroyImmediate(agent);
            foreach (Collider collider in npc.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(collider);
            foreach (Rigidbody body in npc.GetComponentsInChildren<Rigidbody>(true)) Object.DestroyImmediate(body);

            npc.transform.SetParent(null, false);
            Object.Destroy(holder);
            RpgUtil.Tint(npc, tint, 0.55f);
            Box(npc, new Vector3(0, 0.9f, 0), new Vector3(0.7f, 1.8f, 0.7f));
            return npc;
        }
    }
}
