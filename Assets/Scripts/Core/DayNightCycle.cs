using UnityEngine;

namespace RPG.Core
{
    /// <summary>
    /// Rotates the scene's directional light through a day and dims light + ambient at night.
    /// One in-game day lasts DayLengthSeconds of real time (default 8 minutes).
    /// </summary>
    public class DayNightCycle : MonoBehaviour
    {
        public float DayLengthSeconds = 480f;

        // 0 = midnight, 0.25 = 6:00, 0.5 = noon
        [Range(0, 1)] public float TimeOfDay = 0.375f;

        public static DayNightCycle Instance { get; private set; }

        Light sun;
        float baseIntensity = 1f;
        float baseAmbient = 1f;
        Color baseColor = Color.white;
        float baseYaw;
        int day = 1;

        public int Day { get { return day; } }
        public bool IsNight { get { return TimeOfDay < 0.22f || TimeOfDay > 0.8f; } }

        public string ClockText
        {
            get
            {
                int minutes = Mathf.FloorToInt(TimeOfDay * 24f * 60f);
                return "Day " + day + "  " + (minutes / 60).ToString("00") + ":" + (minutes % 60).ToString("00") + (IsNight ? "  (night)" : "");
            }
        }

        private void Awake()
        {
            Instance = this;
            foreach (Light light in FindObjectsByType<Light>(FindObjectsSortMode.None))
            {
                if (light.type == LightType.Directional)
                {
                    sun = light;
                    break;
                }
            }
            if (sun != null)
            {
                baseIntensity = sun.intensity;
                baseColor = sun.color;
                baseYaw = sun.transform.eulerAngles.y;
            }
            baseAmbient = RenderSettings.ambientIntensity;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void Update()
        {
            TimeOfDay += Time.deltaTime / Mathf.Max(10f, DayLengthSeconds);
            if (TimeOfDay >= 1f)
            {
                TimeOfDay -= 1f;
                day++;
            }
            Apply();
        }

        void Apply()
        {
            // sun height: -1 at midnight, +1 at noon
            float height = -Mathf.Cos(TimeOfDay * Mathf.PI * 2f);
            float daylight = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(-0.15f, 0.35f, height));

            RenderSettings.ambientIntensity = Mathf.Lerp(baseAmbient * 0.35f, baseAmbient, daylight);
            if (sun == null) return;

            float pitch = Mathf.Lerp(-10f, 70f, Mathf.Clamp01((height + 0.2f) / 1.2f));
            sun.transform.rotation = Quaternion.Euler(pitch, baseYaw + TimeOfDay * 360f, 0);
            sun.intensity = Mathf.Lerp(baseIntensity * 0.12f, baseIntensity, daylight);

            Color dusk = new Color(1f, 0.6f, 0.4f);
            Color night = new Color(0.45f, 0.55f, 0.9f);
            Color tint = daylight < 0.5f ? Color.Lerp(night, dusk, daylight * 2f) : Color.Lerp(dusk, baseColor, (daylight - 0.5f) * 2f);
            sun.color = tint;
        }
    }
}
