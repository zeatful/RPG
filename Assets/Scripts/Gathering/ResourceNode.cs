using RPG.Content;
using RPG.Core;
using UnityEngine;

namespace RPG.Gathering
{
    /// <summary>A tree or rock built from primitives. Depletes when gathered and respawns on a timer.</summary>
    public class ResourceNode : MonoBehaviour
    {
        public NodeInfo Info { get; private set; }
        public bool IsDepleted { get; private set; }

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

            node.fullVisual = new GameObject("Full");
            node.fullVisual.transform.SetParent(root.transform, false);
            node.depletedVisual = new GameObject("Depleted");
            node.depletedVisual.transform.SetParent(root.transform, false);

            // per-node variety that stays the same every run
            int seed = Mathf.Abs(Mathf.RoundToInt(position.x * 73f + position.z * 131f));

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

        public void Deplete()
        {
            SetDepleted(true);
            respawnTimer = 0;
        }

        private void Update()
        {
            if (!IsDepleted) return;
            respawnTimer += Time.deltaTime;
            if (respawnTimer >= Info.RespawnSeconds) SetDepleted(false);
        }

        void SetDepleted(bool depleted)
        {
            IsDepleted = depleted;
            fullVisual.SetActive(!depleted);
            depletedVisual.SetActive(depleted);
        }

        public string ActionVerb
        {
            get { return Info.Kind == NodeKind.Tree ? "Chop" : "Mine"; }
        }
    }
}
