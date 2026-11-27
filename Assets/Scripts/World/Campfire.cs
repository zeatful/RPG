using RPG.Core;
using RPG.Items;
using UnityEngine;

namespace RPG.World
{
    /// <summary>A fire you can cook on. Lit fires burn out; town hearths burn forever (lifetime 0).</summary>
    public class Campfire : MonoBehaviour
    {
        float lifetime;
        float age;
        Light glow;
        float flicker;

        public float SecondsLeft { get { return lifetime <= 0 ? float.PositiveInfinity : Mathf.Max(0, lifetime - age); } }

        public static Campfire Create(Vector3 position, float lifetime, string logColorHex, string town)
        {
            GameObject root = new GameObject(lifetime > 0 ? "Fire" : "Town hearth");
            root.transform.position = position;
            Campfire fire = root.AddComponent<Campfire>();
            fire.lifetime = lifetime;
            fire.flicker = Random.value * 10f;

            Color log = RpgUtil.FromHex(logColorHex, new Color(0.45f, 0.3f, 0.18f));
            Color stone = new Color(0.42f, 0.4f, 0.38f);
            Transform t = root.transform;

            // a ring of stones for permanent hearths
            if (lifetime <= 0)
            {
                for (int i = 0; i < 9; i++)
                {
                    float a = i * Mathf.PI * 2f / 9f;
                    LowPoly.Part(LowPoly.Ico(0, 0.2f, i), t, new Vector3(Mathf.Cos(a) * 0.75f, 0.08f, Mathf.Sin(a) * 0.75f), new Vector3(0.32f, 0.22f, 0.28f), stone);
                }
            }
            // crossed logs and glowing embers
            for (int i = 0; i < 4; i++)
            {
                float a = i * Mathf.PI * 0.5f + 0.4f;
                Vector3 dir = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a));
                LowPoly.Limb(t, dir * 0.45f + Vector3.up * 0.06f, Vector3.up * 0.32f - dir * 0.05f, 0.07f, log, 5, 0.9f);
            }
            LowPoly.Part(LowPoly.Ico(0, 0.25f, 3), t, new Vector3(0, 0.06f, 0), new Vector3(0.45f, 0.12f, 0.45f), new Color(1f, 0.45f, 0.1f), 1.5f);

            RpgArt art = RpgArt.Instance;
            if (art != null && art.FireFx != null)
            {
                GameObject fx = Instantiate(art.FireFx, t);
                fx.transform.localPosition = new Vector3(0, 0.1f, 0);
                fx.transform.localScale = Vector3.one * 0.6f;
            }
            else
            {
                LowPoly.Spike(t, new Vector3(0, 0.1f, 0), Vector3.up, 0.7f, 0.25f, new Color(1f, 0.6f, 0.15f), 5).GetComponent<Renderer>().sharedMaterial = LowPoly.Mat(new Color(1f, 0.6f, 0.15f), 1.2f);
            }

            GameObject lightObject = new GameObject("Glow");
            lightObject.transform.SetParent(t, false);
            lightObject.transform.localPosition = new Vector3(0, 0.8f, 0);
            fire.glow = lightObject.AddComponent<Light>();
            fire.glow.type = LightType.Point;
            fire.glow.color = new Color(1f, 0.6f, 0.3f);
            fire.glow.range = 7f;
            fire.glow.intensity = 1.6f;
            fire.glow.shadows = LightShadows.None;

            BoxCollider collider = root.AddComponent<BoxCollider>();
            collider.center = new Vector3(0, 0.35f, 0);
            collider.size = new Vector3(1.0f, 0.7f, 1.0f);
            Station.Create(root, StationKind.Fire, lifetime > 0 ? "Fire" : "Hearth", town);
            return fire;
        }

        private void Update()
        {
            flicker += Time.deltaTime * 9f;
            if (glow != null) glow.intensity = 1.5f + Mathf.Sin(flicker) * 0.15f + Mathf.Sin(flicker * 2.3f) * 0.1f;
            if (lifetime <= 0) return;
            age += Time.deltaTime;
            if (age >= lifetime)
            {
                GameLog.Add("The fire burns out.");
                Destroy(gameObject);
            }
        }
    }
}
