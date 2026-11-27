using RPG.Core;
using RPG.Game;
using UnityEngine;

namespace RPG.UI
{
    /// <summary>
    /// The Esc menu: resume, save, settings (volume, effects volume, fullscreen, graphics quality, vsync,
    /// camera turn speed, all remembered between runs), controls, back to character select, quit.
    /// The game is paused while it's open. Esc first closes any open panel (shop, bank, map).
    /// </summary>
    public class GameMenu : MonoBehaviour
    {
        enum Page { Main, Settings, Controls }

        public static GameMenu Instance { get; private set; }
        public bool IsOpen { get; private set; }

        Page page;
        RpgHud hud;
        string toast;
        float toastTime;
        bool confirmQuit;
        GUIStyle title, label, small, bold;
        bool styles;

        public static float MasterVolume = 1f;

        private void Awake()
        {
            Instance = this;
            LoadSettings();
        }

        private void Start()
        {
            hud = GetComponent<RpgHud>();
        }

        public static void LoadSettings()
        {
            MasterVolume = PlayerPrefs.GetFloat("rpg.master", 0.9f);
            Sfx.Volume = PlayerPrefs.GetFloat("rpg.sfx", 0.8f);
            FollowCamera.TurnSpeed = PlayerPrefs.GetFloat("rpg.turn", 110f);
            AudioListener.volume = MasterVolume;
            int quality = PlayerPrefs.GetInt("rpg.quality", -1);
            if (quality >= 0 && quality < QualitySettings.names.Length) QualitySettings.SetQualityLevel(quality, true);
            QualitySettings.vSyncCount = PlayerPrefs.GetInt("rpg.vsync", 1);
        }

        static void SaveSettings()
        {
            PlayerPrefs.SetFloat("rpg.master", MasterVolume);
            PlayerPrefs.SetFloat("rpg.sfx", Sfx.Volume);
            PlayerPrefs.SetFloat("rpg.turn", FollowCamera.TurnSpeed);
            PlayerPrefs.SetInt("rpg.quality", QualitySettings.GetQualityLevel());
            PlayerPrefs.SetInt("rpg.vsync", QualitySettings.vSyncCount);
            PlayerPrefs.Save();
        }

        public void Open()
        {
            if (IsOpen) return;
            IsOpen = true;
            page = Page.Main;
            confirmQuit = false;
            Time.timeScale = 0f;
            UiState.Blocked = true;
            Sfx.Ui("ui", 0.5f);
        }

        public void Close()
        {
            if (!IsOpen) return;
            IsOpen = false;
            Time.timeScale = 1f;
            UiState.Blocked = false;
            SaveSettings();
        }

        private void Update()
        {
            if (GameSession.Instance != null && !GameSession.Instance.Playing) return;
            if (!Input.GetKeyDown(KeyCode.Escape)) return;
            if (IsOpen)
            {
                if (page != Page.Main) page = Page.Main;
                else Close();
                return;
            }
            // Esc closes the top-most panel first, then opens the menu
            if (hud != null && hud.CloseTopPanel()) return;
            Open();
        }

        void InitStyles()
        {
            if (styles) return;
            styles = true;
            title = new GUIStyle(GUI.skin.label) { fontSize = 24, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            title.normal.textColor = new Color(1f, 0.88f, 0.55f);
            label = new GUIStyle(GUI.skin.label) { fontSize = 14 };
            label.normal.textColor = Color.white;
            small = new GUIStyle(label) { fontSize = 12, wordWrap = true };
            bold = new GUIStyle(label) { fontStyle = FontStyle.Bold };
        }

        static void Box(Rect rect, Color color)
        {
            Color old = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = old;
        }

        private void OnGUI()
        {
            if (!IsOpen && string.IsNullOrEmpty(toast)) return;
            InitStyles();
            GUI.depth = -60;
            float scale = Mathf.Max(1f, Screen.height / 800f);
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1));
            float width = Screen.width / scale, height = Screen.height / scale;

            if (!IsOpen)
            {
                if (Time.unscaledTime - toastTime < 2f) GUI.Label(new Rect(0, height * 0.3f, width, 30), toast, title);
                else toast = null;
                return;
            }

            Box(new Rect(0, 0, width, height), new Color(0, 0, 0, 0.55f));
            float w = 380, h = page == Page.Main ? 420 : 470;
            Rect panel = new Rect((width - w) / 2, (height - h) / 2, w, h);
            Box(panel, new Color(0.08f, 0.07f, 0.06f, 0.95f));
            Box(new Rect(panel.x, panel.y, panel.width, 3), new Color(0.75f, 0.6f, 0.3f, 1f));
            float x = panel.x + 30, y = panel.y + 18, bw = w - 60;

            if (page == Page.Main)
            {
                GUI.Label(new Rect(panel.x, y, w, 34), "Paused", title);
                y += 48;
                GameSession session = GameSession.Instance;
                if (Button(x, ref y, bw, "Resume")) Close();
                bool canSave = session != null && session.Character != null && !session.Ephemeral;
                GUI.enabled = canSave;
                if (Button(x, ref y, bw, canSave ? "Save game" : "Save game (not available in tests)"))
                {
                    if (session.Save("Saved")) { toast = "Game saved"; toastTime = Time.unscaledTime; Close(); }
                }
                GUI.enabled = true;
                if (Button(x, ref y, bw, "Settings")) page = Page.Settings;
                if (Button(x, ref y, bw, "Controls")) page = Page.Controls;
                if (Button(x, ref y, bw, "Save and exit to character select") && session != null) { Close(); session.ExitToSelect(); }
                if (!confirmQuit)
                {
                    if (Button(x, ref y, bw, "Quit game")) confirmQuit = true;
                }
                else
                {
                    GUI.Label(new Rect(x, y, bw, 20), "Your character is saved before quitting.", small);
                    y += 22;
                    if (GUI.Button(new Rect(x, y, bw / 2 - 4, 36), "Save and quit"))
                    {
                        Close();
                        if (session != null) session.QuitGame(); else Application.Quit();
                    }
                    if (GUI.Button(new Rect(x + bw / 2 + 4, y, bw / 2 - 4, 36), "Cancel")) confirmQuit = false;
                    y += 44;
                }
                if (session != null && session.LastSaveTime > 0)
                {
                    int ago = Mathf.RoundToInt(Time.unscaledTime - session.LastSaveTime);
                    GUI.Label(new Rect(x, panel.yMax - 28, bw, 20), session.LastSaveText + " " + (ago < 60 ? ago + "s" : ago / 60 + "m") + " ago  (autosaves every minute, F5 to save)", small);
                }
            }
            else if (page == Page.Settings)
            {
                GUI.Label(new Rect(panel.x, y, w, 34), "Settings", title);
                y += 46;
                MasterVolume = Slider(x, ref y, bw, "Master volume", MasterVolume, 0f, 1f, Mathf.RoundToInt(MasterVolume * 100) + "%");
                AudioListener.volume = MasterVolume;
                float before = Sfx.Volume;
                Sfx.Volume = Slider(x, ref y, bw, "Sound effects", Sfx.Volume, 0f, 1f, Mathf.RoundToInt(Sfx.Volume * 100) + "%");
                if (Mathf.Abs(before - Sfx.Volume) > 0.05f) Sfx.Ui("ui", 0.6f);
                FollowCamera.TurnSpeed = Slider(x, ref y, bw, "Camera turn speed (arrow keys)", FollowCamera.TurnSpeed, 40f, 240f, Mathf.RoundToInt(FollowCamera.TurnSpeed) + " deg/s");

                bool full = GUI.Toggle(new Rect(x, y, bw, 24), Screen.fullScreen, "  Fullscreen");
                if (full != Screen.fullScreen) Screen.fullScreen = full;
                y += 30;
                bool vsync = GUI.Toggle(new Rect(x, y, bw, 24), QualitySettings.vSyncCount > 0, "  VSync");
                if (vsync != QualitySettings.vSyncCount > 0) QualitySettings.vSyncCount = vsync ? 1 : 0;
                y += 34;
                string[] names = QualitySettings.names;
                int level = QualitySettings.GetQualityLevel();
                GUI.Label(new Rect(x, y, bw, 20), "Graphics quality", label);
                y += 22;
                if (GUI.Button(new Rect(x, y, 40, 30), "<") && level > 0) QualitySettings.SetQualityLevel(level - 1, true);
                GUI.Label(new Rect(x + 50, y + 4, bw - 100, 24), names.Length > 0 ? names[Mathf.Clamp(QualitySettings.GetQualityLevel(), 0, names.Length - 1)] : "-", new GUIStyle(bold) { alignment = TextAnchor.UpperCenter });
                if (GUI.Button(new Rect(x + bw - 40, y, 40, 30), ">") && level < names.Length - 1) QualitySettings.SetQualityLevel(level + 1, true);
                y += 44;
                if (Button(x, ref y, bw, "Back")) { SaveSettings(); page = Page.Main; }
            }
            else
            {
                GUI.Label(new Rect(panel.x, y, w, 34), "Controls", title);
                y += 46;
                string[] lines =
                {
                    "Left-click: walk, attack, gather, pick up, use, enter doors",
                    "W A S D: walk / run (relative to the camera)",
                    "Arrow keys: turn and tilt the camera   Mouse wheel: zoom",
                    "Middle mouse drag: turn the camera",
                    "R: toggle walk / run (running uses stamina)",
                    "I: inventory   E: equipment   K: skills   M: world map",
                    "1 / 2 / 3: Slash (Attack) / Crush (Strength) / Shield bash (Defense)",
                    "Inventory: left-click to use, eat or equip; right-click to drop",
                    "F5: save   Esc: close panel / this menu",
                };
                foreach (string line in lines)
                {
                    GUI.Label(new Rect(x, y, bw, 40), line, small);
                    y += 32;
                }
                y = panel.yMax - 56;
                if (Button(x, ref y, bw, "Back")) page = Page.Main;
            }
        }

        bool Button(float x, ref float y, float w, string text)
        {
            bool pressed = GUI.Button(new Rect(x, y, w, 38), text);
            y += 46;
            if (pressed) Sfx.Ui("ui", 0.45f);
            return pressed;
        }

        float Slider(float x, ref float y, float w, string name, float value, float min, float max, string shown)
        {
            GUI.Label(new Rect(x, y, w - 80, 20), name, label);
            GUI.Label(new Rect(x + w - 80, y, 80, 20), shown, new GUIStyle(label) { alignment = TextAnchor.UpperRight });
            y += 22;
            float result = GUI.HorizontalSlider(new Rect(x, y + 4, w, 16), value, min, max);
            y += 32;
            return result;
        }
    }
}
