using UnityEngine;

namespace RPG.Core
{
    /// <summary>
    /// Orbit camera that follows the player. Left/right arrows (or middle-mouse drag) turn it, up/down
    /// arrows tilt it, the mouse wheel zooms. The child camera is placed on a sphere around the target.
    /// CloseUp frames the character's face for the character creation screen.
    /// </summary>
    public class FollowCamera : MonoBehaviour
    {
        [SerializeField] Transform target;

        public const float MinPitch = 18f, MaxPitch = 78f;
        public const float MinDistance = 4f, MaxDistance = 20f;

        public static FollowCamera Instance { get; private set; }

        // degrees per second for the arrow keys (set from the settings menu)
        public static float TurnSpeed = 110f;

        public float Yaw { get; private set; }
        public float Pitch { get; private set; }
        public float Distance { get; private set; }

        /// <summary>Set to frame the target's face from the front (character creation).</summary>
        public bool CloseUp;
        public float CloseUpYawOffset;

        Transform cameraTransform;
        bool initialised;
        Vector3 lastMouse;

        public Transform Target { get { return target; } set { target = value; } }

        private void Awake()
        {
            Instance = this;
            Init();
        }

        void Init()
        {
            if (initialised) return;
            initialised = true;
            Camera camera = GetComponentInChildren<Camera>();
            cameraTransform = camera != null ? camera.transform : null;
            if (cameraTransform == null) return;
            // start from the prefab's framing
            Vector3 offset = cameraTransform.localPosition;
            Distance = Mathf.Clamp(offset.magnitude, MinDistance, MaxDistance);
            Pitch = Mathf.Clamp(Mathf.Asin(Mathf.Clamp(offset.y / Mathf.Max(0.01f, offset.magnitude), -1f, 1f)) * Mathf.Rad2Deg, MinPitch, MaxPitch);
            Vector3 flat = -new Vector3(offset.x, 0, offset.z);
            Yaw = (transform.eulerAngles.y + Mathf.Atan2(flat.x, flat.z) * Mathf.Rad2Deg) % 360f;
        }

        /// <summary>The flat forward direction the camera looks along (for WASD movement).</summary>
        public Vector3 FlatForward { get { return Quaternion.Euler(0, Yaw, 0) * Vector3.forward; } }
        public Vector3 FlatRight { get { return Quaternion.Euler(0, Yaw, 0) * Vector3.right; } }

        public void SetYaw(float yaw)
        {
            Yaw = yaw;
        }

        void LateUpdate()
        {
            Init();
            if (target == null || cameraTransform == null) return;
            transform.position = target.position;

            if (!CloseUp && !UiState.Blocked)
            {
                float dt = Time.unscaledDeltaTime;
                float turn = 0f;
                if (Input.GetKey(KeyCode.LeftArrow)) turn += 1f;
                if (Input.GetKey(KeyCode.RightArrow)) turn -= 1f;
                Yaw += turn * TurnSpeed * dt;
                float tilt = 0f;
                if (Input.GetKey(KeyCode.UpArrow)) tilt += 1f;
                if (Input.GetKey(KeyCode.DownArrow)) tilt -= 1f;
                Pitch = Mathf.Clamp(Pitch + tilt * 70f * dt, MinPitch, MaxPitch);
                if (!UiState.PointerOverUi)
                {
                    float wheel = Input.mouseScrollDelta.y;
                    if (Mathf.Abs(wheel) > 0.01f) Distance = Mathf.Clamp(Distance * (1f - wheel * 0.1f), MinDistance, MaxDistance);
                }
                if (Input.GetMouseButtonDown(2)) lastMouse = Input.mousePosition;
                if (Input.GetMouseButton(2))
                {
                    Vector3 delta = Input.mousePosition - lastMouse;
                    lastMouse = Input.mousePosition;
                    Yaw += delta.x * 0.3f;
                    Pitch = Mathf.Clamp(Pitch - delta.y * 0.2f, MinPitch, MaxPitch);
                }
            }

            float yaw = Yaw, pitch = Pitch, distance = Distance;
            Vector3 look = Vector3.up * 1.0f;
            if (CloseUp)
            {
                yaw = target.eulerAngles.y + 180f + CloseUpYawOffset;
                pitch = 6f;
                distance = 3.1f;
                look = Vector3.up * 1.05f;
            }
            transform.rotation = Quaternion.Euler(0, yaw, 0);
            Quaternion tiltRotation = Quaternion.Euler(pitch, 0, 0);
            cameraTransform.localPosition = look + tiltRotation * new Vector3(0, 0, -distance);
            cameraTransform.localRotation = tiltRotation;
        }
    }
}
