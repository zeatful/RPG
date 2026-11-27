using RPG.Content;
using RPG.Core;
using UnityEngine;

namespace RPG.Items
{
    /// <summary>A small follower summoned from a pet item. Hops along behind the player.</summary>
    public class PetFollower : MonoBehaviour
    {
        const float FollowDistance = 1.6f;
        const float Speed = 7f;

        public static PetFollower Current { get; private set; }
        public string PetItemId { get; private set; }

        Transform owner;
        float hopPhase;

        public static void Toggle(string petItemId, Transform owner)
        {
            if (Current != null)
            {
                bool same = Current.PetItemId == petItemId;
                Destroy(Current.gameObject);
                Current = null;
                GameLog.Add("You dismiss your pet.");
                if (same) return;
            }

            ItemInfo info = GameContent.Item(petItemId);
            Color color = RpgUtil.FromHex(info != null ? info.ColorHex : null, Color.white);

            GameObject root = new GameObject(info != null ? info.Name : "Pet");
            root.transform.position = owner.position - owner.forward * FollowDistance;
            RpgUtil.Part(PrimitiveType.Sphere, root.transform, new Vector3(0, 0.35f, 0), new Vector3(0.6f, 0.5f, 0.7f), color);
            RpgUtil.Part(PrimitiveType.Sphere, root.transform, new Vector3(0.14f, 0.5f, 0.28f), Vector3.one * 0.12f, Color.white);
            RpgUtil.Part(PrimitiveType.Sphere, root.transform, new Vector3(-0.14f, 0.5f, 0.28f), Vector3.one * 0.12f, Color.white);

            PetFollower pet = root.AddComponent<PetFollower>();
            pet.owner = owner;
            pet.PetItemId = petItemId;
            Current = pet;
            GameLog.Add((info != null ? info.Name : "Your pet") + " follows you.", GameLog.Rare);
        }

        private void Update()
        {
            if (owner == null)
            {
                Destroy(gameObject);
                return;
            }

            Vector3 goal = owner.position - owner.forward * FollowDistance;
            Vector3 flat = goal - transform.position;
            flat.y = 0;
            if (flat.magnitude > 15f) transform.position = goal; // teleport if left far behind
            else if (flat.magnitude > 0.3f)
            {
                transform.position += flat.normalized * Mathf.Min(Speed * Time.deltaTime, flat.magnitude);
                transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(flat), 10f * Time.deltaTime);
                hopPhase += Time.deltaTime * 12f;
            }

            Vector3 position = transform.position;
            position.y = owner.position.y + Mathf.Abs(Mathf.Sin(hopPhase)) * 0.15f;
            transform.position = position;
        }

        private void OnDestroy()
        {
            if (Current == this) Current = null;
        }
    }
}
