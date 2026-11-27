using RPG.Control;
using UnityEngine;

namespace RPG.UI
{
    /// <summary>
    /// Renders a live, transparent-background picture of the player (only the player layer) for the
    /// equipment panel, so it shows exactly what is equipped. Only renders while something asks for it.
    /// </summary>
    public class PlayerPortrait : MonoBehaviour
    {
        public RenderTexture Texture { get; private set; }

        Camera portraitCamera;
        Transform target;
        float wantedUntil;
        public float Turn;

        /// <summary>Call every frame the picture is on screen.</summary>
        public void Want()
        {
            wantedUntil = Time.unscaledTime + 0.25f;
        }

        private void Start()
        {
            GameObject player = GameObject.FindWithTag("Player");
            target = player != null ? player.transform : null;
            Texture = new RenderTexture(256, 336, 16, RenderTextureFormat.ARGB32);
            Texture.name = "Player portrait";
            GameObject go = new GameObject("Portrait camera");
            portraitCamera = go.AddComponent<Camera>();
            portraitCamera.enabled = false;
            portraitCamera.clearFlags = CameraClearFlags.SolidColor;
            portraitCamera.backgroundColor = new Color(0, 0, 0, 0);
            portraitCamera.cullingMask = 1 << CharacterAppearance.PlayerLayer;
            portraitCamera.fieldOfView = 26f;
            portraitCamera.nearClipPlane = 0.1f;
            portraitCamera.farClipPlane = 20f;
            portraitCamera.targetTexture = Texture;
        }

        private void LateUpdate()
        {
            if (target == null || portraitCamera == null || Time.unscaledTime > wantedUntil) return;
            Quaternion around = Quaternion.Euler(0, target.eulerAngles.y + 180f + 25f + Turn, 0);
            Vector3 look = target.position + Vector3.up * 0.95f;
            portraitCamera.transform.position = look + around * Vector3.forward * -4.6f + Vector3.up * 0.25f;
            portraitCamera.transform.LookAt(look);
            portraitCamera.Render();
        }

        private void OnDestroy()
        {
            if (Texture != null) Texture.Release();
        }
    }
}
