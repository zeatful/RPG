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

            GameObject go = GameObject.CreatePrimitive(special ? PrimitiveType.Sphere : PrimitiveType.Cube);
            go.name = "Loot: " + (info != null ? info.Name : stack.ItemId);
            go.transform.position = position + Vector3.up * 0.3f;
            go.transform.localScale = Vector3.one * (special ? 0.45f : 0.35f);
            RpgUtil.SetColor(go, color);
            if (special) RpgUtil.SetGlow(go, color * 0.8f);

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
            transform.Rotate(0, 60f * Time.deltaTime, 0, Space.World);
        }
    }
}
