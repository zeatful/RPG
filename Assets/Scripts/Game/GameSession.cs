using System;
using System.Collections.Generic;
using RPG.Control;
using RPG.Core;
using RPG.Saving;
using RPG.Stats;
using RPG.UI;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace RPG.Game
{
    /// <summary>
    /// The flow around play: the character select screen (continue a saved character or delete one), the
    /// character creation screen (unique name, skin / hair / clothing colours and a hair style, previewed
    /// live on the real character), then play with autosave every minute, on quit, and from the Esc menu.
    /// Tests and the showcase skip straight to play with nothing saved.
    /// </summary>
    public class GameSession : MonoBehaviour
    {
        public enum Screen { Select, Create, Playing }

        public const float AutosaveSeconds = 60f;

        public static GameSession Instance { get; private set; }

        public Screen Current { get; private set; }
        public bool Playing { get { return Current == Screen.Playing; } }
        public CharacterMeta Character { get; private set; }
        public float LastSaveTime { get; private set; }
        public string LastSaveText { get; private set; }

        /// <summary>No saving at all (automated tests, showcase).</summary>
        public bool Ephemeral { get; private set; }

        GameObject player;
        CharacterAppearance appearance;
        SavingSystem saving;
        List<CharacterMeta> characters = new List<CharacterMeta>();
        AppearanceData draft = new AppearanceData();
        string draftName = "";
        string nameError;
        CharacterMeta confirmDelete;
        float autosaveTimer;
        Vector2 listScroll;
        GUIStyle title, label, small, bold, error, field;
        bool styles;

        private void Awake()
        {
            Instance = this;
        }

        private void Start()
        {
            player = GameObject.FindWithTag("Player");
            appearance = player != null ? player.GetComponent<CharacterAppearance>() : null;
            saving = FindFirstObjectByType<SavingSystem>();
            Ephemeral = SmokeTest.Requested || Showcase.RequestedDir != null;
            if (Ephemeral)
            {
                Current = Screen.Playing;
                return;
            }
            characters = CharacterStore.List();
            Current = characters.Count > 0 ? Screen.Select : Screen.Create;
            if (Current == Screen.Create) NewDraft();
            EnterMenu();
        }

        void EnterMenu()
        {
            UiState.Blocked = true;
            if (FollowCamera.Instance != null) FollowCamera.Instance.CloseUp = true;
        }

        void LeaveMenu()
        {
            UiState.Blocked = false;
            UiState.Typing = false;
            if (FollowCamera.Instance != null)
            {
                FollowCamera.Instance.CloseUp = false;
                FollowCamera.Instance.CloseUpYawOffset = 0f;
            }
            Current = Screen.Playing;
            autosaveTimer = 0;
        }

        void NewDraft()
        {
            System.Random random = new System.Random();
            draft = new AppearanceData
            {
                Skin = random.Next(CharacterAppearance.SkinColors.Length),
                Hair = random.Next(CharacterAppearance.HairColors.Length),
                HairStyle = random.Next(CharacterAppearance.HairStyles.Length - 1),
                Shirt = random.Next(CharacterAppearance.ShirtColors.Length),
                Trousers = random.Next(CharacterAppearance.TrouserColors.Length),
            };
            draftName = "";
            nameError = null;
            if (appearance != null) appearance.SetAppearance(draft);
        }

        // ------------------------------------------------------------------ play / save

        void PlayCharacter(CharacterMeta meta)
        {
            Character = meta;
            if (saving != null && SaveExists(meta)) saving.Load(meta.SaveFile);
            else if (appearance != null) appearance.SetAppearance(meta.Look);
            LeaveMenu();
            GameLog.Add("Welcome back, " + meta.Name + ".", GameLog.LevelUp);
            Sfx.Ui("ui", 0.6f);
        }

        static bool SaveExists(CharacterMeta meta)
        {
            return System.IO.File.Exists(System.IO.Path.Combine(Application.persistentDataPath, meta.SaveFile + ".sav"));
        }

        void CreateCharacter()
        {
            nameError = CharacterStore.Validate(draftName);
            if (nameError != null) return;
            draft.Name = draftName;
            Character = CharacterStore.Create(draftName, draft);
            if (appearance != null) appearance.SetAppearance(draft);
            LeaveMenu();
            Save("Character created");
            GameLog.Add("Welcome to Brookhollow, " + draftName + "! You wake at the spawn stones with a sword, an axe, a pickaxe and a hammer.", GameLog.LevelUp);
            GameLog.Add("Wield your axe (click it in your bag) before chopping trees, and your pickaxe before mining.");
            Sfx.Ui("levelup", 0.5f);
        }

        /// <summary>Saves the current character (if any). Returns true if something was written.</summary>
        public bool Save(string reason)
        {
            if (Ephemeral || Character == null || saving == null || player == null) return false;
            Health health = player.GetComponent<Health>();
            if (health != null && health.IsDead()) return false;
            saving.Save(Character.SaveFile);
            SkillSet skills = player.GetComponent<SkillSet>();
            if (skills != null)
            {
                Character.CombatLevel = skills.CombatLevel();
                Character.TotalLevel = skills.TotalLevel();
            }
            if (appearance != null) Character.Look = appearance.Data.Clone();
            Character.LastPlayedTicks = DateTime.UtcNow.Ticks;
            CharacterStore.Write(Character);
            LastSaveTime = Time.unscaledTime;
            LastSaveText = reason;
            return true;
        }

        /// <summary>Saves and reloads the world, back to the character select screen.</summary>
        public void ExitToSelect()
        {
            Save("Saved");
            Time.timeScale = 1f;
            UiState.Blocked = false;
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }

        public void QuitGame()
        {
            Save("Saved");
            Application.Quit();
        }

        private void OnApplicationQuit()
        {
            if (Playing) Save("Saved on quit");
        }

        private void Update()
        {
            if (!Playing || Ephemeral) return;
            autosaveTimer += Time.unscaledDeltaTime;
            if (autosaveTimer >= AutosaveSeconds)
            {
                autosaveTimer = 0;
                if (Save("Autosaved")) Debug.Log("[RPG] autosaved " + Character.Name);
            }
            if (Input.GetKeyDown(KeyCode.F5) && !UiState.Typing)
            {
                if (Save("Saved")) GameLog.Add("Game saved.");
            }
        }

        // ------------------------------------------------------------------ screens

        void InitStyles()
        {
            if (styles) return;
            styles = true;
            title = new GUIStyle(GUI.skin.label) { fontSize = 30, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            title.normal.textColor = new Color(1f, 0.88f, 0.55f);
            label = new GUIStyle(GUI.skin.label) { fontSize = 14 };
            label.normal.textColor = Color.white;
            small = new GUIStyle(label) { fontSize = 12 };
            small.normal.textColor = new Color(0.85f, 0.85f, 0.85f);
            bold = new GUIStyle(label) { fontStyle = FontStyle.Bold, fontSize = 16 };
            error = new GUIStyle(small);
            error.normal.textColor = new Color(1f, 0.5f, 0.45f);
            field = new GUIStyle(GUI.skin.textField) { fontSize = 18 };
        }

        private void OnGUI()
        {
            if (Playing) return;
            InitStyles();
            GUI.depth = -50;
            float scale = Mathf.Max(1f, UnityEngine.Screen.height / 800f);
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1));
            float width = UnityEngine.Screen.width / scale, height = UnityEngine.Screen.height / scale;

            // darken the left of the screen; the character stands on the right
            Box(new Rect(0, 0, 470, height), new Color(0.05f, 0.04f, 0.03f, 0.82f));
            Box(new Rect(470, 0, 2, height), new Color(0.75f, 0.6f, 0.3f, 0.9f));
            GUI.Label(new Rect(0, 26, 470, 44), "Brookhollow", title);
            GUI.Label(new Rect(0, 66, 470, 22), "an old-school adventure", new GUIStyle(small) { alignment = TextAnchor.MiddleCenter });

            if (Current == Screen.Select) DrawSelect(height);
            else DrawCreate(height);

            // rotate the preview
            if (GUI.Button(new Rect(width - 190, height - 60, 80, 34), "< Turn")) FollowCamera.Instance.CloseUpYawOffset += 30f;
            if (GUI.Button(new Rect(width - 100, height - 60, 80, 34), "Turn >")) FollowCamera.Instance.CloseUpYawOffset -= 30f;
        }

        static void Box(Rect rect, Color color)
        {
            Color old = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = old;
        }

        void DrawSelect(float height)
        {
            float x = 30, y = 110, w = 410;
            GUI.Label(new Rect(x, y, w, 24), "Choose your character", bold);
            y += 34;
            Rect area = new Rect(x, y, w, height - y - 150);
            Rect view = new Rect(0, 0, w - 20, characters.Count * 74);
            listScroll = GUI.BeginScrollView(area, listScroll, view);
            for (int i = 0; i < characters.Count; i++)
            {
                CharacterMeta meta = characters[i];
                Rect row = new Rect(0, i * 74, view.width, 68);
                Box(row, new Color(0.18f, 0.15f, 0.12f, 0.9f));
                // little colour chips for the look
                Box(new Rect(row.x + 10, row.y + 12, 14, 44), CharacterAppearance.Pick(CharacterAppearance.ShirtColors, meta.Look.Shirt));
                Box(new Rect(row.x + 26, row.y + 12, 14, 20), CharacterAppearance.Pick(CharacterAppearance.HairColors, meta.Look.Hair));
                Box(new Rect(row.x + 26, row.y + 34, 14, 22), CharacterAppearance.Pick(CharacterAppearance.SkinColors, meta.Look.Skin));
                GUI.Label(new Rect(row.x + 50, row.y + 8, 200, 24), meta.Name, bold);
                string played = meta.LastPlayedTicks > 0 ? new DateTime(meta.LastPlayedTicks, DateTimeKind.Utc).ToLocalTime().ToString("MMM d, HH:mm") : "-";
                GUI.Label(new Rect(row.x + 50, row.y + 34, 240, 20), "Combat " + meta.CombatLevel + "   Total " + meta.TotalLevel + "   " + played, small);
                if (confirmDelete == meta)
                {
                    if (GUI.Button(new Rect(row.xMax - 170, row.y + 18, 80, 32), "Delete!"))
                    {
                        CharacterStore.Delete(meta);
                        characters = CharacterStore.List();
                        confirmDelete = null;
                        if (characters.Count == 0) { Current = Screen.Create; NewDraft(); }
                        GUI.EndScrollView();
                        return;
                    }
                    if (GUI.Button(new Rect(row.xMax - 84, row.y + 18, 74, 32), "Keep")) confirmDelete = null;
                }
                else
                {
                    if (GUI.Button(new Rect(row.xMax - 150, row.y + 14, 80, 40), "Play")) { PlayCharacter(meta); GUI.EndScrollView(); return; }
                    if (GUI.Button(new Rect(row.xMax - 64, row.y + 20, 54, 28), "Delete")) confirmDelete = meta;
                }
                // preview on hover
                if (row.Contains(Event.current.mousePosition) && appearance != null && appearance.Data.Name != meta.Look.Name) appearance.SetAppearance(meta.Look);
            }
            GUI.EndScrollView();
            if (GUI.Button(new Rect(x, height - 130, w, 44), "Create a new character")) { Current = Screen.Create; NewDraft(); }
            if (GUI.Button(new Rect(x, height - 76, w, 34), "Quit")) Application.Quit();
        }

        void DrawCreate(float height)
        {
            float x = 30, y = 106, w = 410;
            GUI.Label(new Rect(x, y, w, 24), "Create your character", bold);
            y += 34;
            GUI.Label(new Rect(x, y, w, 20), "Name (" + CharacterStore.MinName + "-" + CharacterStore.MaxName + " letters, must be unique)", small);
            y += 22;
            GUI.SetNextControlName("charname");
            string typed = GUI.TextField(new Rect(x, y, w, 32), draftName, CharacterStore.MaxName, field);
            UiState.Typing = GUI.GetNameOfFocusedControl() == "charname";
            if (typed != draftName)
            {
                draftName = typed;
                nameError = draftName.Length > 0 ? CharacterStore.Validate(draftName) : null;
            }
            y += 36;
            if (nameError != null) GUI.Label(new Rect(x, y, w, 20), nameError, error);
            else if (draftName.Length >= CharacterStore.MinName) GUI.Label(new Rect(x, y, w, 20), "\"" + draftName + "\" is available.", new GUIStyle(small) { normal = { textColor = new Color(0.55f, 1f, 0.55f) } });
            y += 28;

            bool changed = false;
            changed |= Swatches(ref y, x, w, "Skin", CharacterAppearance.SkinColors, ref draft.Skin);
            changed |= Swatches(ref y, x, w, "Hair colour", CharacterAppearance.HairColors, ref draft.Hair);
            GUI.Label(new Rect(x, y, w, 20), "Hair style", small);
            y += 22;
            float bw = (w - 3 * 6) / 4f;
            for (int i = 0; i < CharacterAppearance.HairStyles.Length; i++)
            {
                bool on = GUI.Toggle(new Rect(x + i * (bw + 6), y, bw, 28), draft.HairStyle == i, CharacterAppearance.HairStyles[i], "Button");
                if (on && draft.HairStyle != i) { draft.HairStyle = i; changed = true; }
            }
            y += 40;
            changed |= Swatches(ref y, x, w, "Shirt", CharacterAppearance.ShirtColors, ref draft.Shirt);
            changed |= Swatches(ref y, x, w, "Trousers", CharacterAppearance.TrouserColors, ref draft.Trousers);
            GUI.Label(new Rect(x, y, w, 36), "You start in plain clothes with a bronze sword, axe and pickaxe. Armour comes later: buy it, smith it or loot it.", new GUIStyle(small) { wordWrap = true });
            y += 44;

            if (GUI.Button(new Rect(x, y, 130, 32), "Randomise"))
            {
                string keepName = draftName;
                NewDraft();
                draftName = keepName;
                nameError = draftName.Length > 0 ? CharacterStore.Validate(draftName) : null;
            }
            if (changed && appearance != null) appearance.SetAppearance(draft);

            bool oldEnabled = GUI.enabled;
            GUI.enabled = draftName.Length >= CharacterStore.MinName && nameError == null;
            if (GUI.Button(new Rect(x, height - 120, w, 48), "Create and play")) CreateCharacter();
            GUI.enabled = oldEnabled;
            if (characters.Count > 0 && GUI.Button(new Rect(x, height - 64, w, 32), "Back to character select"))
            {
                Current = Screen.Select;
                UiState.Typing = false;
            }
            if (Event.current.type == EventType.KeyDown && (Event.current.keyCode == KeyCode.Return || Event.current.keyCode == KeyCode.KeypadEnter) && GUI.enabled) CreateCharacter();
        }

        bool Swatches(ref float y, float x, float w, string heading, string[] palette, ref int value)
        {
            GUI.Label(new Rect(x, y, w, 20), heading, small);
            y += 22;
            bool changed = false;
            float size = Mathf.Min(40f, (w - (palette.Length - 1) * 6) / palette.Length);
            for (int i = 0; i < palette.Length; i++)
            {
                Rect cell = new Rect(x + i * (size + 6), y, size, size);
                if (value == i) Box(new Rect(cell.x - 3, cell.y - 3, cell.width + 6, cell.height + 6), new Color(1f, 0.9f, 0.5f));
                Box(cell, CharacterAppearance.Pick(palette, i));
                if (GUI.Button(cell, GUIContent.none, GUIStyle.none) && value != i)
                {
                    value = i;
                    changed = true;
                    Sfx.Ui("ui", 0.4f);
                }
            }
            y += size + 14;
            return changed;
        }
    }
}
