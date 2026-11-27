using System.Collections.Generic;
using RPG.Content;
using RPG.Core;
using RPG.Items;
using UnityEngine;

namespace RPG.World
{
    /// <summary>
    /// Lays out a town around its cobbled square: hearth in the middle, then a bank chest with a banker,
    /// a general store stall with a shopkeeper, and a smithing corner (furnace + anvil + smith), ringed
    /// by houses, lamp posts and fences. Roads into town are kept clear.
    /// </summary>
    public static class TownBuilder
    {
        class Style
        {
            public Color Awning;
            public Color Banker = RpgUtil.FromHex("#D9B23A", Color.yellow);
            public Color Shopkeeper;
            public bool Huts;
        }

        static Style StyleOf(string id)
        {
            switch (id)
            {
                case "ironmere": return new Style { Awning = RpgUtil.FromHex("#3A5BD0", Color.blue), Shopkeeper = RpgUtil.FromHex("#5A7AA0", Color.blue), Huts = true };
                case "emberfall": return new Style { Awning = RpgUtil.FromHex("#D0602A", Color.red), Shopkeeper = RpgUtil.FromHex("#A04A2A", Color.red), Huts = true };
            }
            return new Style { Awning = RpgUtil.FromHex("#B8433E", Color.red), Shopkeeper = RpgUtil.FromHex("#5E8C31", Color.green), Huts = false };
        }

        static Transform root;
        static TownInfo town;
        static List<float> roads;

        public static void Build(TownInfo info, Transform world, RpgArt art)
        {
            town = info;
            root = new GameObject(info.Name).transform;
            root.SetParent(world, false);
            roads = WorldLayout.RoadAngles(info);
            float main = roads.Count > 0 ? roads[0] : 90f;
            Style style = StyleOf(info.Id);
            GameObject npcPrefab = art != null ? art.EnemyPrefab : null;

            // hearth in the square (cook here for free)
            Campfire.Create(At(0, 0), 0f, "#4A321E", info.Name).transform.SetParent(root, true);

            // bank chest with the banker behind it, and the bank house behind that
            float bankAngle = main + 115f;
            GameObject chest = Props.BankChest();
            Put(chest, bankAngle, 9.5f);
            Station.Create(chest, StationKind.Bank, "Bank chest", info.Name);
            GameObject banker = Props.Npc(npcPrefab, style.Banker, "Banker");
            if (banker != null)
            {
                Put(banker, bankAngle, 11f);
                Station.Create(banker, StationKind.Bank, "Banker", info.Name);
            }
            House(art, style, bankAngle, 18.5f, 1.45f);

            // general store stall with its shopkeeper
            float shopAngle = main - 115f;
            GameObject stall = Props.Stall(style.Awning);
            Put(stall, shopAngle, 10f);
            Station.Create(stall, StationKind.Shop, GameContent.Shops[info.ShopId].Name, info.Name).ShopId = info.ShopId;
            GameObject keeper = Props.Npc(npcPrefab, style.Shopkeeper, "Shopkeeper");
            if (keeper != null)
            {
                Put(keeper, shopAngle, 11.2f);
                Station station = Station.Create(keeper, StationKind.Shop, GameContent.Shops[info.ShopId].Name, info.Name);
                station.ShopId = info.ShopId;
            }
            if (art != null && art.Crate != null)
            {
                for (int i = 0; i < 3; i++)
                {
                    GameObject crate = Props.Model(art.Crate, 0.75f, true);
                    Put(crate, shopAngle + 13f + i * 4f, 11.5f + (i % 2) * 0.9f);
                }
            }

            // smithing corner at the back of the square
            float smithAngle = main + 180f;
            GameObject furnace = Props.Furnace();
            Put(furnace, smithAngle + 14f, 10.5f);
            Station.Create(furnace, StationKind.Furnace, "Furnace", info.Name);
            GameObject anvil = Props.Anvil();
            Put(anvil, smithAngle - 12f, 9.5f);
            Station.Create(anvil, StationKind.Anvil, "Anvil", info.Name);
            GameObject smith = Props.Npc(npcPrefab, RpgUtil.FromHex("#4A4C50", Color.gray), "Smith");
            if (smith != null) Put(smith, smithAngle - 20f, 11.5f);

            // houses around the outside, skipping the roads and the buildings above
            float[] houses = { 50f, 82f, 150f, 178f, 208f, -50f, -82f, -150f };
            for (int i = 0; i < houses.Length; i++)
            {
                float angle = main + houses[i];
                if (NearRoad(angle, 24f) || Mathf.Abs(Mathf.DeltaAngle(angle, bankAngle)) < 22f) continue;
                House(art, style, angle, 21f + (i % 3) * 1.5f, 1.25f + (i % 2) * 0.15f);
            }

            // lamp posts on the edge of the square
            for (int i = 0; i < 6; i++)
            {
                float angle = main + 30f + i * 60f;
                if (NearRoad(angle, 12f)) continue;
                Put(Props.Lamp(), angle, TownInfo.PlazaRadius + 0.8f);
            }

            // fence runs around the edge of town, with gaps for roads and every so often
            if (art != null && art.Fence != null)
            {
                float radius = TownInfo.FlatRadius - 1f;
                float step = 1.4f / radius * Mathf.Rad2Deg;
                int index = 0;
                for (float angle = main + 12f; angle < main + 348f; angle += step, index++)
                {
                    if (NearRoad(angle, 12f) || index % 14 >= 11) continue;
                    GameObject fence = Props.Model(art.Fence, 1.1f, true);
                    Put(fence, angle, radius);
                }
            }

            // a couple of villagers chatting in the square
            Color[] villagers = { RpgUtil.FromHex("#8A6A9A", Color.magenta), RpgUtil.FromHex("#9A7A4A", Color.yellow) };
            for (int i = 0; i < 2; i++)
            {
                GameObject villager = Props.Npc(npcPrefab, villagers[i], "Villager");
                if (villager == null) continue;
                Put(villager, main + 60f + i * 12f, 5.5f);
                villager.transform.rotation = Quaternion.Euler(0, villager.transform.eulerAngles.y + (i == 0 ? 50f : -50f), 0);
            }
        }

        static bool NearRoad(float angle, float margin)
        {
            foreach (float road in roads)
            {
                if (Mathf.Abs(Mathf.DeltaAngle(angle, road)) < margin) return true;
            }
            return false;
        }

        static Vector3 At(float angle, float radius)
        {
            float a = angle * Mathf.Deg2Rad;
            Vector2 p = town.Center + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * radius;
            return WorldBuilder.Ground(p.x, p.y);
        }

        /// <summary>Places a prop on the ring at angle/radius, facing the middle of the square.</summary>
        static void Put(GameObject go, float angle, float radius)
        {
            if (go == null) return;
            float a = angle * Mathf.Deg2Rad;
            float yaw = Mathf.Atan2(-Mathf.Cos(a), -Mathf.Sin(a)) * Mathf.Rad2Deg;
            Props.Place(go, root, At(angle, radius), yaw);
        }

        static void House(RpgArt art, Style style, float angle, float radius, float scale)
        {
            if (art == null) return;
            GameObject house;
            if (style.Huts && art.Hut != null)
            {
                house = Props.Model(art.Hut, 5.4f * scale, true);
            }
            else if (art.HouseBase != null && art.HouseRoof != null)
            {
                house = new GameObject("House");
                GameObject walls = Object.Instantiate(art.HouseBase, house.transform);
                GameObject roof = Object.Instantiate(art.HouseRoof, house.transform);
                roof.transform.localPosition = new Vector3(0, 2.28f, 0);
                foreach (Collider collider in house.GetComponentsInChildren<Collider>()) Object.DestroyImmediate(collider);
                house.transform.localScale = Vector3.one * scale;
                BoxCollider box = house.AddComponent<BoxCollider>();
                box.center = new Vector3(0, 1.6f, 0);
                box.size = new Vector3(4.3f, 3.2f, 5.8f);
            }
            else if (art.VillageHouse != null)
            {
                house = Props.Model(art.VillageHouse, 3.3f * scale * 1.3f, true);
            }
            else return;
            Put(house, angle, radius);
        }
    }
}
