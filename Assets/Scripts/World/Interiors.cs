using System.Collections;
using System.Collections.Generic;
using RPG.Core;
using RPG.Items;
using Unity.AI.Navigation;
using UnityEngine;
using UnityEngine.AI;

namespace RPG.World
{
    /// <summary>
    /// A room you can walk into: built far outside the map (like a separate instance) with its own
    /// NavMesh. Its door leads back to the house door outside. The same mechanism can host caves and
    /// dungeons later.
    /// </summary>
    public class Interior
    {
        public string Name;
        public string Kind;
        public Transform Root;
        public Bounds Area;
        public Vector3 EntryPoint;     // where you appear inside
        public Vector3 OutsidePoint;   // where you appear when you leave
        public float OutsideYaw;
        public readonly List<FadeWall> Walls = new List<FadeWall>();
    }

    /// <summary>On a door station: which interior it leads into, or out of.</summary>
    public class Doorway : MonoBehaviour
    {
        public Interior Target;
        public bool IsExit;
    }

    /// <summary>A wall that turns see-through while it faces the camera, so you can see inside a room.</summary>
    public class FadeWall : MonoBehaviour
    {
        public Vector3 Outward;
        Renderer[] renderers;
        Material[] solid;
        bool faded;
        static readonly Dictionary<Color, Material> fadeMaterials = new Dictionary<Color, Material>();

        public void SetFaded(bool value)
        {
            if (renderers == null)
            {
                renderers = GetComponentsInChildren<Renderer>();
                solid = new Material[renderers.Length];
                for (int i = 0; i < renderers.Length; i++) solid[i] = renderers[i].sharedMaterial;
            }
            if (faded == value) return;
            faded = value;
            for (int i = 0; i < renderers.Length; i++)
            {
                if (renderers[i] == null) continue;
                renderers[i].sharedMaterial = value ? FadeOf(solid[i]) : solid[i];
                renderers[i].shadowCastingMode = value ? UnityEngine.Rendering.ShadowCastingMode.Off : UnityEngine.Rendering.ShadowCastingMode.On;
            }
        }

        static Material FadeOf(Material original)
        {
            Color color = original != null && original.HasProperty("_Color") ? original.color : Color.gray;
            Material material;
            if (fadeMaterials.TryGetValue(color, out material) && material != null) return material;
            RpgArt art = RpgArt.Instance;
            Shader shader = art != null && art.Fade != null ? art.Fade : Shader.Find("RPG/Fade");
            if (shader == null) return original;
            material = new Material(shader);
            material.color = new Color(color.r, color.g, color.b, 0.18f);
            fadeMaterials[color] = material;
            return material;
        }
    }

    /// <summary>Builds and tracks all interiors; moves the player through doors with a short fade.</summary>
    public static class Interiors
    {
        public const float RoomWidth = 9f;
        public const float RoomDepth = 7f;
        public const float WallHeight = 3f;
        static readonly Vector3 Origin = new Vector3(1600f, 0f, 1600f);

        public static readonly List<Interior> All = new List<Interior>();
        public static Transform Root { get; private set; }

        public static void Reset()
        {
            All.Clear();
            Root = new GameObject("Interiors").transform;
        }

        /// <summary>The interior containing a position, or null when outside.</summary>
        public static Interior At(Vector3 position)
        {
            foreach (Interior interior in All)
            {
                if (interior.Area.Contains(position)) return interior;
            }
            return null;
        }

        static readonly Color Wood = RpgUtil.FromHex("#6B4A2B", Color.gray);
        static readonly Color WoodDark = RpgUtil.FromHex("#4A321E", Color.gray);
        static readonly Color Plaster = RpgUtil.FromHex("#D8CBB0", Color.white);
        static readonly Color Floor = RpgUtil.FromHex("#7A5A3A", Color.gray);
        static readonly Color Stone = RpgUtil.FromHex("#7E7A74", Color.gray);
        static readonly Color Gold = RpgUtil.FromHex("#D9B23A", Color.yellow);

        /// <summary>
        /// Builds a room and a door prop outside at doorPosition (facing doorYaw, i.e. out of the house).
        /// kind: "home" (bed, table, fireplace you can cook at) or "bank" (bank chest and a banker).
        /// </summary>
        public static Interior Create(string name, string kind, string town, Vector3 doorPosition, float doorYaw, Transform outsideParent, System.Random random)
        {
            if (Root == null) Reset();
            int index = All.Count;
            Vector3 center = Origin + new Vector3((index % 8) * 30f, 0, (index / 8) * 30f);
            Interior interior = new Interior { Name = name, Kind = kind };
            interior.Root = new GameObject(name + " (inside)").transform;
            interior.Root.SetParent(Root, false);
            interior.Root.position = center;
            interior.Area = new Bounds(center + Vector3.up * 2f, new Vector3(RoomWidth + 1f, 8f, RoomDepth + 1f));

            BuildRoom(interior, kind, random);
            Furnish(interior, kind, town, random);

            // the door in the outside world
            Quaternion facing = Quaternion.Euler(0, doorYaw, 0);
            GameObject door = DoorProp(name == null ? "Door" : name);
            Props.Place(door, outsideParent, doorPosition, doorYaw);
            Station outside = Station.Create(door, StationKind.Door, name, town);
            Doorway doorway = door.AddComponent<Doorway>();
            doorway.Target = interior;
            interior.OutsidePoint = doorPosition + facing * Vector3.forward * 1.3f;
            interior.OutsideYaw = doorYaw;

            All.Add(interior);
            return interior;
        }

        static GameObject Part(Transform parent, Vector3 local, Vector3 size, Color color, bool solid, float glow = 0f)
        {
            GameObject go = LowPoly.Part(LowPoly.Block(), parent, local, Quaternion.identity, size, color, glow);
            if (solid)
            {
                BoxCollider box = go.AddComponent<BoxCollider>();
                box.center = new Vector3(0, 0.5f, 0);
                box.size = Vector3.one;
            }
            return go;
        }

        static void BuildRoom(Interior interior, string kind, System.Random random)
        {
            Transform t = interior.Root;
            float w = RoomWidth, d = RoomDepth, h = WallHeight, th = 0.25f;
            Color wall = kind == "bank" ? RpgUtil.FromHex("#C9C2B4", Color.white) : Plaster;

            GameObject floor = Part(t, new Vector3(0, -0.25f, 0), new Vector3(w + th * 2, 0.25f, d + th * 2), kind == "bank" ? Stone : Floor, true);
            floor.layer = 8;
            // floorboards
            if (kind != "bank")
            {
                for (int i = 0; i < 9; i++) Part(t, new Vector3(-w / 2 + 0.5f + i, 0, 0), new Vector3(0.04f, 0.01f, d), WoodDark, false);
            }

            // walls: back (+z), left, right, and the front (-z) with a doorway in the middle
            AddWall(interior, new Vector3(0, 0, d / 2 + th / 2), new Vector3(w + th * 2, h, th), Vector3.forward, wall);
            AddWall(interior, new Vector3(-w / 2 - th / 2, 0, 0), new Vector3(th, h, d), Vector3.left, wall);
            AddWall(interior, new Vector3(w / 2 + th / 2, 0, 0), new Vector3(th, h, d), Vector3.right, wall);
            float gap = 1.5f;
            float side = (w + th * 2 - gap) / 2f;
            AddWall(interior, new Vector3(-gap / 2 - side / 2, 0, -d / 2 - th / 2), new Vector3(side, h, th), Vector3.back, wall);
            AddWall(interior, new Vector3(gap / 2 + side / 2, 0, -d / 2 - th / 2), new Vector3(side, h, th), Vector3.back, wall);
            AddWall(interior, new Vector3(0, 2.3f, -d / 2 - th / 2), new Vector3(gap, h - 2.3f, th), Vector3.back, wall, false);

            // beams along the top of the walls
            foreach (float x in new[] { -w / 2, w / 2 })
            {
                GameObject beam = Part(t, new Vector3(x, h - 0.2f, 0), new Vector3(0.3f, 0.2f, d + 0.5f), WoodDark, false);
                FadeWall fade = beam.AddComponent<FadeWall>();
                fade.Outward = x < 0 ? Vector3.left : Vector3.right;
                interior.Walls.Add(fade);
            }

            // the way out: a door frame on the inside of the doorway
            GameObject exit = new GameObject("Exit");
            exit.transform.SetParent(t, false);
            exit.transform.localPosition = new Vector3(0, 0, -d / 2 - 0.05f);
            Part(exit.transform, new Vector3(0, 0, 0), new Vector3(gap, 0.02f, 0.4f), RpgUtil.FromHex("#3A2A1E", Color.black), false);
            Part(exit.transform, new Vector3(0, 0.02f, -0.1f), new Vector3(gap - 0.2f, 2.2f, 0.05f), RpgUtil.FromHex("#1A1410", Color.black), false);
            BoxCollider exitBox = exit.AddComponent<BoxCollider>();
            exitBox.center = new Vector3(0, 1.1f, -0.1f);
            exitBox.size = new Vector3(gap, 2.2f, 0.3f);
            exitBox.isTrigger = false;
            Station station = Station.Create(exit, StationKind.Door, "Way out", null);
            Doorway doorway = exit.AddComponent<Doorway>();
            doorway.Target = interior;
            doorway.IsExit = true;
            exit.layer = 9; // not part of the NavMesh, so you can walk right up to it

            interior.EntryPoint = t.position + new Vector3(0, 0, -d / 2 + 1.2f);

            // warm light so rooms are lit at night too
            GameObject lamp = new GameObject("Room light");
            lamp.transform.SetParent(t, false);
            lamp.transform.localPosition = new Vector3(0, h - 0.4f, 0.5f);
            Light light = lamp.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = new Color(1f, 0.82f, 0.6f);
            light.range = 10f;
            light.intensity = 1.1f;
            light.shadows = LightShadows.None;
        }

        static void AddWall(Interior interior, Vector3 local, Vector3 size, Vector3 outward, Color color, bool solid = true)
        {
            GameObject wall = Part(interior.Root, local, size, color, solid);
            // a darker skirting board along the bottom
            Part(wall.transform, new Vector3(0, 0, 0), new Vector3(1.02f, 0.12f / size.y, 1.06f), WoodDark, false);
            FadeWall fade = wall.AddComponent<FadeWall>();
            fade.Outward = outward;
            interior.Walls.Add(fade);
        }

        static void Furnish(Interior interior, string kind, string town, System.Random random)
        {
            Transform t = interior.Root;
            float w = RoomWidth, d = RoomDepth;
            RpgArt art = RpgArt.Instance;
            GameObject npcPrefab = art != null ? art.EnemyPrefab : null;

            // a rug in the middle
            Color[] rugs = { RpgUtil.FromHex("#8A2E2E", Color.red), RpgUtil.FromHex("#2E4A7A", Color.blue), RpgUtil.FromHex("#4A6A2E", Color.green) };
            Part(t, new Vector3(0, 0.005f, 0), new Vector3(3.2f, 0.02f, 2.2f), rugs[random.Next(rugs.Length)], false);
            Part(t, new Vector3(0, 0.01f, 0), new Vector3(2.8f, 0.02f, 1.8f), Color.Lerp(rugs[random.Next(rugs.Length)], Color.white, 0.25f), false);

            if (kind == "bank")
            {
                // a long counter with the banker behind it and the bank chest in front
                Part(t, new Vector3(0, 0, d / 2 - 1.6f), new Vector3(5f, 1.05f, 0.7f), Wood, true);
                Part(t, new Vector3(0, 1.05f, d / 2 - 1.6f), new Vector3(5.2f, 0.07f, 0.8f), WoodDark, false);
                GameObject chest = Props.BankChest();
                chest.transform.SetParent(t, false);
                chest.transform.localPosition = new Vector3(-2.6f, 0, 0.6f);
                chest.transform.localRotation = Quaternion.Euler(0, 90f, 0);
                Station.Create(chest, StationKind.Bank, "Bank chest", town);
                GameObject banker = Props.Npc(npcPrefab, RpgUtil.FromHex("#D9B23A", Color.yellow), "Banker");
                if (banker != null)
                {
                    banker.transform.SetParent(t, false);
                    banker.transform.localPosition = new Vector3(0.6f, 0, d / 2 - 0.9f);
                    banker.transform.localRotation = Quaternion.Euler(0, 180f, 0);
                    Station.Create(banker, StationKind.Bank, "Banker", town);
                }
                // vault: gold piles and strongboxes along the back wall
                for (int i = 0; i < 5; i++)
                {
                    float x = -3.4f + i * 1.7f;
                    LowPoly.Part(LowPoly.Ico(0, 0.25f, i), t, new Vector3(x, 0.1f, d / 2 - 0.4f), new Vector3(0.5f, 0.3f, 0.4f), Gold, 0.3f);
                    Part(t, new Vector3(x + 0.6f, 0, d / 2 - 0.4f), new Vector3(0.5f, 0.45f, 0.4f), RpgUtil.FromHex("#3A3C40", Color.gray), false);
                }
                Part(t, new Vector3(3.6f, 0, -1.5f), new Vector3(0.8f, 1.6f, 0.4f), WoodDark, true);
                return;
            }

            // bed in the back-left corner
            Vector3 bed = new Vector3(-w / 2 + 0.75f, 0, d / 2 - 1.2f);
            Part(t, bed, new Vector3(1.2f, 0.45f, 2.1f), Wood, true);
            Part(t, bed + new Vector3(0, 0.45f, 0), new Vector3(1.1f, 0.15f, 2.0f), RpgUtil.FromHex("#E6DCC6", Color.white), false);
            Color[] blankets = { RpgUtil.FromHex("#7A2E2E", Color.red), RpgUtil.FromHex("#3F5E8C", Color.blue), RpgUtil.FromHex("#3E6B3A", Color.green), RpgUtil.FromHex("#6B4E8A", Color.magenta) };
            Part(t, bed + new Vector3(0, 0.6f, -0.25f), new Vector3(1.14f, 0.06f, 1.4f), blankets[random.Next(blankets.Length)], false);
            Part(t, bed + new Vector3(0, 0.6f, 0.7f), new Vector3(0.7f, 0.12f, 0.35f), Color.white, false);
            Part(t, bed + new Vector3(0, 0, 1.0f), new Vector3(1.2f, 1.0f, 0.12f), WoodDark, false);

            // table with two stools and a few things on it
            Vector3 table = new Vector3(1.8f, 0, 0.4f);
            Part(t, table + new Vector3(0, 0.75f, 0), new Vector3(1.6f, 0.08f, 1.0f), Wood, false);
            foreach (Vector3 leg in new[] { new Vector3(-0.7f, 0, -0.4f), new Vector3(0.7f, 0, -0.4f), new Vector3(-0.7f, 0, 0.4f), new Vector3(0.7f, 0, 0.4f) })
            {
                Part(t, table + leg, new Vector3(0.08f, 0.75f, 0.08f), WoodDark, false);
            }
            GameObject tableBlock = new GameObject("Table");
            tableBlock.transform.SetParent(t, false);
            tableBlock.transform.localPosition = table;
            BoxCollider tableBox = tableBlock.AddComponent<BoxCollider>();
            tableBox.center = new Vector3(0, 0.4f, 0);
            tableBox.size = new Vector3(1.6f, 0.8f, 1.0f);
            foreach (float x in new[] { -1.15f, 1.15f })
            {
                LowPoly.Part(LowPoly.Prism(6), t, table + new Vector3(x, 0, 0), new Vector3(0.4f, 0.45f, 0.4f), WoodDark);
            }
            LowPoly.Part(LowPoly.Ico(0, 0.1f, 1), t, table + new Vector3(-0.3f, 0.86f, 0), new Vector3(0.3f, 0.2f, 0.3f), RpgUtil.FromHex("#C98B4A", Color.yellow));
            LowPoly.Part(LowPoly.Prism(6), t, table + new Vector3(0.35f, 0.83f, 0.1f), new Vector3(0.14f, 0.2f, 0.14f), RpgUtil.FromHex("#8A8D91", Color.gray));
            LowPoly.Part(LowPoly.Prism(8), t, table + new Vector3(0.1f, 0.83f, -0.25f), new Vector3(0.3f, 0.04f, 0.3f), RpgUtil.FromHex("#D8CBB0", Color.white));

            // stone fireplace on the right wall: a permanent fire you can cook on
            Vector3 hearth = new Vector3(w / 2 - 0.55f, 0, -0.9f);
            Part(t, hearth + new Vector3(0.15f, 0, 0), new Vector3(0.8f, 2.2f, 1.8f), Stone, true);
            Part(t, hearth + new Vector3(-0.15f, 0.9f, 0), new Vector3(0.3f, 0.15f, 1.9f), WoodDark, false);
            Campfire fire = Campfire.Create(t.TransformPoint(hearth + new Vector3(-0.75f, 0, 0)), 0f, "#4A321E", town);
            fire.transform.SetParent(t, true);
            fire.transform.localScale = Vector3.one * 0.7f;

            // shelf with pots, a chest, and barrels
            Vector3 shelf = new Vector3(1.0f, 1.5f, d / 2 - 0.25f);
            Part(t, shelf, new Vector3(2.2f, 0.07f, 0.35f), Wood, false);
            for (int i = 0; i < 4; i++)
            {
                Color pot = i % 2 == 0 ? RpgUtil.FromHex("#A0603A", Color.red) : RpgUtil.FromHex("#5E7A8A", Color.blue);
                LowPoly.Part(LowPoly.Prism(7, 0.75f), t, shelf + new Vector3(-0.8f + i * 0.5f, 0.07f, 0), new Vector3(0.22f, 0.28f, 0.22f), pot);
            }
            Part(t, new Vector3(-w / 2 + 0.6f, 0, -1.6f), new Vector3(0.6f, 0.55f, 1.0f), WoodDark, true);
            Part(t, new Vector3(-w / 2 + 0.6f, 0.55f, -1.6f), new Vector3(0.64f, 0.06f, 1.04f), Gold, false);
            for (int i = 0; i < 2; i++)
            {
                GameObject barrel = LowPoly.Part(LowPoly.Prism(8, 0.9f), t, new Vector3(w / 2 - 0.5f - i * 0.7f, 0, d / 2 - 0.5f), new Vector3(0.6f, 0.9f, 0.6f), Wood);
                barrel.AddComponent<BoxCollider>();
            }

            // whoever lives here
            Color[] residents = { RpgUtil.FromHex("#8A6A9A", Color.magenta), RpgUtil.FromHex("#9A7A4A", Color.yellow), RpgUtil.FromHex("#5A8A7A", Color.cyan), RpgUtil.FromHex("#9A5A4A", Color.red) };
            GameObject resident = Props.Npc(npcPrefab, residents[random.Next(residents.Length)], "Villager");
            if (resident != null)
            {
                resident.transform.SetParent(t, false);
                resident.transform.localPosition = new Vector3(0.6f, 0, 1.6f);
                resident.transform.localRotation = Quaternion.Euler(0, 200f, 0);
            }
        }

        /// <summary>A plank door in a frame, built facing +z (out of the house).</summary>
        static GameObject DoorProp(string name)
        {
            GameObject root = new GameObject("Door");
            Transform t = root.transform;
            Color plank = RpgUtil.FromHex("#6B4528", Color.gray);
            Color frame = RpgUtil.FromHex("#3A2A1E", Color.black);
            LowPoly.Part(LowPoly.Block(), t, new Vector3(0, 0, 0.02f), Quaternion.identity, new Vector3(1.1f, 2.05f, 0.12f), plank);
            for (int i = 0; i < 4; i++)
            {
                LowPoly.Part(LowPoly.Block(), t, new Vector3(-0.41f + i * 0.275f, 0, 0.09f), Quaternion.identity, new Vector3(0.03f, 2.0f, 0.02f), frame);
            }
            LowPoly.Part(LowPoly.Block(), t, new Vector3(-0.62f, 0, 0), Quaternion.identity, new Vector3(0.14f, 2.25f, 0.2f), frame);
            LowPoly.Part(LowPoly.Block(), t, new Vector3(0.62f, 0, 0), Quaternion.identity, new Vector3(0.14f, 2.25f, 0.2f), frame);
            LowPoly.Part(LowPoly.Block(), t, new Vector3(0, 2.1f, 0), Quaternion.identity, new Vector3(1.38f, 0.15f, 0.22f), frame);
            LowPoly.Part(LowPoly.Ico(0), t, new Vector3(0.36f, 1.0f, 0.12f), Vector3.one * 0.08f, Gold, 0.3f);
            LowPoly.Part(LowPoly.Block(), t, new Vector3(0, 0, 0.45f), Quaternion.identity, new Vector3(1.0f, 0.02f, 0.6f), RpgUtil.FromHex("#8A6A3A", Color.yellow));
            BoxCollider box = root.AddComponent<BoxCollider>();
            box.center = new Vector3(0, 1.05f, 0.05f);
            box.size = new Vector3(1.3f, 2.1f, 0.3f);
            root.layer = 9; // keep it out of the NavMesh so the path ends right at the door
            return root;
        }

        /// <summary>Bakes the NavMesh of every room (separately from the world's).</summary>
        public static void BakeNavMesh()
        {
            if (Root == null || All.Count == 0) return;
            NavMeshSurface surface = Root.gameObject.AddComponent<NavMeshSurface>();
            surface.collectObjects = CollectObjects.Children;
            surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
            surface.layerMask = ~(1 << WorldBuilder.DecorLayer);
            surface.overrideVoxelSize = true;
            surface.voxelSize = 0.15f;
            surface.BuildNavMesh();
        }
    }

    /// <summary>
    /// Runs on the RPG Systems object: walks the player through doors with a fade to black, switches the
    /// camera background indoors and fades the walls between the camera and the player.
    /// </summary>
    public class InteriorDirector : MonoBehaviour
    {
        public static InteriorDirector Instance { get; private set; }

        /// <summary>0..1 black overlay drawn by the HUD while moving through a door.</summary>
        public float FadeAlpha { get; private set; }
        public bool Travelling { get; private set; }

        GameObject player;
        Interior current;
        CameraClearFlags outsideClear;
        bool cameraSaved;

        public Interior Current { get { return current; } }

        private void Awake()
        {
            Instance = this;
        }

        private void Start()
        {
            player = GameObject.FindWithTag("Player");
        }

        public void Travel(Doorway door)
        {
            if (door == null || door.Target == null || Travelling || player == null) return;
            StartCoroutine(TravelRoutine(door));
        }

        IEnumerator TravelRoutine(Doorway door)
        {
            Travelling = true;
            Sfx.At("door", player.transform.position, 0.8f);
            for (float t = 0; t < 0.25f; t += Time.unscaledDeltaTime)
            {
                FadeAlpha = t / 0.25f;
                yield return null;
            }
            FadeAlpha = 1f;
            Interior target = door.Target;
            Vector3 destination = door.IsExit ? target.OutsidePoint : target.EntryPoint;
            float yaw = door.IsExit ? target.OutsideYaw : 0f;
            NavMeshHit hit;
            if (NavMesh.SamplePosition(destination, out hit, 3f, NavMesh.AllAreas)) destination = hit.position;
            NavMeshAgent agent = player.GetComponent<NavMeshAgent>();
            ActionScheduler scheduler = player.GetComponent<ActionScheduler>();
            if (scheduler != null) scheduler.CancelCurrentAction();
            if (agent != null && agent.enabled) agent.Warp(destination);
            else player.transform.position = destination;
            player.transform.rotation = Quaternion.Euler(0, yaw, 0);
            GameLog.Add(door.IsExit ? "You step back outside." : "You go into the " + target.Name.ToLower() + ".");
            yield return null;
            for (float t = 0; t < 0.35f; t += Time.unscaledDeltaTime)
            {
                FadeAlpha = 1f - t / 0.35f;
                yield return null;
            }
            FadeAlpha = 0f;
            Travelling = false;
        }

        private void LateUpdate()
        {
            if (player == null) return;
            Interior now = Interiors.At(player.transform.position);
            Camera camera = Camera.main;
            if (now != current)
            {
                if (current != null) foreach (FadeWall wall in current.Walls) wall.SetFaded(false);
                current = now;
                if (camera != null)
                {
                    if (!cameraSaved)
                    {
                        outsideClear = camera.clearFlags;
                        cameraSaved = true;
                    }
                    camera.clearFlags = now != null ? CameraClearFlags.SolidColor : outsideClear;
                    camera.backgroundColor = new Color(0.07f, 0.06f, 0.05f);
                }
            }
            if (current == null || camera == null) return;
            // walls whose outside faces the camera are see-through
            Vector3 view = camera.transform.forward;
            view.y = 0;
            view.Normalize();
            foreach (FadeWall wall in current.Walls)
            {
                if (wall == null) continue;
                wall.SetFaded(Vector3.Dot(wall.Outward, -view) > 0.2f);
            }
        }
    }
}
