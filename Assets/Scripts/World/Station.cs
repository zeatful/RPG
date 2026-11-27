using System.Collections.Generic;
using RPG.Core;
using RPG.Items;
using UnityEngine;

namespace RPG.World
{
    /// <summary>
    /// Something in the world the player clicks to use: a bank chest, a shop counter, a furnace, an anvil
    /// or a fire. Needs a collider for clicking. The HUD opens the matching panel when the player is close.
    /// </summary>
    public class Station : MonoBehaviour
    {
        public const float UseRange = 2.6f;

        public static readonly List<Station> All = new List<Station>();

        public StationKind Kind;
        public string Label;
        public string ShopId;
        public string Town;

        Collider area;

        /// <summary>Flat distance from a position to the edge of this station.</summary>
        public float DistanceTo(Vector3 position)
        {
            if (area == null) area = GetComponentInChildren<Collider>();
            Vector3 point = area != null && area.enabled ? area.ClosestPoint(position) : transform.position;
            return RpgUtil.FlatDistance(point, position);
        }

        /// <summary>A point next to the station on the player's side, to walk to.</summary>
        public Vector3 ApproachPoint(Vector3 from)
        {
            if (area == null) area = GetComponentInChildren<Collider>();
            Vector3 point = area != null ? area.ClosestPoint(from) : transform.position;
            Vector3 away = from - point;
            away.y = 0;
            if (away.sqrMagnitude < 0.01f) away = transform.forward;
            return point + away.normalized * 1.0f;
        }

        private void OnEnable() { All.Add(this); }
        private void OnDisable() { All.Remove(this); }

        public static Station Create(GameObject go, StationKind kind, string label, string town)
        {
            Station station = go.AddComponent<Station>();
            station.Kind = kind;
            station.Label = label;
            station.Town = town;
            return station;
        }

        public string Verb
        {
            get
            {
                switch (Kind)
                {
                    case StationKind.Bank: return "Open";
                    case StationKind.Shop: return "Trade at";
                    case StationKind.Furnace: return "Smelt at";
                    case StationKind.Anvil: return "Smith at";
                    case StationKind.Fire: return "Cook on";
                }
                return "Use";
            }
        }

        /// <summary>Nearest station of a kind within range of a position, or null.</summary>
        public static Station Nearest(StationKind kind, Vector3 position, float range)
        {
            Station best = null;
            float bestDistance = range;
            foreach (Station station in All)
            {
                if (station.Kind != kind) continue;
                float distance = station.DistanceTo(position);
                if (distance <= bestDistance)
                {
                    bestDistance = distance;
                    best = station;
                }
            }
            return best;
        }

        public static bool IsNear(StationKind kind, Vector3 position)
        {
            return kind == StationKind.None || Nearest(kind, position, UseRange + 0.8f) != null;
        }

        public static string Describe(StationKind kind)
        {
            switch (kind)
            {
                case StationKind.Furnace: return "a furnace";
                case StationKind.Anvil: return "an anvil";
                case StationKind.Fire: return "a fire";
                case StationKind.Bank: return "a bank chest";
                case StationKind.Shop: return "a shop";
            }
            return "nothing";
        }
    }
}
