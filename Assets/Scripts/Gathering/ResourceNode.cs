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

            Color main = RpgUtil.FromHex(info.ColorHex, Color.green);
            Color accent = RpgUtil.FromHex(info.AccentHex, Color.gray);

            node.fullVisual = new GameObject("Full");
            node.fullVisual.transform.SetParent(root.transform, false);
            node.depletedVisual = new GameObject("Depleted");
            node.depletedVisual.transform.SetParent(root.transform, false);

            if (info.Kind == NodeKind.Tree)
            {
                CapsuleCollider collider = root.AddComponent<CapsuleCollider>();
                collider.radius = 0.8f;
                collider.height = 4.5f;
                collider.center = new Vector3(0, 2.25f, 0);

                Transform full = node.fullVisual.transform;
                RpgUtil.Part(PrimitiveType.Cylinder, full, new Vector3(0, 1.2f, 0), new Vector3(0.4f, 1.2f, 0.4f), accent);
                if (info.Id == "pine")
                {
                    RpgUtil.Part(PrimitiveType.Sphere, full, new Vector3(0, 2.6f, 0), new Vector3(1.8f, 1.6f, 1.8f), main);
                    RpgUtil.Part(PrimitiveType.Sphere, full, new Vector3(0, 3.6f, 0), new Vector3(1.2f, 1.4f, 1.2f), main);
                    RpgUtil.Part(PrimitiveType.Sphere, full, new Vector3(0, 4.4f, 0), new Vector3(0.6f, 0.9f, 0.6f), main);
                }
                else if (info.Id == "willow")
                {
                    RpgUtil.Part(PrimitiveType.Sphere, full, new Vector3(0, 3.0f, 0), new Vector3(3.0f, 1.6f, 3.0f), main);
                    RpgUtil.Part(PrimitiveType.Cylinder, full, new Vector3(0, 2.2f, 0), new Vector3(2.6f, 0.8f, 2.6f), main * 0.9f);
                }
                else
                {
                    RpgUtil.Part(PrimitiveType.Sphere, full, new Vector3(0, 3.1f, 0), new Vector3(2.6f, 2.2f, 2.6f), main);
                    RpgUtil.Part(PrimitiveType.Sphere, full, new Vector3(0.8f, 2.6f, 0.3f), new Vector3(1.4f, 1.2f, 1.4f), main * 0.9f);
                }
                RpgUtil.Part(PrimitiveType.Cylinder, node.depletedVisual.transform, new Vector3(0, 0.2f, 0), new Vector3(0.45f, 0.2f, 0.45f), accent);
            }
            else
            {
                SphereCollider collider = root.AddComponent<SphereCollider>();
                collider.radius = 0.9f;
                collider.center = new Vector3(0, 0.5f, 0);

                Transform full = node.fullVisual.transform;
                RpgUtil.Part(PrimitiveType.Sphere, full, new Vector3(0, 0.45f, 0), new Vector3(1.6f, 1.1f, 1.4f), main);
                GameObject vein1 = RpgUtil.Part(PrimitiveType.Sphere, full, new Vector3(0.45f, 0.75f, 0.3f), new Vector3(0.45f, 0.35f, 0.45f), accent);
                GameObject vein2 = RpgUtil.Part(PrimitiveType.Sphere, full, new Vector3(-0.4f, 0.6f, 0.45f), new Vector3(0.35f, 0.3f, 0.35f), accent);
                GameObject vein3 = RpgUtil.Part(PrimitiveType.Sphere, full, new Vector3(0.05f, 0.95f, -0.35f), new Vector3(0.3f, 0.25f, 0.3f), accent);
                RpgUtil.SetGlow(vein1, accent * 0.15f);
                RpgUtil.SetGlow(vein2, accent * 0.15f);
                RpgUtil.SetGlow(vein3, accent * 0.15f);

                RpgUtil.Part(PrimitiveType.Sphere, node.depletedVisual.transform, new Vector3(0, 0.4f, 0), new Vector3(1.5f, 0.9f, 1.3f), main * 0.6f);
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
