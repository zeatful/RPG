using UnityEngine;

namespace RPG.Core
{
    public static class UiState
    {
        // Set by the HUD every GUI pass; world clicks are ignored while the mouse is over a panel.
        public static bool PointerOverUi;
    }

    public static class RpgUtil
    {
        public static Color FromHex(string hex, Color fallback)
        {
            Color color;
            if (!string.IsNullOrEmpty(hex) && ColorUtility.TryParseHtmlString(hex, out color)) return color;
            return fallback;
        }

        public static float FlatDistance(Vector3 a, Vector3 b)
        {
            a.y = 0;
            b.y = 0;
            return Vector3.Distance(a, b);
        }

        /// <summary>Tint every renderer under a GameObject (multiplies over existing textures).</summary>
        public static void Tint(GameObject root, Color tint, float amount)
        {
            foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>())
            {
                foreach (Material material in renderer.materials)
                {
                    if (material.HasProperty("_Color"))
                    {
                        material.color = Color.Lerp(material.color, tint, amount);
                    }
                }
            }
        }

        public static void SetColor(GameObject go, Color color)
        {
            Renderer renderer = go.GetComponent<Renderer>();
            if (renderer != null) renderer.material.color = color;
        }

        public static void SetGlow(GameObject go, Color glow)
        {
            Renderer renderer = go.GetComponent<Renderer>();
            if (renderer == null) return;
            Material material = renderer.material;
            if (!material.HasProperty("_EmissionColor")) return;
            material.EnableKeyword("_EMISSION");
            material.SetColor("_EmissionColor", glow);
        }

        /// <summary>Create a primitive child with no collider.</summary>
        public static GameObject Part(PrimitiveType type, Transform parent, Vector3 localPos, Vector3 scale, Color color)
        {
            GameObject part = GameObject.CreatePrimitive(type);
            Collider collider = part.GetComponent<Collider>();
            if (collider != null) Object.Destroy(collider);
            part.transform.SetParent(parent, false);
            part.transform.localPosition = localPos;
            part.transform.localScale = scale;
            SetColor(part, color);
            return part;
        }
    }
}
