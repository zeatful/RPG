using RPG.Content;
using RPG.Core;
using UnityEngine;
using UnityEngine.AI;

namespace RPG.Items
{
    /// <summary>A loot item lying on the ground. Click it to walk over and pick it up.</summary>
    public class Pickup : MonoBehaviour
    {
        const float Lifetime = 180f;

        public ItemStack Stack;
        public bool Special;

        float age;
        Vector3 basePosition;
        float phase;

        public string Label
        {
            get
            {
                ItemInfo info = GameContent.Item(Stack.ItemId);
                string name = info != null ? info.Name : Stack.ItemId;
                return Stack.Quantity > 1 ? name + " x" + Stack.Quantity : name;
            }
        }

        public static Pickup Spawn(ItemStack stack, Vector3 position, bool special)
        {
            ItemInfo info = GameContent.Item(stack.ItemId);
            Color color = RpgUtil.FromHex(info != null ? info.ColorHex : null, Color.white);

            NavMeshHit hit;
            if (NavMesh.SamplePosition(position, out hit, 3f, NavMesh.AllAreas)) position = hit.position;

            // the item's icon as a floating billboard; rare drops sit on a glowing orb
            GameObject go = new GameObject("Loot: " + (info != null ? info.Name : stack.ItemId));
            go.transform.position = position + Vector3.up * 0.45f;
            SphereCollider collider = go.AddComponent<SphereCollider>();
            collider.radius = 0.35f;

            GameObject spriteObject = new GameObject("Icon");
            spriteObject.transform.SetParent(go.transform, false);
            SpriteRenderer sprite = spriteObject.AddComponent<SpriteRenderer>();
            sprite.sprite = RPG.UI.ItemIcons.GetSprite(stack.ItemId, 0.6f);
            if (sprite.sprite == null)
            {
                Destroy(spriteObject);
                RpgUtil.Part(PrimitiveType.Cube, go.transform, Vector3.zero, Vector3.one * 0.35f, color);
            }

            if (special)
            {
                GameObject orb = RpgUtil.Part(PrimitiveType.Sphere, go.transform, new Vector3(0, -0.3f, 0), Vector3.one * 0.22f, GameLog.Rare);
                RpgUtil.SetGlow(orb, GameLog.Rare * 0.9f);
                spriteObject.transform.localScale = Vector3.one * 1.25f;
            }

            Pickup pickup = go.AddComponent<Pickup>();
            pickup.Stack = stack;
            pickup.Special = special;
            pickup.basePosition = go.transform.position;
            pickup.phase = Random.value * 10f;
            return pickup;
        }

        private void Update()
        {
            age += Time.deltaTime;
            if (age > Lifetime)
            {
                Destroy(gameObject);
                return;
            }
            transform.position = basePosition + Vector3.up * (Mathf.Sin(Time.time * 2f + phase) * 0.06f);
            // face the camera like a billboard
            Camera camera = Camera.main;
            if (camera != null) transform.rotation = Quaternion.LookRotation(transform.position - camera.transform.position, Vector3.up);
        }
    }
}
