using System.Collections.Generic;
using UnityEngine;

namespace RPG.World
{
    public class TownInfo
    {
        public string Id;
        public string Name;
        public Vector2 Center;
        public string ShopId;
        public string Blurb;
        public const float PlazaRadius = 15f;
        public const float FlatRadius = 30f;
    }

    /// <summary>A cluster of enemies or resource nodes placed around a point.</summary>
    public class SpawnGroup
    {
        public string Id;
        public int Count;
        public Vector2 Center;
        public float Radius;

        public SpawnGroup(string id, int count, float x, float z, float radius)
        {
            Id = id;
            Count = count;
            Center = new Vector2(x, z);
            Radius = radius;
        }
    }

    public class ZoneInfo
    {
        public string Id;
        public string Name;
        public string Levels;
        public Vector2 Center;
        public Color Ground;
        public Color GroundAlt;
        public float Hills;
        public List<SpawnGroup> Enemies = new List<SpawnGroup>();
        public List<SpawnGroup> Nodes = new List<SpawnGroup>();
    }

    public class Pond
    {
        public Vector2 Center;
        public float Radius;

        public Pond(float x, float z, float radius)
        {
            Center = new Vector2(x, z);
            Radius = radius;
        }
    }

    /// <summary>
    /// The world map: three towns joined by roads through five zones, each with its own monsters
    /// (getting harder the further you go from Brookhollow) and its own trees and rocks.
    ///
    ///                      Emberfall (Ashlands 30-50)
    ///                              |
    ///   Mirefen Swamp 15-30 ---- Crossroads (Old Pinewood 5-15) ---- Ironmere (Greyrock Hills 10-25)
    ///                              |
    ///                      Brookhollow (Meadow 1-10)
    /// </summary>
    public static class WorldLayout
    {
        // Ground covers +-Extent; the playable area is ringed by mountains from about +-MountainStart.
        public const float Extent = 280f;
        public const float MountainStart = 232f;

        public static readonly Vector2 Crossroads = new Vector2(0, 10);

        public static readonly List<TownInfo> Towns = new List<TownInfo>
        {
            new TownInfo { Id = "brookhollow", Name = "Brookhollow", Center = new Vector2(0, -170), ShopId = "brookhollow", Blurb = "A quiet farming town. Good place to start." },
            new TownInfo { Id = "ironmere", Name = "Ironmere", Center = new Vector2(170, 10), ShopId = "ironmere", Blurb = "Mining town in the Greyrock Hills." },
            new TownInfo { Id = "emberfall", Name = "Emberfall", Center = new Vector2(-20, 175), ShopId = "emberfall", Blurb = "Last outpost before the Ashlands." },
        };

        public const string StartTown = "brookhollow";

        // Roads as polylines (x, z).
        public static readonly List<Vector2[]> Roads = new List<Vector2[]>
        {
            new[] { new Vector2(0, -170), new Vector2(-4, -120), new Vector2(6, -60), new Vector2(0, 10) },
            new[] { new Vector2(0, 10), new Vector2(-12, 90), new Vector2(-20, 175) },
            new[] { new Vector2(0, 10), new Vector2(80, 2), new Vector2(170, 10) },
            new[] { new Vector2(0, 10), new Vector2(-70, 14), new Vector2(-118, 12) },
        };

        public static readonly List<Pond> Ponds = new List<Pond>
        {
            new Pond(118, 58, 13f),     // Ironmere lake
            new Pond(-42, 42, 8f),      // Pinewood pond
            new Pond(-160, -22, 10f),   // swamp pools
            new Pond(-124, 30, 7f),
            new Pond(-205, 8, 9f),
        };

        public static readonly Vector2 BossLair = new Vector2(-190, 52);
        public static readonly Vector2 BanditCamp = new Vector2(150, 78);

        public static readonly List<ZoneInfo> Zones = BuildZones();

        static List<ZoneInfo> BuildZones()
        {
            List<ZoneInfo> zones = new List<ZoneInfo>();

            ZoneInfo meadow = Zone("meadow", "Brookhollow Meadow", "1-10", 0, -135, "#5E9142", "#6E9E4A", 1.6f);
            meadow.Enemies.Add(new SpawnGroup("thistlehog", 7, -45, -125, 22));
            meadow.Enemies.Add(new SpawnGroup("thistlehog", 5, 40, -110, 18));
            meadow.Enemies.Add(new SpawnGroup("gnarl", 5, -62, -172, 16));
            meadow.Enemies.Add(new SpawnGroup("boar", 5, 58, -160, 16));
            meadow.Nodes.Add(new SpawnGroup("pine", 9, -32, -150, 22));
            meadow.Nodes.Add(new SpawnGroup("pine", 6, 34, -138, 18));
            meadow.Nodes.Add(new SpawnGroup("oak", 5, -72, -118, 18));
            meadow.Nodes.Add(new SpawnGroup("copper", 4, 44, -198, 8));
            meadow.Nodes.Add(new SpawnGroup("tin", 4, 50, -190, 8));
            zones.Add(meadow);

            ZoneInfo pinewood = Zone("pinewood", "Old Pinewood", "5-15", 0, 0, "#46743A", "#527F3E", 2.4f);
            pinewood.Enemies.Add(new SpawnGroup("gnarl", 6, -25, -15, 22));
            pinewood.Enemies.Add(new SpawnGroup("grey_wolf", 4, 45, 55, 16));
            pinewood.Nodes.Add(new SpawnGroup("pine", 10, -30, -40, 26));
            pinewood.Nodes.Add(new SpawnGroup("oak", 8, 30, -28, 24));
            pinewood.Nodes.Add(new SpawnGroup("willow", 5, -42, 42, 15));
            zones.Add(pinewood);

            ZoneInfo hills = Zone("hills", "Greyrock Hills", "10-25", 150, 10, "#8E9A5C", "#8A8270", 4.5f);
            hills.Enemies.Add(new SpawnGroup("grey_wolf", 5, 118, -42, 20));
            hills.Enemies.Add(new SpawnGroup("bandit", 6, BanditCamp.x, BanditCamp.y, 13));
            hills.Enemies.Add(new SpawnGroup("stone_golem", 5, 205, -40, 16));
            hills.Nodes.Add(new SpawnGroup("iron", 5, 206, 32, 9));
            hills.Nodes.Add(new SpawnGroup("coal", 5, 214, 58, 9));
            hills.Nodes.Add(new SpawnGroup("copper", 2, 128, -12, 7));
            hills.Nodes.Add(new SpawnGroup("tin", 2, 136, -16, 7));
            hills.Nodes.Add(new SpawnGroup("oak", 4, 108, 22, 12));
            hills.Nodes.Add(new SpawnGroup("willow", 5, 118, 58, 20));
            zones.Add(hills);

            ZoneInfo swamp = Zone("swamp", "Mirefen Swamp", "15-30", -155, 15, "#4E5E36", "#5A5034", 0.8f);
            swamp.Enemies.Add(new SpawnGroup("bog_slime", 8, -150, 2, 32));
            swamp.Nodes.Add(new SpawnGroup("willow", 8, -130, -40, 22));
            swamp.Nodes.Add(new SpawnGroup("yew", 4, -186, -45, 14));
            swamp.Nodes.Add(new SpawnGroup("maple", 3, -112, 62, 12));
            zones.Add(swamp);

            ZoneInfo ash = Zone("ash", "Emberfall Ashlands", "30-50", 10, 160, "#4E4844", "#5E4238", 3.2f);
            ash.Enemies.Add(new SpawnGroup("cinder_imp", 6, 45, 135, 20));
            ash.Enemies.Add(new SpawnGroup("ash_wolf", 5, -75, 128, 20));
            ash.Enemies.Add(new SpawnGroup("magma_golem", 4, 92, 205, 14));
            ash.Nodes.Add(new SpawnGroup("maple", 5, -45, 98, 16));
            ash.Nodes.Add(new SpawnGroup("ashwood", 4, -92, 192, 12));
            ash.Nodes.Add(new SpawnGroup("yew", 2, -58, 214, 8));
            ash.Nodes.Add(new SpawnGroup("verdite", 3, 28, 192, 7));
            ash.Nodes.Add(new SpawnGroup("coal", 3, 36, 182, 7));
            ash.Nodes.Add(new SpawnGroup("cobalt", 3, 82, 160, 7));
            ash.Nodes.Add(new SpawnGroup("cinderite", 3, 100, 212, 6));
            zones.Add(ash);

            return zones;
        }

        static ZoneInfo Zone(string id, string name, string levels, float x, float z, string ground, string alt, float hills)
        {
            Color a, b;
            ColorUtility.TryParseHtmlString(ground, out a);
            ColorUtility.TryParseHtmlString(alt, out b);
            return new ZoneInfo { Id = id, Name = name, Levels = levels, Center = new Vector2(x, z), Ground = a, GroundAlt = b, Hills = hills };
        }

        public static TownInfo Town(string id)
        {
            foreach (TownInfo town in Towns) if (town.Id == id) return town;
            return null;
        }

        /// <summary>The zone whose centre is nearest (a simple Voronoi map).</summary>
        public static ZoneInfo ZoneAt(Vector2 p)
        {
            ZoneInfo best = null;
            float bestDistance = float.MaxValue;
            foreach (ZoneInfo zone in Zones)
            {
                float d = (zone.Center - p).sqrMagnitude;
                if (d < bestDistance)
                {
                    bestDistance = d;
                    best = zone;
                }
            }
            return best;
        }

        public static float DistanceToRoad(Vector2 p)
        {
            float best = float.MaxValue;
            foreach (Vector2[] road in Roads)
            {
                for (int i = 0; i + 1 < road.Length; i++) best = Mathf.Min(best, DistanceToSegment(p, road[i], road[i + 1]));
            }
            return best;
        }

        public static float DistanceToSegment(Vector2 p, Vector2 a, Vector2 b)
        {
            Vector2 ab = b - a;
            float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / Mathf.Max(0.0001f, ab.sqrMagnitude));
            return (a + ab * t - p).magnitude;
        }

        /// <summary>Directions (degrees, 0 = +x, counter-clockwise) that roads leave a town in.</summary>
        public static List<float> RoadAngles(TownInfo town)
        {
            List<float> angles = new List<float>();
            foreach (Vector2[] road in Roads)
            {
                if ((road[0] - town.Center).sqrMagnitude < 1f) angles.Add(Angle(road[1] - road[0]));
                if ((road[road.Length - 1] - town.Center).sqrMagnitude < 1f) angles.Add(Angle(road[road.Length - 2] - road[road.Length - 1]));
            }
            return angles;
        }

        static float Angle(Vector2 v)
        {
            return Mathf.Atan2(v.y, v.x) * Mathf.Rad2Deg;
        }

        public static TownInfo NearestTown(Vector2 p, out float distance)
        {
            TownInfo best = null;
            distance = float.MaxValue;
            foreach (TownInfo town in Towns)
            {
                float d = (town.Center - p).magnitude;
                if (d < distance)
                {
                    distance = d;
                    best = town;
                }
            }
            return best;
        }
    }
}
