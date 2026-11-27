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

        /// <summary>
        /// Instantiate a model and fit it into a box of the given size (feet at y = 0, centred). Pass 0 for
        /// x/z to scale those axes with the height. Returns a root whose child carries the scale.
        /// </summary>
        public static GameObject FitModel(GameObject prefab, Vector3 size)
        {
            GameObject root = new GameObject(prefab.name + " (fitted)");
            GameObject holder = new GameObject("Scale");
            holder.transform.SetParent(root.transform, false);
            GameObject model = Object.Instantiate(prefab, holder.transform, false);
            model.name = prefab.name;
            foreach (Collider collider in model.GetComponentsInChildren<Collider>()) Object.Destroy(collider);

            // Measure the model as it will look while animating: sample its idle (or first) clip first,
            // because exported armatures often carry their real scale/offset in the animation.
            Animation animation = model.GetComponentInChildren<Animation>();
            if (animation != null)
            {
                string clip = null;
                foreach (AnimationState state in animation)
                {
                    if (clip == null || state.name.ToLower().Contains("idle")) clip = state.name;
                }
                if (clip != null)
                {
                    animation.Play(clip);
                    animation.Sample();
                }
            }
            foreach (SkinnedMeshRenderer skinned in model.GetComponentsInChildren<SkinnedMeshRenderer>()) skinned.updateWhenOffscreen = true;

            Bounds bounds = new Bounds();
            bool first = true;
            foreach (Renderer renderer in model.GetComponentsInChildren<Renderer>())
            {
                if (first) { bounds = renderer.bounds; first = false; }
                else bounds.Encapsulate(renderer.bounds);
            }
            if (first || bounds.size.y <= 0.0001f) return root;

            model.transform.localPosition -= new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);
            float uniform = size.y / bounds.size.y;
            holder.transform.localScale = new Vector3(
                size.x > 0 ? size.x / Mathf.Max(0.0001f, bounds.size.x) : uniform,
                uniform,
                size.z > 0 ? size.z / Mathf.Max(0.0001f, bounds.size.z) : uniform);
            return root;
        }

        /// <summary>Recolour (instanced) materials on a model whose name contains the given text; null matches all.</summary>
        public static void Recolor(GameObject root, string materialNameContains, Color color)
        {
            foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>())
            {
                Material[] materials = renderer.materials;
                foreach (Material material in materials)
                {
                    if (materialNameContains != null && !material.name.Contains(materialNameContains)) continue;
                    if (material.HasProperty("_Color")) material.color = color;
                    if (material.HasProperty("_Glossiness")) material.SetFloat("_Glossiness", 0.1f);
                }
                renderer.materials = materials;
            }
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
