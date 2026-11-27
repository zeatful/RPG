using System.Collections.Generic;
using RPG.Content;
using RPG.Control;
using RPG.Core;
using RPG.Gathering;
using RPG.Items;
using RPG.Saving;
using Unity.AI.Navigation;
using UnityEngine;
using UnityEngine.AI;

namespace RPG.World
{
    /// <summary>
    /// Generates the open world at load time, the same every run: terrain, three towns (each with a bank
    /// chest, general store, furnace, anvil and hearth), ponds, scenery, the NavMesh, then the trees,
    /// rocks and monsters of every zone and the swamp boss.
    /// </summary>
    public static class WorldBuilder
    {
        const int Seed = 20261127;
        public const int DecorLayer = 9;
        public const float AggroRange = 5f;
        public const float ChaseLeash = 18f;
        public const float RoamRadius = 4f;

        public static Transform Root { get; private set; }
        public static Vector3 StartPosition { get; private set; }
        public static Vector3 BossLairPosition { get; private set; }
        public static float BuildSeconds { get; private set; }

        static RpgArt art;
        static System.Random random;
        static readonly List<Vector3> nodePositions = new List<Vector3>();

        public static void Build(GameObject player)
        {
            float started = Time.realtimeSinceStartup;
            art = RpgArt.Instance;
            random = new System.Random(Seed);
            nodePositions.Clear();

            Root = new GameObject("World").transform;
            Transform ground = new GameObject("Ground").transform;
            ground.SetParent(Root, false);
            Shader shader = art != null && art.VertexLit != null ? art.VertexLit : Shader.Find("Standard");
            WorldTerrain.Build(ground, new Material(shader));
            Physics.SyncTransforms();

            BuildPonds();
            Interiors.Reset();
            foreach (TownInfo town in WorldLayout.Towns) TownBuilder.Build(town, Root, art);
            BuildBanditCamp();
            BuildCrossroads();
            Physics.SyncTransforms();

            BakeNavMesh();
            Interiors.BakeNavMesh();
            Scatter();

            TownInfo start = WorldLayout.Town(WorldLayout.StartTown);
            StartPosition = OnNavMesh(new Vector3(start.Center.x - 2f, 0, start.Center.y + 8f));
            PlacePlayer(player, StartPosition);
            GameObject shrine = Props.SpawnShrine();
            Props.Place(shrine, Root, StartPosition, 0f);

            foreach (ZoneInfo zone in WorldLayout.Zones)
            {
                foreach (SpawnGroup group in zone.Nodes) SpawnNodes(group);
            }
            foreach (ZoneInfo zone in WorldLayout.Zones)
            {
                foreach (SpawnGroup group in zone.Enemies) SpawnEnemies(group);
            }
            SpawnBoss();

            SetupCamera();
            BuildSeconds = Time.realtimeSinceStartup - started;
            Debug.Log("[RPG World] built in " + BuildSeconds.ToString("0.0") + "s: " + nodePositions.Count + " nodes, "
                + EnemyProfile.All.Count + " enemies, " + Station.All.Count + " stations, " + Interiors.All.Count + " rooms");
        }

        // ------------------------------------------------------------------ helpers

        public static Vector3 Ground(float x, float z)
        {
            return new Vector3(x, WorldTerrain.GroundY(x, z), z);
        }

        static Vector3 OnNavMesh(Vector3 position)
        {
            Vector3 p = Ground(position.x, position.z);
            NavMeshHit hit;
            if (NavMesh.SamplePosition(p, out hit, 6f, NavMesh.AllAreas)) return hit.position;
            return p;
        }

        static float Range(float min, float max)
        {
            return min + (float)random.NextDouble() * (max - min);
        }

        static Vector2 InCircle(Vector2 center, float radius)
        {
            float a = Range(0, Mathf.PI * 2f);
            float r = Mathf.Sqrt((float)random.NextDouble()) * radius;
            return center + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r;
        }

        static bool InTown(Vector2 p, float margin)
        {
            foreach (TownInfo town in WorldLayout.Towns)
            {
                if ((town.Center - p).magnitude < TownInfo.FlatRadius + margin) return true;
            }
            return false;
        }

        static bool NearPond(Vector2 p, float margin)
        {
            foreach (Pond pond in WorldLayout.Ponds)
            {
                if ((pond.Center - p).magnitude < pond.Radius + margin) return true;
            }
            return false;
        }

        static bool Reachable(Vector3 to)
        {
            NavMeshPath path = new NavMeshPath();
            if (!NavMesh.CalculatePath(StartPosition, to, NavMesh.AllAreas, path)) return false;
            return path.status == NavMeshPathStatus.PathComplete;
        }

        // ------------------------------------------------------------------ water, camps, signs

        static void BuildPonds()
        {
            Shader water = art != null && art.Water != null ? art.Water : Shader.Find("Standard");
            Material material = new Material(water);
            foreach (Pond pond in WorldLayout.Ponds)
            {
                GameObject disc = LowPoly.Part(LowPoly.Prism(28), Root, new Vector3(pond.Center.x, WorldTerrain.WaterLevel - 0.05f, pond.Center.y),
                    new Vector3((pond.Radius + 3f) * 2f, 0.05f, (pond.Radius + 3f) * 2f), Color.white);
                disc.name = "Pond";
                disc.GetComponent<Renderer>().sharedMaterial = material;
                disc.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

                // nobody walks into the water
                GameObject block = new GameObject("Pond (not walkable)");
                block.transform.SetParent(Root, false);
                block.transform.position = new Vector3(pond.Center.x, WorldTerrain.WaterLevel, pond.Center.y);
                NavMeshModifierVolume volume = block.AddComponent<NavMeshModifierVolume>();
                volume.size = new Vector3(pond.Radius * 1.6f, 12f, pond.Radius * 1.6f);
                volume.center = Vector3.zero;
                volume.area = 1;
            }
        }

        static void BuildBanditCamp()
        {
            Vector2 c = WorldLayout.BanditCamp;
            Color[] cloth = { RpgUtil.FromHex("#7A3A2A", Color.red), RpgUtil.FromHex("#5A4A3A", Color.gray), RpgUtil.FromHex("#8A6A3A", Color.yellow) };
            for (int i = 0; i < 3; i++)
            {
                float a = i * 2.1f + 0.5f;
                Vector2 p = c + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * 8f;
                GameObject tent = Props.Tent(cloth[i]);
                Props.Place(tent, Root, Ground(p.x, p.y), -a * Mathf.Rad2Deg + 90f);
            }
            Campfire.Create(Ground(c.x, c.y), 0f, "#4A321E", null);
            if (art != null && art.Crate != null)
            {
                for (int i = 0; i < 5; i++)
                {
                    GameObject crate = Props.Model(art.Crate, 0.8f, true);
                    Vector2 p = c + new Vector2(Range(-6, 6), Range(-6, 6));
                    if ((p - c).magnitude < 2.5f) continue;
                    Props.Place(crate, Root, Ground(p.x, p.y), Range(0, 360));
                }
            }
        }

        static void BuildCrossroads()
        {
            Vector2 c = WorldLayout.Crossroads;
            GameObject sign = Props.Signpost(3);
            Props.Place(sign, Root, Ground(c.x + 3.5f, c.y + 3.5f), 20f);
        }

        // ------------------------------------------------------------------ navmesh

        static void BakeNavMesh()
        {
            NavMeshSurface surface = Root.gameObject.AddComponent<NavMeshSurface>();
            surface.collectObjects = CollectObjects.Children;
            surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
            surface.layerMask = ~(1 << DecorLayer);
            surface.overrideVoxelSize = true;
            surface.voxelSize = 0.25f;
            surface.overrideTileSize = true;
            surface.tileSize = 128;
            surface.BuildNavMesh();
        }

        static void PlacePlayer(GameObject player, Vector3 position)
        {
            NavMeshAgent agent = player.GetComponent<NavMeshAgent>();
            if (agent != null) agent.enabled = false;
            player.transform.position = position;
            player.transform.rotation = Quaternion.Euler(0, 0, 0);
            if (agent != null)
            {
                agent.enabled = true;
                agent.Warp(position);
            }
        }

        static void SetupCamera()
        {
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogStartDistance = 70f;
            RenderSettings.fogEndDistance = 210f;
            Camera camera = Camera.main;
            if (camera != null)
            {
                camera.farClipPlane = 240f;
                float[] distances = new float[32];
                distances[DecorLayer] = 85f;
                camera.layerCullDistances = distances;
            }
        }

        // ------------------------------------------------------------------ nodes and monsters

        static void SpawnNodes(SpawnGroup group)
        {
            NodeInfo info;
            if (!GameContent.Nodes.TryGetValue(group.Id, out info)) return;
            float spacing = info.Kind == NodeKind.Tree ? 3.6f : 2.4f;
            int spawned = 0;
            for (int attempt = 0; attempt < group.Count * 40 && spawned < group.Count; attempt++)
            {
                Vector2 p = InCircle(group.Center, group.Radius);
                if (InTown(p, 3f) || NearPond(p, 1.5f) || WorldLayout.DistanceToRoad(p) < 4f) continue;
                NavMeshHit hit;
                if (!NavMesh.SamplePosition(Ground(p.x, p.y), out hit, 1.5f, NavMesh.AllAreas)) continue;
                bool tooClose = false;
                foreach (Vector3 other in nodePositions)
                {
                    if (RpgUtil.FlatDistance(other, hit.position) < spacing) { tooClose = true; break; }
                }
                if (tooClose || !Reachable(hit.position)) continue;

                ResourceNode.Create(info, hit.position, Range(0, 360));
                nodePositions.Add(hit.position);
                spawned++;
            }
            if (spawned < group.Count) Debug.LogWarning("[RPG World] only placed " + spawned + "/" + group.Count + " " + group.Id + " near " + group.Center);
        }

        static void SpawnEnemies(SpawnGroup group)
        {
            EnemyInfo info;
            if (art == null || art.EnemyPrefab == null || !GameContent.Enemies.TryGetValue(group.Id, out info)) return;
            int spawned = 0;
            for (int attempt = 0; attempt < group.Count * 30 && spawned < group.Count; attempt++)
            {
                Vector2 p = InCircle(group.Center, group.Radius);
                if (InTown(p, 14f) || NearPond(p, 1.5f)) continue;
                NavMeshHit hit;
                if (!NavMesh.SamplePosition(Ground(p.x, p.y), out hit, 1.5f, NavMesh.AllAreas) || !Reachable(hit.position)) continue;
                SpawnEnemy(info, hit.position, Range(0, 360));
                spawned++;
            }
        }

        public static EnemyProfile SpawnEnemy(EnemyInfo info, Vector3 position, float yaw)
        {
            GameObject enemy = Object.Instantiate(art.EnemyPrefab, position, Quaternion.Euler(0, yaw, 0));
            // Spawned copies would all share the prefab's save id; enemies aren't saved.
            SaveableEntity saveable = enemy.GetComponent<SaveableEntity>();
            if (saveable != null) Object.Destroy(saveable);

            AIController ai = enemy.GetComponent<AIController>();
            ai.SetPatrolPath(null);
            // small aggro range and roaming area, medium chase: they give up ~18 m from home
            ai.ConfigureWorld(info.Aggressive, info.IsBoss ? 7f : AggroRange, info.IsBoss ? 20f : ChaseLeash, info.IsBoss ? 2f : RoamRadius);
            EnemyProfile profile = enemy.GetComponent<EnemyProfile>();
            if (profile == null) profile = enemy.AddComponent<EnemyProfile>();
            profile.Configure(info);
            return profile;
        }

        static void SpawnBoss()
        {
            EnemyInfo info;
            if (art == null || art.EnemyPrefab == null || !GameContent.Enemies.TryGetValue(GameContent.BossId, out info)) return;
            Vector3 lair = OnNavMesh(new Vector3(WorldLayout.BossLair.x, 0, WorldLayout.BossLair.y));
            BossLairPosition = lair;
            EnemyProfile boss = SpawnEnemy(info, lair, 135f);
            boss.gameObject.AddComponent<BossController>();

            // bones and dead trees around the lair
            Color bark = RpgUtil.FromHex("#3A3026", Color.gray);
            for (int i = 0; i < 7; i++)
            {
                float a = i * 0.9f;
                Vector2 p = WorldLayout.BossLair + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * Range(9f, 13f);
                GameObject tree = Props.DeadTree(random, bark);
                Props.Place(tree, Root, Ground(p.x, p.y), Range(0, 360));
            }
            for (int i = 0; i < 10; i++)
            {
                Vector2 p = InCircle(WorldLayout.BossLair, 7f);
                LowPoly.Part(LowPoly.Prism(5, 0.7f), Root, Ground(p.x, p.y), Quaternion.Euler(Range(70, 110), Range(0, 360), 0), new Vector3(0.12f, Range(0.5f, 0.9f), 0.12f), RpgUtil.FromHex("#E8DFCB", Color.white));
            }
        }

        // ------------------------------------------------------------------ scenery

        static void Scatter()
        {
            Transform decor = new GameObject("Scenery").transform;
            decor.SetParent(Root, false);

            ScatterZone("meadow", decor, 320, 120, 35, 0);
            ScatterZone("pinewood", decor, 160, 40, 55, 0);
            ScatterZone("hills", decor, 90, 10, 15, 90);
            ScatterZone("swamp", decor, 40, 0, 20, 0);
            ScatterZone("ash", decor, 0, 0, 0, 60);
            ScatterSpecial(decor);
            BorderForest(decor);
        }

        static Vector2 RandomPointIn(string zoneId, out bool ok)
        {
            ZoneInfo zone = null;
            foreach (ZoneInfo z in WorldLayout.Zones) if (z.Id == zoneId) zone = z;
            for (int attempt = 0; attempt < 12; attempt++)
            {
                Vector2 p = InCircle(zone.Center, 95f);
                if (Mathf.Abs(p.x) > WorldLayout.MountainStart - 4f || Mathf.Abs(p.y) > WorldLayout.MountainStart - 4f) continue;
                if (WorldLayout.ZoneAt(p) != zone) continue;
                if (InTown(p, 2f) || NearPond(p, 1f) || WorldLayout.DistanceToRoad(p) < 3.5f) continue;
                ok = true;
                return p;
            }
            ok = false;
            return Vector2.zero;
        }

        static void Decor(GameObject prefab, Transform parent, Vector2 p, float height)
        {
            if (prefab == null) return;
            GameObject go = Object.Instantiate(prefab, parent);
            foreach (Collider collider in go.GetComponentsInChildren<Collider>()) Object.Destroy(collider);
            go.transform.position = Ground(p.x, p.y);
            go.transform.rotation = Quaternion.Euler(0, Range(0, 360), 0);
            go.transform.localScale = Vector3.one * height;
            SetLayer(go, DecorLayer);
            EnableInstancing(go);
        }

        static void SetLayer(GameObject go, int layer)
        {
            foreach (Transform t in go.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = layer;
        }

        static readonly HashSet<Material> instanced = new HashSet<Material>();

        static void EnableInstancing(GameObject go)
        {
            foreach (Renderer renderer in go.GetComponentsInChildren<Renderer>())
            {
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                foreach (Material material in renderer.sharedMaterials)
                {
                    if (material != null && instanced.Add(material)) material.enableInstancing = true;
                }
            }
        }

        static void ScatterZone(string zoneId, Transform parent, int grass, int flowers, int bushes, int stones)
        {
            bool ok;
            for (int i = 0; i < grass; i++)
            {
                Vector2 p = RandomPointIn(zoneId, out ok);
                if (ok) Decor(art != null ? art.Grass : null, parent, p, Range(2.2f, 3.4f));
            }
            for (int i = 0; i < flowers; i++)
            {
                Vector2 p = RandomPointIn(zoneId, out ok);
                if (ok) Decor(art != null ? art.Flower : null, parent, p, Range(1.6f, 2.4f));
            }
            for (int i = 0; i < bushes; i++)
            {
                Vector2 p = RandomPointIn(zoneId, out ok);
                if (ok) Decor(art != null ? art.Bush : null, parent, p, Range(0.6f, 1.0f));
            }
            Color stone = zoneId == "ash" ? RpgUtil.FromHex("#3A3432", Color.gray) : RpgUtil.FromHex("#8A8578", Color.gray);
            for (int i = 0; i < stones; i++)
            {
                Vector2 p = RandomPointIn(zoneId, out ok);
                if (!ok) continue;
                float size = Range(0.3f, 0.9f);
                GameObject rock = LowPoly.Part(LowPoly.Ico(0, 0.3f, i), parent, Ground(p.x, p.y), Quaternion.Euler(0, Range(0, 360), 0), new Vector3(size * 1.3f, size * 0.7f, size), stone);
                rock.layer = DecorLayer;
                if (zoneId == "ash" && i % 3 == 0)
                {
                    // ember vents
                    GameObject ember = LowPoly.Part(LowPoly.Ico(0, 0.2f, i + 7), parent, Ground(p.x, p.y) + Vector3.up * size * 0.3f, new Vector3(size * 0.5f, size * 0.3f, size * 0.5f), RpgUtil.FromHex("#FF6A1A", Color.red), 1.8f);
                    ember.layer = DecorLayer;
                }
            }
        }

        static void ScatterSpecial(Transform parent)
        {
            bool ok;
            // swamp: dead trees and reeds
            Color bark = RpgUtil.FromHex("#3E3428", Color.gray);
            Color reed = RpgUtil.FromHex("#7A8A4A", Color.green);
            for (int i = 0; i < 30; i++)
            {
                Vector2 p = RandomPointIn("swamp", out ok);
                if (!ok) continue;
                GameObject tree = Props.DeadTree(random, bark);
                Props.Place(tree, parent, Ground(p.x, p.y), Range(0, 360));
            }
            foreach (Pond pond in WorldLayout.Ponds)
            {
                for (int i = 0; i < 14; i++)
                {
                    float a = Range(0, Mathf.PI * 2f);
                    Vector2 p = pond.Center + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * (pond.Radius + Range(1.5f, 3.5f));
                    for (int k = 0; k < 4; k++)
                    {
                        Vector3 at = Ground(p.x + Range(-0.4f, 0.4f), p.y + Range(-0.4f, 0.4f));
                        GameObject blade = LowPoly.Spike(parent, at, new Vector3(Range(-0.15f, 0.15f), 1f, Range(-0.15f, 0.15f)), Range(0.8f, 1.5f), 0.05f, reed, 3);
                        blade.layer = DecorLayer;
                    }
                }
            }
            // ashlands: charred trees
            Color charred = RpgUtil.FromHex("#221C1A", Color.black);
            for (int i = 0; i < 28; i++)
            {
                Vector2 p = RandomPointIn("ash", out ok);
                if (!ok) continue;
                GameObject tree = Props.DeadTree(random, charred);
                Props.Place(tree, parent, Ground(p.x, p.y), Range(0, 360));
            }
            // pinewood and meadow: fallen logs and pumpkins near Brookhollow
            for (int i = 0; i < 14; i++)
            {
                Vector2 p = RandomPointIn("pinewood", out ok);
                if (ok) Decor(art != null ? art.TreeLog : null, parent, p, Range(0.8f, 1.1f));
            }
            TownInfo brook = WorldLayout.Town("brookhollow");
            for (int i = 0; i < 24; i++)
            {
                Vector2 p = brook.Center + new Vector2(Range(-58f, -38f), Range(-12f, 12f));
                Decor(art != null ? art.Pumpkin : null, parent, p, Range(1.4f, 2.2f));
            }
        }

        /// <summary>Scenery trees on the lower mountain slopes so the world edge looks like forest.</summary>
        static void BorderForest(Transform parent)
        {
            if (art == null) return;
            GameObject[] green = { art.FirTreeA, art.FirTreeB, art.TallTree, art.RoundTree };
            Color charred = RpgUtil.FromHex("#221C1A", Color.black);
            Color bark = RpgUtil.FromHex("#3E3428", Color.gray);
            for (int i = 0; i < 260; i++)
            {
                float along = Range(-WorldLayout.MountainStart, WorldLayout.MountainStart);
                float inward = WorldLayout.MountainStart + Range(-6f, 14f);
                int side = random.Next(4);
                Vector2 p = side == 0 ? new Vector2(along, inward) : side == 1 ? new Vector2(along, -inward) : side == 2 ? new Vector2(inward, along) : new Vector2(-inward, along);
                ZoneInfo zone = WorldLayout.ZoneAt(p);
                if (InTown(p, 6f)) continue;
                if (zone.Id == "swamp" || zone.Id == "ash")
                {
                    GameObject tree = Props.DeadTree(random, zone.Id == "ash" ? charred : bark);
                    tree.transform.localScale = Vector3.one * Range(1.2f, 1.8f);
                    Props.Place(tree, parent, Ground(p.x, p.y), Range(0, 360));
                    continue;
                }
                GameObject prefab = green[random.Next(green.Length)];
                if (prefab == null) continue;
                GameObject fitted = RpgUtil.FitModel(prefab, new Vector3(0, Range(5.5f, 8.5f), 0));
                fitted.transform.SetParent(parent, false);
                fitted.transform.position = Ground(p.x, p.y);
                fitted.transform.rotation = Quaternion.Euler(0, Range(0, 360), 0);
                EnableInstancing(fitted);
            }
        }
    }
}
