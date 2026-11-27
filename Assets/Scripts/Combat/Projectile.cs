using RPG.Stats;
using UnityEngine;

namespace RPG.Combat
{
    /// <summary>Cosmetic arrow / spark that flies to the target. Damage is applied by the Fighter.</summary>
    public class Projectile : MonoBehaviour
    {
        const float Speed = 22f;
        const float MaxLife = 1.5f;

        Transform target;
        float life;

        public static void Launch(Vector3 from, Transform target, CombatStyle style)
        {
            bool magic = style == CombatStyle.Magic;
            GameObject go = GameObject.CreatePrimitive(magic ? PrimitiveType.Sphere : PrimitiveType.Cube);
            Destroy(go.GetComponent<Collider>());
            go.name = magic ? "Spark" : "Arrow";
            go.transform.position = from;
            go.transform.localScale = magic ? Vector3.one * 0.3f : new Vector3(0.05f, 0.05f, 0.6f);

            Renderer renderer = go.GetComponent<Renderer>();
            Color color = magic ? new Color(1f, 0.6f, 0.2f) : new Color(0.75f, 0.6f, 0.4f);
            renderer.material.color = color;
            if (magic && renderer.material.HasProperty("_EmissionColor"))
            {
                renderer.material.EnableKeyword("_EMISSION");
                renderer.material.SetColor("_EmissionColor", color * 1.5f);
            }

            Projectile projectile = go.AddComponent<Projectile>();
            projectile.target = target;
        }

        private void Update()
        {
            life += Time.deltaTime;
            if (target == null || life > MaxLife)
            {
                Destroy(gameObject);
                return;
            }

            Vector3 aim = target.position + Vector3.up * 1.1f;
            transform.LookAt(aim);
            transform.position = Vector3.MoveTowards(transform.position, aim, Speed * Time.deltaTime);
            if (Vector3.Distance(transform.position, aim) < 0.2f) Destroy(gameObject);
        }
    }
}
