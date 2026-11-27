using RPG.Content;
using RPG.Core;
using UnityEngine;

namespace RPG.Gathering
{
    /// <summary>A tree or rock. Gives a few logs/ores (each taking several swings), then depletes and respawns on a timer.</summary>
    public class ResourceNode : MonoBehaviour
    {
        public static readonly System.Collections.Generic.List<ResourceNode> Active = new System.Collections.Generic.List<ResourceNode>();

        public NodeInfo Info { get; private set; }
        public bool IsDepleted { get; private set; }

        /// <summary>Work done towards the next log/ore (0..Hardness), shown as a progress bar.</summary>
        public double Work { get; set; }

        /// <summary>Logs/ores left before the node is depleted.</summary>
        public int Remaining { get; private set; }

        public float Progress { get { return Info.Hardness > 0 ? Mathf.Clamp01((float)(Work / Info.Hardness)) : 0f; } }

        /// <summary>Seconds since the last swing at it, so the HUD only shows the bar while working.</summary>
        public float SinceSwing { get; set; }

        System.Random yieldRandom;

        GameObject fullVisual;
        GameObject depletedVisual;
        float respawnTimer;

        public static ResourceNode Create(NodeInfo info, Vector3 position, float yRotation)
        {
            GameObject root = new GameObject(info.Name);
            root.transform.position = position;
            root.transform.rotation = Quaternion.Euler(0, yRotation, 0);

            ResourceNode node = root.AddComponent<ResourceNode>();
            node.Info = info;
            node.SinceSwing = 999f;

            node.fullVisual = new GameObject("Full");
            node.fullVisual.transform.SetParent(root.transform, false);
            node.depletedVisual = new GameObject("Depleted");
            node.depletedVisual.transform.SetParent(root.transform, false);

            // per-node variety that stays the same every run
            int seed = Mathf.Abs(Mathf.RoundToInt(position.x * 73f + position.z * 131f));
            node.yieldRandom = new System.Random(seed);

            if (info.Kind == NodeKind.Tree)
            {
                CapsuleCollider collider = root.AddComponent<CapsuleCollider>();
                collider.radius = 0.8f;
                collider.height = 4.5f;
                collider.center = new Vector3(0, 2.25f, 0);
                TreeArt.Build(info, node.fullVisual.transform, node.depletedVisual.transform, seed);
            }
            else
            {
                SphereCollider collider = root.AddComponent<SphereCollider>();
                collider.radius = 0.9f;
                collider.center = new Vector3(0, 0.5f, 0);
                TreeArt.BuildRock(info, node.fullVisual.transform, node.depletedVisual.transform, seed);
            }

            node.SetDepleted(false);
            return node;
        }

        private void OnEnable() { Active.Add(this); }
        private void OnDisable() { Active.Remove(this); }

        public void Deplete()
        {
            SetDepleted(true);
            respawnTimer = 0;
        }

        /// <summary>Takes one log/ore from the node; depletes it when none are left. Returns true if now depleted.</summary>
        public bool TakeOne()
        {
            Work = 0;
            Remaining--;
            if (Remaining <= 0)
            {
                Deplete();
                return true;
            }
            return false;
        }

        private void Update()
        {
            SinceSwing += Time.deltaTime;
            if (!IsDepleted) return;
            respawnTimer += Time.deltaTime;
            if (respawnTimer >= Info.RespawnSeconds) SetDepleted(false);
        }

        void SetDepleted(bool depleted)
        {
            IsDepleted = depleted;
            Work = 0;
            if (!depleted) Remaining = yieldRandom.Next(Info.YieldMin, Mathf.Max(Info.YieldMin, Info.YieldMax) + 1);
            fullVisual.SetActive(!depleted);
            depletedVisual.SetActive(depleted);
        }

        public string ActionVerb
        {
            get { return Info.Kind == NodeKind.Tree ? "Chop" : "Mine"; }
        }
    }
}
