using System.Collections.Generic;
using RPG.Combat;
using RPG.Content;
using RPG.Control;
using RPG.Core;
using RPG.Gathering;
using RPG.Items;
using RPG.Stats;
using RPG.World;
using UnityEngine;

namespace RPG.UI
{
    /// <summary>
    /// Immediate-mode HUD (OnGUI) so it needs no prefabs or canvas setup:
    /// HP bar, chat log, XP drops, level-up banner, hitsplats, enemy nameplates,
    /// hover tooltips, inventory + crafting panel (I), skills panel (K), minimap and world map (M),
    /// and the bank / shop / furnace / anvil / fire panels when the player is at one.
    /// </summary>
    public class RpgHud : MonoBehaviour
    {
        const float NameplateRange = 25f;
        const int Columns = 4;
        float Cell = 54f;

        struct FloatingText
        {
            public string Text;
            public float Time;
            public Color Color;
        }

        struct Hitsplat
        {
            public Transform Target;
            public int Damage;
            public float Time;
            public Vector2 Jitter;
        }

        GameObject player;
        SkillSet skills;
        Inventory inventory;
        Health health;
        PlayerStats stats;

        Bank bank;
        Stamina stamina;
        StationUser stationUser;
        Crafter crafter;

        bool showInventory = true;
        bool showEquipment = true;
        bool showSkills = false;
        PlayerPortrait portrait;
        CharacterAppearance appearance;

        // last time each character fought (attacked or was attacked), for the overhead health bars
        readonly Dictionary<Transform, float> lastFight = new Dictionary<Transform, float>();
        const float OverheadSeconds = 6f;
        bool showMap = false;
        bool craftingTab = false;
        Vector2 recipeScroll;
        Vector2 stationScroll;

        // the map is drawn turned so the camera's forward direction is up
        Texture2D mapTexture;
        float mapYaw;
        const float MinimapMeters = 130f;
        string currentZone;

        readonly List<FloatingText> xpDrops = new List<FloatingText>();
        readonly List<Hitsplat> hitsplats = new List<Hitsplat>();
        FloatingText banner;
        string hoverText;
        string slotTooltip;
        float scale = 1f;

        readonly List<Rect> panelRects = new List<Rect>();

        GUIStyle label, small, bold, centered, title, shadowed;
        bool stylesReady;

        private void Start()
        {
            player = GameObject.FindWithTag("Player");
            skills = player.GetComponent<SkillSet>();
            inventory = player.GetComponent<Inventory>();
            health = player.GetComponent<Health>();
            stats = player.GetComponent<PlayerStats>();
            bank = player.GetComponent<Bank>();
            stamina = player.GetComponent<Stamina>();
            stationUser = player.GetComponent<StationUser>();
            crafter = player.GetComponent<Crafter>();
            appearance = player.GetComponent<CharacterAppearance>();
            portrait = GetComponent<PlayerPortrait>();

            if (WorldBuilder.Root != null)
            {
                // north-up maps (the camera turns freely now)
                mapYaw = 0f;
                mapTexture = WorldTerrain.MapTexture(320, mapYaw);
            }

            skills.onXpGained += OnXpGained;
            skills.onLevelUp += OnLevelUp;
            Fighter.onAttackResolved += OnAttackResolved;
        }

        private void OnDestroy()
        {
            Fighter.onAttackResolved -= OnAttackResolved;
            if (skills != null)
            {
                skills.onXpGained -= OnXpGained;
                skills.onLevelUp -= OnLevelUp;
            }
            UiState.PointerOverUi = false;
        }

        void OnXpGained(Skill skill, double amount)
        {
            FloatingText drop = new FloatingText();
            drop.Text = "+" + amount.ToString("0.#") + " " + skill;
            drop.Time = Time.time;
            drop.Color = new Color(1f, 0.92f, 0.6f);
            xpDrops.Add(drop);
            if (xpDrops.Count > 8) xpDrops.RemoveAt(0);
        }

        void OnLevelUp(Skill skill, int level)
        {
            banner.Text = skill + " level " + level + "!";
            banner.Time = Time.time;
            banner.Color = GameLog.LevelUp;
        }

        void OnAttackResolved(GameObject attacker, GameObject defender, int damage)
        {
            if (attacker != null) lastFight[attacker.transform] = Time.time;
            if (defender != null) lastFight[defender.transform] = Time.time;
            Hitsplat splat = new Hitsplat();
            splat.Target = defender.transform;
            splat.Damage = damage;
            splat.Time = Time.time;
            splat.Jitter = new Vector2(Random.Range(-14f, 14f), Random.Range(-8f, 8f));
            hitsplats.Add(splat);
        }

        bool Playing { get { return RPG.Game.GameSession.Instance == null || RPG.Game.GameSession.Instance.Playing; } }

        private void Update()
        {
            if (!Playing) return;
            if (!UiState.Blocked && !UiState.Typing)
            {
                if (Input.GetKeyDown(KeyCode.I)) showInventory = !showInventory;
                if (Input.GetKeyDown(KeyCode.E)) showEquipment = !showEquipment;
                if (Input.GetKeyDown(KeyCode.K)) showSkills = !showSkills;
                if (Input.GetKeyDown(KeyCode.M) && mapTexture != null) showMap = !showMap;
                if (Input.GetKeyDown(KeyCode.Alpha1)) stats.SetStance(MeleeStance.Slash);
                if (Input.GetKeyDown(KeyCode.Alpha2)) stats.SetStance(MeleeStance.Crush);
                if (Input.GetKeyDown(KeyCode.Alpha3)) stats.SetStance(MeleeStance.Bash);
            }
            UpdateHover();
            UpdateZone();
        }

        /// <summary>Esc: closes the top-most panel. False if nothing was open.</summary>
        public bool CloseTopPanel()
        {
            if (stationUser != null && stationUser.Open != null)
            {
                stationUser.Close();
                return true;
            }
            if (showMap)
            {
                showMap = false;
                return true;
            }
            return false;
        }

        bool Indoors { get { return InteriorDirector.Instance != null && InteriorDirector.Instance.Current != null; } }

        void UpdateZone()
        {
            if (WorldBuilder.Root == null || player == null || Indoors) return;
            Vector2 p = new Vector2(player.transform.position.x, player.transform.position.z);
            float townDistance;
            TownInfo town = WorldLayout.NearestTown(p, out townDistance);
            string zone = townDistance < TownInfo.FlatRadius ? town.Name : WorldLayout.ZoneAt(p).Name;
            if (zone == currentZone) return;
            bool first = currentZone == null;
            currentZone = zone;
            if (first) return;
            string text = townDistance < TownInfo.FlatRadius ? "Welcome to " + town.Name : "Entering " + zone + " (levels " + WorldLayout.ZoneAt(p).Levels + ")";
            banner.Text = text;
            banner.Time = Time.time;
            banner.Color = new Color(1f, 0.92f, 0.65f);
            GameLog.Add(text + ".");
        }

        void UpdateHover()
        {
            hoverText = null;
            if (UiState.PointerOverUi || Camera.main == null) return;

            RaycastHit[] hits = Physics.RaycastAll(Camera.main.ScreenPointToRay(Input.mousePosition));
            System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
            foreach (RaycastHit hit in hits)
            {
                EnemyProfile enemy = hit.transform.GetComponentInParent<EnemyProfile>();
                if (enemy != null && enemy.Info != null && !enemy.Health.IsDead())
                {
                    hoverText = "Attack " + enemy.Info.Name + " (level " + enemy.Info.Level + ")";
                    return;
                }
                Pickup pickup = hit.transform.GetComponent<Pickup>();
                if (pickup != null)
                {
                    hoverText = "Take " + pickup.Label;
                    return;
                }
                Station station = hit.transform.GetComponentInParent<Station>();
                if (station != null)
                {
                    hoverText = station.Verb + " " + (station.Kind == StationKind.Door && station.Label != null ? station.Label.ToLower() : station.Label);
                    return;
                }
                ResourceNode node = hit.transform.GetComponentInParent<ResourceNode>();
                if (node != null)
                {
                    hoverText = node.IsDepleted ? node.Info.Name + " (depleted)" : NodeHover(node);
                    return;
                }
            }
        }

        string NodeHover(ResourceNode node)
        {
            NodeInfo info = node.Info;
            string text = node.ActionVerb + " " + info.Name + " (" + info.Skill + " " + info.Level + ")";
            int level = skills.GetLevel(info.Skill);
            ToolKind kind = info.Kind == NodeKind.Tree ? ToolKind.Axe : ToolKind.Pickaxe;
            ItemInfo tool = inventory.EquippedTool(kind);
            if (level < info.Level) return text + "\nYou need " + info.Skill + " " + info.Level;
            if (tool == null) return text + "\n" + (inventory.HasAnyTool(kind) ? "Wield your " + (kind == ToolKind.Axe ? "axe" : "pickaxe") + " first (click it in your bag)" : "You need to wield " + (kind == ToolKind.Axe ? "an axe" : "a pickaxe"));
            int swings = GameContent.SwingsNeeded(info, level, tool.ToolPower);
            return text + "\n~" + swings + " swing" + (swings == 1 ? "" : "s") + " per " + (info.Kind == NodeKind.Tree ? "log" : "ore") + " with your " + tool.Name.ToLower();
        }

        // ---------------------------------------------------------------- GUI

        void InitStyles()
        {
            if (stylesReady) return;
            label = new GUIStyle(GUI.skin.label) { fontSize = 13, wordWrap = false };
            label.normal.textColor = Color.white;
            small = new GUIStyle(label) { fontSize = 11 };
            bold = new GUIStyle(label) { fontStyle = FontStyle.Bold };
            centered = new GUIStyle(label) { alignment = TextAnchor.MiddleCenter };
            title = new GUIStyle(bold) { fontSize = 22, alignment = TextAnchor.MiddleCenter };
            shadowed = new GUIStyle(bold) { alignment = TextAnchor.MiddleCenter, fontSize = 12 };
            stylesReady = true;
        }

        private void OnGUI()
        {
            if (player == null) return;
            if (!Playing)
            {
                UiState.PointerOverUi = false;
                return;
            }
            InitStyles();

            scale = Mathf.Max(1f, Screen.height / 900f);
            GUI.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, new Vector3(scale, scale, 1f));
            float width = Screen.width / scale;
            float height = Screen.height / scale;
            // shrink inventory cells on short screens so the bag fits under the minimap
            float room = height - 40f - (mapTexture != null ? 214f : 20f) - 70f;
            Cell = Mathf.Clamp(Mathf.Floor(room / 7f), 40f, 54f);

            if (Event.current.type == EventType.Layout) panelRects.Clear();

            DrawWorldLabels();
            DrawNodeProgress();
            DrawNameplates();
            DrawOverheadBars();
            DrawHitsplats();
            DrawStatusBar(width);
            DrawBossBar(width);
            DrawMinimap(width);
            DrawXpDrops(width);
            DrawBanner(width);
            DrawLog(height);
            Station open = stationUser != null ? stationUser.Open : null;
            if (open != null) showInventory = true;
            if (showInventory) DrawInventory(width, height);
            if (open != null) DrawStationPanel(open, width, height);
            else if (showEquipment) DrawEquipment(width, height);
            if (showSkills) DrawSkills(height);
            if (showMap && !Indoors) DrawWorldMap(width, height);
            DrawHelp(width, height);
            DrawHoverTooltip();
            DrawFade(width, height);

            if (Event.current.type == EventType.Repaint || Event.current.type == EventType.Layout)
            {
                Vector2 mouse = Event.current.mousePosition;
                bool over = false;
                foreach (Rect rect in panelRects) if (rect.Contains(mouse)) over = true;
                UiState.PointerOverUi = over;
            }
        }

        void Panel(Rect rect)
        {
            if (Event.current.type == EventType.Layout) panelRects.Add(rect);
            Box(rect, new Color(0.08f, 0.07f, 0.06f, 0.85f));
            Box(new Rect(rect.x, rect.y, rect.width, 2), new Color(0.75f, 0.6f, 0.3f, 0.9f));
        }

        static void Box(Rect rect, Color color)
        {
            Color old = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = old;
        }

        void Shadow(Rect rect, string text, GUIStyle style, Color color)
        {
            Color old = style.normal.textColor;
            style.normal.textColor = new Color(0, 0, 0, color.a * 0.9f);
            GUI.Label(new Rect(rect.x + 1, rect.y + 1, rect.width, rect.height), text, style);
            style.normal.textColor = color;
            GUI.Label(rect, text, style);
            style.normal.textColor = old;
        }

        bool WorldToGui(Vector3 world, out Vector2 gui)
        {
            gui = Vector2.zero;
            Vector3 screen = Camera.main.WorldToScreenPoint(world);
            if (screen.z < 0) return false;
            gui = new Vector2(screen.x / scale, (Screen.height - screen.y) / scale);
            return true;
        }

        void DrawNameplates()
        {
            if (Camera.main == null) return;
            int playerLevel = stats != null ? stats.CombatLevel : 3;

            foreach (EnemyProfile enemy in EnemyProfile.All)
            {
                if (enemy.Info == null || enemy.Health.IsDead()) continue;
                if (Vector3.Distance(enemy.transform.position, player.transform.position) > NameplateRange) continue;

                Vector2 pos;
                if (!WorldToGui(enemy.transform.position + Vector3.up * Mathf.Max(1.1f, enemy.NameplateHeight), out pos)) continue;

                // colour by level difference, like classic MMOs
                int diff = enemy.Info.Level - playerLevel;
                Color color = diff <= -5 ? new Color(0.5f, 1f, 0.4f) : diff >= 5 ? new Color(1f, 0.4f, 0.35f) : new Color(1f, 0.9f, 0.4f);
                Shadow(new Rect(pos.x - 90, pos.y - 30, 180, 18), enemy.Info.Name + " (lvl " + enemy.Info.Level + ")", shadowed, color);

            }
        }

        /// <summary>
        /// A thin bar over the player and over monsters while they're fighting (and a few seconds after),
        /// green at full health turning red as it drops.
        /// </summary>
        void DrawOverheadBars()
        {
            if (Camera.main == null) return;
            OverheadBar(player.transform, health, 2.15f);
            foreach (EnemyProfile enemy in EnemyProfile.All)
            {
                if (enemy.Info == null || enemy.Health.IsDead()) continue;
                OverheadBar(enemy.transform, enemy.Health, Mathf.Max(1.1f, enemy.NameplateHeight) - 0.15f);
            }
        }

        void OverheadBar(Transform who, Health whose, float lift)
        {
            float last;
            if (who == null || whose == null || whose.IsDead() || !lastFight.TryGetValue(who, out last)) return;
            float age = Time.time - last;
            if (age > OverheadSeconds) return;
            Vector2 pos;
            if (!WorldToGui(who.position + Vector3.up * lift, out pos)) return;
            float alpha = Mathf.Clamp01((OverheadSeconds - age) / 1f);
            float fraction = Mathf.Clamp01(whose.GetFraction());
            Rect bar = new Rect(pos.x - 26, pos.y, 52, 5);
            Box(new Rect(bar.x - 1, bar.y - 1, bar.width + 2, bar.height + 2), new Color(0, 0, 0, 0.75f * alpha));
            Box(bar, new Color(0.25f, 0.05f, 0.05f, 0.85f * alpha));
            Color fill = fraction > 0.5f ? Color.Lerp(new Color(0.95f, 0.85f, 0.15f), new Color(0.2f, 0.85f, 0.25f), (fraction - 0.5f) * 2f) : Color.Lerp(new Color(0.9f, 0.15f, 0.1f), new Color(0.95f, 0.85f, 0.15f), fraction * 2f);
            fill.a = alpha;
            Box(new Rect(bar.x, bar.y, bar.width * fraction, bar.height), fill);
        }

        void DrawFade(float width, float height)
        {
            InteriorDirector director = InteriorDirector.Instance;
            if (director == null || director.FadeAlpha <= 0f) return;
            Box(new Rect(0, 0, width, height), new Color(0, 0, 0, director.FadeAlpha));
        }

        void DrawHitsplats()
        {
            if (Camera.main == null) return;
            for (int i = hitsplats.Count - 1; i >= 0; i--)
            {
                Hitsplat splat = hitsplats[i];
                float age = Time.time - splat.Time;
                if (age > 1.0f || splat.Target == null)
                {
                    hitsplats.RemoveAt(i);
                    continue;
                }

                Vector2 pos;
                if (!WorldToGui(splat.Target.position + Vector3.up * 1.4f, out pos)) continue;
                pos += splat.Jitter + Vector2.up * (-age * 20f);

                Rect rect = new Rect(pos.x - 14, pos.y - 11, 28, 22);
                Box(rect, splat.Damage > 0 ? new Color(0.75f, 0.08f, 0.08f, 0.95f) : new Color(0.15f, 0.35f, 0.85f, 0.95f));
                Shadow(rect, splat.Damage.ToString(), shadowed, Color.white);
            }
        }

        void DrawStatusBar(float width)
        {
            Rect rect = new Rect(width / 2 - 160, 8, 320, 82);
            Panel(rect);
            string name = appearance != null ? appearance.Data.Name : "You";
            Shadow(new Rect(rect.x, rect.y + 4, rect.width, 18), name + "   -   combat " + skills.CombatLevel() + ", total " + skills.TotalLevel(), small, new Color(1f, 0.9f, 0.65f));
            float fraction = health.GetFraction();
            Rect bar = new Rect(rect.x + 10, rect.y + 22, rect.width - 20, 20);
            Box(bar, new Color(0.45f, 0.05f, 0.05f, 1f));
            Color fill = Color.Lerp(new Color(0.85f, 0.15f, 0.1f), new Color(0.15f, 0.75f, 0.2f), Mathf.Clamp01(fraction * 1.6f - 0.3f));
            Box(new Rect(bar.x, bar.y, bar.width * fraction, bar.height), fill);
            Shadow(bar, "HP " + Mathf.CeilToInt(health.GetHealth()) + " / " + Mathf.CeilToInt(health.GetMaxHealth()), shadowed, Color.white);

            // run energy: drains while running, refills while walking or standing
            if (stamina != null)
            {
                StaminaModel model = stamina.Model;
                Rect energy = new Rect(rect.x + 10, rect.y + 46, rect.width - 86, 14);
                Box(energy, new Color(0.2f, 0.18f, 0.1f, 1f));
                Box(new Rect(energy.x, energy.y, energy.width * model.Energy / StaminaModel.Max, energy.height), model.Running ? new Color(0.95f, 0.75f, 0.2f, 1f) : new Color(0.6f, 0.55f, 0.4f, 1f));
                Shadow(energy, "Stamina " + Mathf.FloorToInt(model.Energy), small, Color.white);
                if (GUI.Button(new Rect(rect.xMax - 70, rect.y + 44, 60, 18), model.Running ? "Run" : "Walk")) stamina.Toggle();
                if (new Rect(rect.x + 10, rect.y + 44, rect.width - 20, 18).Contains(Event.current.mousePosition))
                {
                    slotTooltip = (model.Running ? "Running" : "Walking") + " (R to toggle)\nRunning drains stamina; walking or resting refills it.";
                }
            }
            string clock = DayNightCycle.Instance != null ? DayNightCycle.Instance.ClockText : "";
            string style = stats.Style == CombatStyle.Melee ? PlayerStats.StanceName(stats.Stance) + " (" + CombatMath.StanceSkill(stats.Stance) + ")" : stats.Style.ToString();
            Shadow(new Rect(rect.x, rect.y + 63, rect.width, 18), clock + "     Style: " + style, small, new Color(1f, 1f, 1f, 0.85f));
        }

        void DrawBossBar(float width)
        {
            BossController boss = BossController.Active;
            if (boss == null || !boss.IsEngaged || boss.Profile == null || boss.Profile.Info == null) return;
            Health bossHealth = boss.Profile.Health;
            Rect rect = new Rect(width / 2 - 200, 96, 400, 44);
            Panel(rect);
            Shadow(new Rect(rect.x, rect.y + 4, rect.width, 18), boss.Profile.Info.Name + "  (level " + boss.Profile.Info.Level + ")", shadowed, new Color(1f, 0.55f, 0.4f));
            Rect bar = new Rect(rect.x + 12, rect.y + 24, rect.width - 24, 12);
            Box(bar, new Color(0.35f, 0.05f, 0.05f, 1f));
            Box(new Rect(bar.x, bar.y, bar.width * bossHealth.GetFraction(), bar.height), new Color(0.85f, 0.2f, 0.15f, 1f));
        }

        void DrawXpDrops(float width)
        {
            float y = 80;
            for (int i = xpDrops.Count - 1; i >= 0; i--)
            {
                FloatingText drop = xpDrops[i];
                float age = Time.time - drop.Time;
                if (age > 2.5f)
                {
                    xpDrops.RemoveAt(i);
                    continue;
                }
                Color color = drop.Color;
                color.a = Mathf.Clamp01(2.5f - age);
                float x = mapTexture != null ? width - 460 : width - 260;
                Shadow(new Rect(x, y - age * 25f, 240, 20), drop.Text + " xp", bold, color);
                y += 20;
            }
        }

        void DrawBanner(float width)
        {
            if (string.IsNullOrEmpty(banner.Text)) return;
            float age = Time.time - banner.Time;
            if (age > 3.5f) return;
            Color color = banner.Color;
            color.a = Mathf.Clamp01(3.5f - age);
            Shadow(new Rect(0, 146, width, 36), banner.Text, title, color);
        }

        void DrawLog(float height)
        {
            float lineHeight = 18;
            Rect rect = new Rect(12, height - 40 - lineHeight * GameLog.MaxMessages - 12, 520, lineHeight * GameLog.MaxMessages + 12);
            Box(rect, new Color(0.08f, 0.07f, 0.06f, 0.6f));
            IList<GameLog.Message> messages = GameLog.Messages;
            float y = rect.y + 6;
            for (int i = 0; i < messages.Count; i++)
            {
                Shadow(new Rect(rect.x + 8, y, rect.width - 16, lineHeight), messages[i].Text, label, messages[i].Color);
                y += lineHeight;
            }
        }

        void DrawHelp(float width, float height)
        {
            Shadow(new Rect(12, height - 28, width - 24, 20),
                "Click or WASD: move     Arrows: camera     R: walk/run     1/2/3: combat style     I: bag   E: equipment   K: skills   M: map     Esc: menu",
                small, new Color(1, 1, 1, 0.8f));
        }

        void DrawHoverTooltip()
        {
            string text = !string.IsNullOrEmpty(slotTooltip) ? slotTooltip : hoverText;
            slotTooltip = null;
            if (string.IsNullOrEmpty(text)) return;

            Vector2 mouse = Event.current.mousePosition;
            string[] lines = text.Split('\n');
            float w = 0;
            foreach (string line in lines) w = Mathf.Max(w, label.CalcSize(new GUIContent(line)).x);
            Rect rect = new Rect(mouse.x + 16, mouse.y + 12, w + 16, lines.Length * 18 + 8);
            Box(rect, new Color(0, 0, 0, 0.8f));
            GUI.Label(new Rect(rect.x + 8, rect.y + 4, rect.width, rect.height), text, label);
        }

        // ---------------------------------------------------------------- Inventory + crafting

        void DrawInventory(float width, float height)
        {
            float panelWidth = Columns * Cell + 24;
            float panelHeight = 7 * Cell + 52;
            Rect rect = new Rect(width - panelWidth - 12, height - panelHeight - 40, panelWidth, panelHeight);
            Panel(rect);

            float x = rect.x + 12;
            float y = rect.y + 10;

            float tab = (panelWidth - 32) / 2f;
            if (GUI.Toggle(new Rect(x, y, tab, 24), !craftingTab, "Bag", "Button")) craftingTab = false;
            if (GUI.Toggle(new Rect(x + tab + 8, y, tab, 24), craftingTab, "Make", "Button")) craftingTab = true;
            y += 32;

            if (craftingTab) DrawRecipes(new Rect(x, y, panelWidth - 24, rect.yMax - y - 10));
            else DrawSlots(x, y);
        }

        // ---------------------------------------------------------------- Equipment

        static readonly string[] SlotNames = { "Head", "Body", "Legs", "Weapon", "Shield", "Ammo" };

        void DrawEquipment(float width, float height)
        {
            float inventoryWidth = Columns * Cell + 24;
            float panelWidth = 250f;
            float panelHeight = 7 * Cell + 52;
            Rect rect = new Rect(width - inventoryWidth - 12 - panelWidth - 8, height - panelHeight - 40, panelWidth, panelHeight);
            Panel(rect);
            Shadow(new Rect(rect.x + 10, rect.y + 6, rect.width - 20, 18), "Equipment  (E)", bold, new Color(1f, 0.9f, 0.6f));

            // a live picture of the character, with slots around it
            Rect picture = new Rect(rect.x + 45, rect.y + 28, 160, 210);
            Box(picture, new Color(0.14f, 0.12f, 0.1f, 0.9f));
            if (portrait != null)
            {
                portrait.Want();
                if (portrait.Texture != null) GUI.DrawTexture(picture, portrait.Texture, ScaleMode.ScaleToFit, true);
                if (picture.Contains(Event.current.mousePosition) && Event.current.type == EventType.MouseDrag)
                {
                    portrait.Turn += Event.current.delta.x;
                    Event.current.Use();
                }
            }
            InventoryModel model = inventory.Model;
            float s = 40f;
            EquipCell(new Rect(rect.center.x - s / 2, rect.y + 30, s, s), EquipSlot.Head);
            EquipCell(new Rect(rect.xMax - s - 6, rect.y + 30, s, s), EquipSlot.Ammo);
            EquipCell(new Rect(rect.x + 6, rect.y + 100, s, s), EquipSlot.Weapon);
            EquipCell(new Rect(rect.center.x - s / 2, rect.y + 120, s, s), EquipSlot.Body);
            EquipCell(new Rect(rect.xMax - s - 6, rect.y + 100, s, s), EquipSlot.Shield);
            EquipCell(new Rect(rect.center.x - s / 2, rect.y + 196, s, s), EquipSlot.Legs);

            float y = rect.y + 246;
            GUI.Label(new Rect(rect.x + 10, y, rect.width - 20, 18), "Atk +" + model.TotalAttackBonus() + "   Str +" + model.TotalStrengthBonus() + "   Def +" + model.TotalDefenseBonus(), small);
            y += 16;
            int reduction = Mathf.RoundToInt((float)CombatMath.DamageReduction(skills.GetLevel(Skill.Defense), model.TotalDefenseBonus()) * 100f);
            GUI.Label(new Rect(rect.x + 10, y, rect.width - 20, 18), "Max hit " + stats.MaxHit + "   Blocks " + reduction + "% of damage", small);
            y += 22;

            // combat style
            if (stats.Style == CombatStyle.Melee)
            {
                GUI.Label(new Rect(rect.x + 10, y, rect.width - 20, 18), "Combat style (1 / 2 / 3)", small);
                y += 18;
                float bw = (rect.width - 28) / 3f;
                MeleeStance[] stances = { MeleeStance.Slash, MeleeStance.Crush, MeleeStance.Bash };
                string[] labels = { "Slash", "Crush", "Bash" };
                for (int i = 0; i < 3; i++)
                {
                    Rect button = new Rect(rect.x + 10 + i * (bw + 4), y, bw, 26);
                    bool enabled = stances[i] != MeleeStance.Bash || stats.HasShield;
                    bool old = GUI.enabled;
                    GUI.enabled = enabled;
                    bool on = GUI.Toggle(button, stats.Stance == stances[i], labels[i], "Button");
                    GUI.enabled = old;
                    if (on && stats.Stance != stances[i]) stats.SetStance(stances[i]);
                    if (button.Contains(Event.current.mousePosition)) slotTooltip = StanceTooltip(stances[i]);
                }
                y += 30;
                GUI.Label(new Rect(rect.x + 10, y, rect.width - 20, 34), StanceSummary(stats.Stance), new GUIStyle(small) { wordWrap = true });
            }
            else
            {
                ItemInfo weapon = GameContent.Item(model.WeaponId);
                ItemInfo ammo = weapon != null ? GameContent.Item(weapon.AmmoId) : null;
                string count = ammo == null ? "" : ammo.Slot == EquipSlot.Ammo ? (model.AmmoId == ammo.Id ? model.AmmoCount : 0) + " equipped" : model.Count(ammo.Id) + " in bag";
                GUI.Label(new Rect(rect.x + 10, y, rect.width - 20, 34), stats.Style + " (trains " + stats.Style + ")\n" + (ammo != null ? ammo.Name + ": " + count : ""), new GUIStyle(small) { wordWrap = true });
            }
        }

        static string StanceTooltip(MeleeStance stance)
        {
            switch (stance)
            {
                case MeleeStance.Crush: return "Crush (trains Strength)\nSlower, heavy blows that hit hardest and scale with Strength.";
                case MeleeStance.Bash: return "Shield bash (trains Defense)\nNeeds a shield. Hits as fast and as hard as Slash.";
                default: return "Slash (trains Attack)\nQuicker swings that hit a little lower.";
            }
        }

        static string StanceSummary(MeleeStance stance)
        {
            switch (stance)
            {
                case MeleeStance.Crush: return "Crush: slow, heavy hits. Trains Strength.";
                case MeleeStance.Bash: return "Shield bash: quick hits. Trains Defense.";
                default: return "Slash: quick, lighter hits. Trains Attack.";
            }
        }

        void EquipCell(Rect cell, EquipSlot slot)
        {
            InventoryModel model = inventory.Model;
            string id = model.Worn(slot);
            ItemInfo info = GameContent.Item(id);
            Box(cell, new Color(0.2f, 0.18f, 0.15f, 0.95f));
            Box(new Rect(cell.x, cell.y, cell.width, 1), new Color(0.75f, 0.6f, 0.3f, 0.6f));
            if (info == null)
            {
                Shadow(cell, SlotNames[(int)slot - 1], new GUIStyle(small) { alignment = TextAnchor.MiddleCenter, fontSize = 9 }, new Color(1, 1, 1, 0.35f));
            }
            else
            {
                Texture2D icon = ItemIcons.Get(id);
                if (icon != null) GUI.DrawTexture(new Rect(cell.x + 3, cell.y + 3, cell.width - 6, cell.height - 6), icon, ScaleMode.ScaleToFit);
                if (slot == EquipSlot.Ammo) Shadow(new Rect(cell.x + 2, cell.y, cell.width, 16), QuantityText(model.AmmoCount), small, Color.yellow);
            }
            if (!cell.Contains(Event.current.mousePosition)) return;
            if (info == null)
            {
                slotTooltip = SlotNames[(int)slot - 1] + " slot (empty)" + (slot == EquipSlot.Ammo ? "\nEquip up to " + InventoryModel.AmmoCap + " arrows" : "");
                return;
            }
            slotTooltip = ItemTooltip(info, true) + "\nClick: take off";
            if (Event.current.type == EventType.MouseDown)
            {
                inventory.Unequip(slot);
                Event.current.Use();
            }
        }

        void DrawSlots(float x, float y)
        {
            InventoryModel model = inventory.Model;
            Event e = Event.current;

            for (int i = 0; i < InventoryModel.SlotCount; i++)
            {
                Rect cell = new Rect(x + (i % Columns) * Cell, y + (i / Columns) * Cell, Cell - 4, Cell - 4);
                Box(cell, new Color(0.2f, 0.18f, 0.15f, 0.9f));

                ItemStack stack = model.GetSlot(i);
                if (stack.IsEmpty) continue;
                ItemInfo info = GameContent.Item(stack.ItemId);
                if (info == null) continue;

                Texture2D icon = ItemIcons.Get(stack.ItemId);
                if (icon != null) GUI.DrawTexture(new Rect(cell.x + 3, cell.y + 3, cell.width - 6, cell.height - 6), icon, ScaleMode.ScaleToFit);
                if (info.Rare) Box(new Rect(cell.x, cell.y, cell.width, 2), GameLog.Rare);
                if (stack.Quantity > 1) Shadow(new Rect(cell.x + 2, cell.y, cell.width, 16), QuantityText(stack.Quantity), small, Color.yellow);

                if (cell.Contains(e.mousePosition))
                {
                    Station open = stationUser != null ? stationUser.Open : null;
                    if (open != null && open.Kind == StationKind.Bank)
                    {
                        slotTooltip = info.Name + "\nLeft-click: deposit " + (info.Stackable ? "all" : "1") + "    Right-click: deposit all";
                        if (e.type == EventType.MouseDown)
                        {
                            bank.Deposit(info.Id, e.button == 0 && !info.Stackable ? 1 : int.MaxValue);
                            e.Use();
                        }
                    }
                    else if (open != null && open.Kind == StationKind.Shop)
                    {
                        int price = GameContent.SellPrice(info.Id);
                        slotTooltip = info.Name + (price > 0 ? "\nShop pays " + price + " coins each" : "\nShops won't buy this") + "\nLeft-click: sell 1    Right-click: sell all";
                        if (e.type == EventType.MouseDown)
                        {
                            string message;
                            if (ShopLogic.TrySell(inventory.Model, info.Id, e.button == 0 ? 1 : int.MaxValue, out message)) Sfx.Ui("coins", 0.6f);
                            if (message != null) GameLog.Add(message);
                            inventory.NotifyChanged();
                            e.Use();
                        }
                    }
                    else
                    {
                        slotTooltip = ItemTooltip(info);
                        if (e.type == EventType.MouseDown)
                        {
                            if (e.button == 0) inventory.UseSlot(i);
                            else if (e.button == 1) inventory.DropSlot(i);
                            e.Use();
                        }
                    }
                }
            }
        }

        string ItemTooltip(ItemInfo info)
        {
            return ItemTooltip(info, false);
        }

        string ItemTooltip(ItemInfo info, bool worn)
        {
            string text = info.Name;
            if (!string.IsNullOrEmpty(info.Tier))
            {
                MaterialTier tier = GameContent.Tier(info.Tier);
                if (tier != null) text += "\n" + tier.Name + " tier - passive: " + tier.Passive;
            }
            if (info.IsPet)
            {
                text += "\n" + info.Description;
            }
            else if (info.IsEquippable)
            {
                string slotName = info.Slot == EquipSlot.Weapon ? (info.TwoHanded ? "Two-handed weapon" : info.Tool != ToolKind.None ? "Tool (wield it to " + (info.Tool == ToolKind.Axe ? "chop" : "mine") + ")" : "One-handed weapon") : SlotNames[(int)info.Slot - 1];
                text += "\n" + slotName;
                if (info.Style != CombatStyle.Melee)
                {
                    ItemInfo ammo = GameContent.Item(info.AmmoId);
                    text += "\n" + info.Style + ", range " + info.Range + "m, uses " + (ammo != null ? ammo.Name.ToLower() : "nothing");
                }
                if (info.Tool != ToolKind.None) text += "\n" + Mathf.RoundToInt(info.ToolPower * 100f) + "% swing power";
                if (info.Slot != EquipSlot.Ammo) text += "\nAtk +" + info.AttackBonus + "  Str +" + info.StrengthBonus + "  Def +" + info.DefenseBonus;
                text += "\nRequires " + info.RequiredSkill + " " + info.RequiredLevel;
                if (!worn) text += info.Slot == EquipSlot.Ammo ? "\nLeft-click: equip (up to " + InventoryModel.AmmoCap + ")    Right-click: drop" : "\nLeft-click: equip    Right-click: drop";
            }
            else if (info.IsEdible)
            {
                text += "\nHeals " + info.HealAmount + "\nLeft-click: eat    Right-click: drop";
            }
            else if (GameContent.Burnables.ContainsKey(info.Id))
            {
                BurnInfo burn = GameContent.Burnables[info.Id];
                text += "\nPyro " + burn.Level + " to burn (" + burn.Xp + " xp)\nLeft-click: light a fire (needs flint and steel)    Right-click: drop";
            }
            else
            {
                if (!string.IsNullOrEmpty(info.Description)) text += "\n" + info.Description;
                text += "\nRight-click: drop";
            }
            if (info.Value > 0) text += "\nValue " + info.Value + " coins (shops pay " + GameContent.SellPrice(info.Id) + ")";
            return text;
        }

        static string QuantityText(int quantity)
        {
            if (quantity >= 10000000) return (quantity / 1000000) + "M";
            if (quantity >= 100000) return (quantity / 1000) + "K";
            return quantity.ToString();
        }

        static string Abbreviate(string name)
        {
            return name.Length <= 9 ? name : name.Substring(0, 8) + ".";
        }

        static List<Recipe> RecipesAt(StationKind station)
        {
            return GameContent.Recipes.FindAll(r => r.Station == station);
        }

        void DrawRecipes(Rect area)
        {
            List<Recipe> recipes = RecipesAt(StationKind.None);
            float rowHeight = 46;
            GUI.Label(new Rect(area.x, area.y, area.width, 18), "Smelt at furnaces, smith at anvils, cook on fires.", small);
            area = new Rect(area.x, area.y + 20, area.width, area.height - 20);
            Rect view = new Rect(0, 0, area.width - 18, recipes.Count * rowHeight);
            recipeScroll = GUI.BeginScrollView(area, recipeScroll, view);

            for (int i = 0; i < recipes.Count; i++)
            {
                Recipe recipe = recipes[i];
                float y = i * rowHeight;
                bool hasLevel = skills.GetLevel(recipe.Skill) >= recipe.Level;
                bool canMake = inventory.CanMake(recipe);

                Color nameColor = canMake ? Color.white : hasLevel ? new Color(0.8f, 0.8f, 0.8f) : new Color(1f, 0.5f, 0.45f);
                Shadow(new Rect(0, y, view.width - 60, 18), recipe.Name, bold, nameColor);
                GUI.Label(new Rect(0, y + 18, view.width - 60, 18), recipe.Skill + " " + recipe.Level + "  |  " + Inputs(recipe), small);

                bool oldEnabled = GUI.enabled;
                GUI.enabled = canMake;
                if (GUI.Button(new Rect(view.width - 56, y + 6, 54, 26), "Make")) inventory.Make(recipe);
                GUI.enabled = oldEnabled;
            }
            GUI.EndScrollView();
        }

        string Inputs(Recipe recipe)
        {
            List<string> parts = new List<string>();
            if (!string.IsNullOrEmpty(recipe.ToolId))
            {
                ItemInfo tool = GameContent.Item(recipe.ToolId);
                if (inventory.Model.Count(recipe.ToolId) == 0) parts.Add("needs a " + (tool != null ? tool.Name.ToLower() : recipe.ToolId));
            }
            foreach (ItemStack input in recipe.Inputs)
            {
                ItemInfo info = GameContent.Item(input.ItemId);
                string name = info != null ? info.Name : input.ItemId;
                parts.Add(input.Quantity + "x " + name + " (" + inventory.Model.Count(input.ItemId) + ")");
            }
            return string.Join(", ", parts.ToArray());
        }

        // ---------------------------------------------------------------- World labels, progress bars

        void DrawWorldLabels()
        {
            if (Camera.main == null || WorldBuilder.Root == null) return;
            Vector3 me = player.transform.position;
            if (!Indoors)
            {
                foreach (TownInfo town in WorldLayout.Towns)
                {
                    Vector3 centre = new Vector3(town.Center.x, me.y, town.Center.y);
                    float distance = RpgUtil.FlatDistance(centre, me);
                    if (distance > 90f || distance < 6f) continue;
                    Vector2 pos;
                    if (!WorldToGui(new Vector3(town.Center.x, WorldTerrain.GroundY(town.Center.x, town.Center.y) + 9f, town.Center.y), out pos)) continue;
                    Color color = new Color(1f, 0.88f, 0.55f, Mathf.Clamp01((90f - distance) / 20f));
                    Shadow(new Rect(pos.x - 120, pos.y - 14, 240, 28), town.Name, title, color);
                }
                float toSpawn = RpgUtil.FlatDistance(WorldBuilder.StartPosition, me);
                Vector2 spawnPos;
                if (toSpawn < 16f && WorldToGui(WorldBuilder.StartPosition + Vector3.up * 2.4f, out spawnPos))
                {
                    Shadow(new Rect(spawnPos.x - 90, spawnPos.y - 9, 180, 18), "Spawn point", shadowed, new Color(0.6f, 0.95f, 1f, Mathf.Clamp01((16f - toSpawn) / 4f)));
                }
            }
            foreach (Station station in Station.All)
            {
                if (station.Kind == StationKind.Fire && station.Town == null) continue;
                float distance = RpgUtil.FlatDistance(station.transform.position, me);
                if (distance > (station.Kind == StationKind.Door ? 7f : 14f)) continue;
                Vector2 pos;
                float lift = station.Kind == StationKind.Furnace ? 3.8f : station.Kind == StationKind.Shop ? 3.0f : station.Kind == StationKind.Door ? 2.6f : 2.3f;
                if (!WorldToGui(station.transform.position + Vector3.up * lift, out pos)) continue;
                if (station.GetComponent<Animator>() != null) continue; // NPCs share the label of their counter
                Shadow(new Rect(pos.x - 90, pos.y - 9, 180, 18), station.Label, shadowed, new Color(0.85f, 0.95f, 1f, Mathf.Clamp01((14f - distance) / 4f)));
            }
        }

        void DrawNodeProgress()
        {
            if (Camera.main == null) return;
            Vector3 me = player.transform.position;
            foreach (ResourceNode node in ResourceNode.Active)
            {
                if (node.IsDepleted || node.SinceSwing > 1.6f) continue;
                if (RpgUtil.FlatDistance(node.transform.position, me) > 8f) continue;
                Vector2 pos;
                float lift = node.Info.Kind == NodeKind.Tree ? 3.2f : 1.6f;
                if (!WorldToGui(node.transform.position + Vector3.up * lift, out pos)) continue;
                Rect bar = new Rect(pos.x - 32, pos.y, 64, 8);
                Box(new Rect(bar.x - 1, bar.y - 1, bar.width + 2, bar.height + 2), new Color(0, 0, 0, 0.8f));
                Box(bar, new Color(0.25f, 0.22f, 0.18f, 1f));
                Box(new Rect(bar.x, bar.y, bar.width * node.Progress, bar.height), new Color(0.95f, 0.75f, 0.25f, 1f));
            }
        }

        // ---------------------------------------------------------------- Maps

        Vector2 MapUv(Vector3 world)
        {
            float yaw = mapYaw * Mathf.Deg2Rad;
            Vector2 forward = new Vector2(Mathf.Sin(yaw), Mathf.Cos(yaw));
            Vector2 right = new Vector2(Mathf.Cos(yaw), -Mathf.Sin(yaw));
            Vector2 p = new Vector2(world.x, world.z);
            float extent = WorldLayout.Extent;
            return new Vector2((Vector2.Dot(p, right) + extent) / (2f * extent), (Vector2.Dot(p, forward) + extent) / (2f * extent));
        }

        void DrawMinimap(float width)
        {
            if (mapTexture == null) return;
            float size = 180f;
            Rect frame = new Rect(width - size - 16, 12, size + 4, size + 22);
            Panel(frame);
            Rect rect = new Rect(frame.x + 2, frame.y + 2, size, size);
            if (Indoors)
            {
                Box(rect, new Color(0.12f, 0.1f, 0.08f, 1f));
                Shadow(new Rect(rect.x, rect.y + size / 2 - 20, size, 20), "Indoors", bold, new Color(1f, 0.9f, 0.65f));
                Shadow(new Rect(rect.x, rect.y + size / 2, size, 20), "Leave by the door", small, Color.white);
                Shadow(new Rect(frame.x, frame.yMax - 19, frame.width, 18), InteriorDirector.Instance.Current.Name, small, new Color(1f, 0.92f, 0.7f));
                return;
            }

            Vector2 centre = MapUv(player.transform.position);
            float span = MinimapMeters / (2f * WorldLayout.Extent);
            Rect uv = new Rect(centre.x - span * 0.5f, centre.y - span * 0.5f, span, span);
            GUI.DrawTextureWithTexCoords(rect, mapTexture, uv);

            foreach (Station station in Station.All)
            {
                if (station.Kind == StationKind.Fire && station.Town == null) continue;
                if (station.Kind == StationKind.Door) continue;
                if (station.GetComponent<Animator>() != null) continue;
                Vector2 p;
                if (!MiniPoint(rect, uv, station.transform.position, out p)) continue;
                string letter = station.Kind == StationKind.Bank ? "B" : station.Kind == StationKind.Shop ? "$" : station.Kind == StationKind.Furnace ? "F" : station.Kind == StationKind.Anvil ? "A" : "*";
                Box(new Rect(p.x - 6, p.y - 6, 12, 12), new Color(0.1f, 0.1f, 0.1f, 0.85f));
                Shadow(new Rect(p.x - 6, p.y - 7, 12, 14), letter, shadowed, new Color(1f, 0.9f, 0.45f));
            }
            Vector2 spawn;
            if (MiniPoint(rect, uv, WorldBuilder.StartPosition, out spawn))
            {
                Box(new Rect(spawn.x - 4, spawn.y - 4, 8, 8), new Color(0.5f, 0.9f, 1f));
            }
            foreach (EnemyProfile enemy in EnemyProfile.All)
            {
                if (enemy.Health.IsDead()) continue;
                Vector2 p;
                if (!MiniPoint(rect, uv, enemy.transform.position, out p)) continue;
                Box(new Rect(p.x - 1.5f, p.y - 1.5f, 3, 3), enemy.Info != null && enemy.Info.IsBoss ? new Color(1f, 0.2f, 0.9f) : new Color(1f, 0.25f, 0.2f));
            }
            Vector2 me;
            MiniPoint(rect, uv, player.transform.position, out me);
            Box(new Rect(me.x - 3, me.y - 3, 6, 6), Color.white);
            // which way the camera looks, and north
            if (FollowCamera.Instance != null)
            {
                Vector3 look = FollowCamera.Instance.FlatForward;
                for (int i = 1; i <= 4; i++) Box(new Rect(me.x + look.x * i * 4f - 1, me.y - look.z * i * 4f - 1, 2, 2), new Color(1, 1, 1, 0.8f));
            }
            Shadow(new Rect(rect.center.x - 10, rect.y + 2, 20, 16), "N", bold, Color.white);

            string zone = currentZone ?? "";
            Shadow(new Rect(frame.x, frame.yMax - 19, frame.width, 18), zone, small, new Color(1f, 0.92f, 0.7f));
        }

        bool MiniPoint(Rect rect, Rect uv, Vector3 world, out Vector2 point)
        {
            Vector2 u = MapUv(world);
            float x = (u.x - uv.x) / uv.width;
            float y = (u.y - uv.y) / uv.height;
            point = new Vector2(rect.x + x * rect.width, rect.y + (1f - y) * rect.height);
            return x >= 0.02f && x <= 0.98f && y >= 0.02f && y <= 0.98f;
        }

        void DrawWorldMap(float width, float height)
        {
            float size = Mathf.Min(width - 80, height - 120);
            Rect frame = new Rect((width - size) / 2 - 6, 50, size + 12, size + 40);
            Panel(frame);
            Rect rect = new Rect(frame.x + 6, frame.y + 30, size, size);
            Shadow(new Rect(frame.x, frame.y + 6, frame.width, 20), "World map  (M to close)", bold, Color.white);
            GUI.DrawTexture(rect, mapTexture);
            Rect all = new Rect(0, 0, 1, 1);

            foreach (ZoneInfo zone in WorldLayout.Zones)
            {
                Vector2 p;
                MiniPoint(rect, all, new Vector3(zone.Center.x, 0, zone.Center.y), out p);
                Shadow(new Rect(p.x - 110, p.y + 16, 220, 18), zone.Name + "  (" + zone.Levels + ")", bold, new Color(0.85f, 0.95f, 1f));
            }
            foreach (TownInfo town in WorldLayout.Towns)
            {
                Vector2 p;
                MiniPoint(rect, all, new Vector3(town.Center.x, 0, town.Center.y), out p);
                Box(new Rect(p.x - 6, p.y - 6, 12, 12), new Color(0.95f, 0.8f, 0.3f));
                Shadow(new Rect(p.x - 100, p.y - 26, 200, 20), town.Name, bold, new Color(1f, 0.9f, 0.5f));
            }
            if (BossController.Active != null)
            {
                Vector2 p;
                MiniPoint(rect, all, BossController.Active.transform.position, out p);
                Shadow(new Rect(p.x - 60, p.y - 9, 120, 18), "Old Gullet (boss)", small, new Color(1f, 0.5f, 0.9f));
            }
            Vector2 me;
            MiniPoint(rect, all, player.transform.position, out me);
            Box(new Rect(me.x - 4, me.y - 4, 8, 8), Color.white);
            Shadow(new Rect(me.x - 30, me.y + 4, 60, 16), "You", small, Color.white);

            // which way is north on this (camera-aligned) map
            Vector2 north;
            MiniPoint(rect, all, new Vector3(0, 0, WorldLayout.Extent * 0.9f), out north);
            Shadow(new Rect(north.x - 10, north.y - 10, 20, 20), "N", bold, Color.white);
        }

        // ---------------------------------------------------------------- Station panels

        void DrawStationPanel(Station station, float width, float height)
        {
            float panelWidth = 430f;
            float panelHeight = Mathf.Min(560f, height - 140f);
            float inventoryWidth = Columns * Cell + 24;
            Rect rect = new Rect(width - inventoryWidth - 12 - panelWidth - 10, height - panelHeight - 40, panelWidth, panelHeight);
            Panel(rect);

            string heading = station.Label + (string.IsNullOrEmpty(station.Town) ? "" : "  -  " + station.Town);
            Shadow(new Rect(rect.x + 12, rect.y + 8, rect.width - 60, 20), heading, bold, new Color(1f, 0.9f, 0.6f));
            if (GUI.Button(new Rect(rect.xMax - 34, rect.y + 6, 26, 22), "X")) stationUser.Close();
            Rect body = new Rect(rect.x + 12, rect.y + 34, rect.width - 24, rect.height - 44);

            switch (station.Kind)
            {
                case StationKind.Bank: DrawBank(body); break;
                case StationKind.Shop: DrawShop(station, body); break;
                default: DrawStationRecipes(station.Kind, body); break;
            }
        }

        void DrawBank(Rect area)
        {
            BankModel model = bank.Model;
            GUI.Label(new Rect(area.x, area.y, area.width - 130, 18), "Shared by every bank. Left: take 1   Right: take all", small);
            if (GUI.Button(new Rect(area.xMax - 124, area.y - 2, 124, 22), "Deposit inventory")) bank.DepositAll();

            int columns = 7;
            float cell = 56f;
            Rect scrollArea = new Rect(area.x, area.y + 24, area.width, area.height - 24);
            int rows = Mathf.Max(1, (model.Count + columns - 1) / columns);
            Rect view = new Rect(0, 0, columns * cell, rows * cell);
            stationScroll = GUI.BeginScrollView(scrollArea, stationScroll, view);
            Event e = Event.current;
            if (model.Count == 0) GUI.Label(new Rect(0, 4, 300, 20), "Your bank is empty. Click items in your bag to deposit.", label);
            for (int i = 0; i < model.Count; i++)
            {
                ItemStack stack = model.Get(i);
                Rect c = new Rect((i % columns) * cell, (i / columns) * cell, cell - 4, cell - 4);
                Box(c, new Color(0.2f, 0.18f, 0.15f, 0.9f));
                Texture2D icon = ItemIcons.Get(stack.ItemId);
                if (icon != null) GUI.DrawTexture(new Rect(c.x + 3, c.y + 3, c.width - 6, c.height - 6), icon, ScaleMode.ScaleToFit);
                Shadow(new Rect(c.x + 2, c.y, c.width, 16), QuantityText(stack.Quantity), small, Color.yellow);
                if (c.Contains(e.mousePosition))
                {
                    ItemInfo info = GameContent.Item(stack.ItemId);
                    slotTooltip = (info != null ? info.Name : stack.ItemId) + " x" + stack.Quantity.ToString("N0") + "\nLeft-click: withdraw 1    Right-click: withdraw all";
                    if (e.type == EventType.MouseDown)
                    {
                        bank.Withdraw(stack.ItemId, e.button == 0 ? 1 : int.MaxValue);
                        e.Use();
                    }
                }
            }
            GUI.EndScrollView();
        }

        void DrawShop(Station station, Rect area)
        {
            ShopInfo shop;
            if (!GameContent.Shops.TryGetValue(station.ShopId ?? "", out shop)) return;
            int coins = inventory.Model.Count(ShopLogic.Coins);
            GUI.Label(new Rect(area.x, area.y, area.width, 18), "You have " + coins.ToString("N0") + " coins.   Click items in your bag to sell them.", small);

            float rowHeight = 40f;
            Rect scrollArea = new Rect(area.x, area.y + 24, area.width, area.height - 24);
            Rect view = new Rect(0, 0, area.width - 18, shop.Stock.Count * rowHeight);
            stationScroll = GUI.BeginScrollView(scrollArea, stationScroll, view);
            for (int i = 0; i < shop.Stock.Count; i++)
            {
                string id = shop.Stock[i];
                ItemInfo info = GameContent.Item(id);
                if (info == null) continue;
                float y = i * rowHeight;
                int price = GameContent.BuyPrice(id);
                Texture2D icon = ItemIcons.Get(id);
                if (icon != null) GUI.DrawTexture(new Rect(0, y + 2, 34, 34), icon, ScaleMode.ScaleToFit);
                bool affordable = coins >= price;
                Shadow(new Rect(40, y + 2, view.width - 180, 18), info.Name, bold, affordable ? Color.white : new Color(1f, 0.55f, 0.5f));
                GUI.Label(new Rect(40, y + 19, view.width - 180, 18), price.ToString("N0") + " coins" + Requirement(info), small);
                Rect row = new Rect(0, y, view.width - 130, rowHeight);
                if (row.Contains(Event.current.mousePosition)) slotTooltip = ItemTooltip(info);

                bool oldEnabled = GUI.enabled;
                GUI.enabled = affordable;
                int bulk = info.Stackable ? 50 : 5;
                if (GUI.Button(new Rect(view.width - 124, y + 6, 54, 26), "Buy 1")) Buy(id, 1);
                if (GUI.Button(new Rect(view.width - 66, y + 6, 64, 26), "Buy " + bulk)) Buy(id, bulk);
                GUI.enabled = oldEnabled;
            }
            GUI.EndScrollView();
        }

        static string Requirement(ItemInfo info)
        {
            if (info.IsEquippable || info.Tool != ToolKind.None) return "   (" + info.RequiredSkill + " " + info.RequiredLevel + ")";
            if (info.IsEdible) return "   (heals " + info.HealAmount + ")";
            return "";
        }

        void Buy(string id, int quantity)
        {
            string message;
            bool ok = ShopLogic.TryBuy(inventory.Model, id, quantity, out message);
            if (message != null) GameLog.Add(message, ok ? GameLog.Normal : GameLog.Warning);
            if (ok) Sfx.Ui("coins", 0.6f);
            inventory.NotifyChanged();
        }

        void DrawStationRecipes(StationKind kind, Rect area)
        {
            List<Recipe> recipes = RecipesAt(kind);
            string hint = kind == StationKind.Anvil ? "Needs a hammer in your bag." : kind == StationKind.Fire ? "Food can burn; less often at higher Cooking." : "Coal is needed for steel and up.";
            if (crafter != null && crafter.Busy) hint = "Making " + crafter.Current.Name + "... (" + crafter.Remaining + " left, click to move to stop)";
            GUI.Label(new Rect(area.x, area.y, area.width, 18), hint, small);

            float rowHeight = 46f;
            Rect scrollArea = new Rect(area.x, area.y + 22, area.width, area.height - 22);
            Rect view = new Rect(0, 0, area.width - 18, recipes.Count * rowHeight);
            stationScroll = GUI.BeginScrollView(scrollArea, stationScroll, view);
            for (int i = 0; i < recipes.Count; i++)
            {
                Recipe recipe = recipes[i];
                float y = i * rowHeight;
                bool hasLevel = skills.GetLevel(recipe.Skill) >= recipe.Level;
                bool canMake = inventory.CanMake(recipe);
                Texture2D icon = ItemIcons.Get(recipe.Output.ItemId);
                if (icon != null) GUI.DrawTexture(new Rect(0, y + 4, 36, 36), icon, ScaleMode.ScaleToFit);

                Color nameColor = canMake ? Color.white : hasLevel ? new Color(0.8f, 0.8f, 0.8f) : new Color(1f, 0.5f, 0.45f);
                string burn = "";
                if (!string.IsNullOrEmpty(recipe.BurntItemId) && hasLevel) burn = "  (burn " + Mathf.RoundToInt((float)recipe.BurnChance(skills.GetLevel(recipe.Skill)) * 100f) + "%)";
                Shadow(new Rect(42, y + 2, view.width - 170, 18), recipe.Name + burn, bold, nameColor);
                GUI.Label(new Rect(42, y + 20, view.width - 170, 18), recipe.Skill + " " + recipe.Level + "  |  " + recipe.Xp.ToString("0.#") + " xp  |  " + Inputs(recipe), small);

                bool oldEnabled = GUI.enabled;
                GUI.enabled = canMake;
                if (GUI.Button(new Rect(view.width - 122, y + 8, 50, 26), "1")) crafter.Make(recipe, 1);
                if (GUI.Button(new Rect(view.width - 68, y + 8, 66, 26), "All")) crafter.Make(recipe, 28);
                GUI.enabled = oldEnabled;
            }
            GUI.EndScrollView();
        }

        // ---------------------------------------------------------------- Skills

        void DrawSkills(float height)
        {
            float rowHeight = 30;
            Rect rect = new Rect(12, 114, 300, SkillInfo.Count * rowHeight + 60);
            Panel(rect);
            GUI.Label(new Rect(rect.x + 12, rect.y + 8, 280, 20), "Skills", bold);

            for (int i = 0; i < SkillInfo.Count; i++)
            {
                Skill skill = (Skill)i;
                float y = rect.y + 34 + i * rowHeight;
                int level = skills.GetLevel(skill);
                double xp = skills.GetXp(skill);
                float progress = ExperienceTable.ProgressToNext(xp);

                GUI.Label(new Rect(rect.x + 12, y, 110, 20), SkillInfo.Name(skill), label);
                GUI.Label(new Rect(rect.x + 120, y, 40, 20), level.ToString(), bold);

                Rect bar = new Rect(rect.x + 160, y + 4, 128, 12);
                Box(bar, new Color(0.25f, 0.22f, 0.18f, 1f));
                Box(new Rect(bar.x, bar.y, bar.width * progress, bar.height), new Color(0.85f, 0.65f, 0.2f, 1f));

                Rect rowRect = new Rect(rect.x + 8, y, rect.width - 16, rowHeight);
                if (rowRect.Contains(Event.current.mousePosition))
                {
                    string next = level >= ExperienceTable.MaxLevel
                        ? "Max level!"
                        : "Next level at " + ExperienceTable.XpForLevel(level + 1).ToString("N0") + " xp ("
                          + (ExperienceTable.XpForLevel(level + 1) - xp).ToString("N0") + " to go)";
                    slotTooltip = SkillInfo.Name(skill) + ": " + xp.ToString("N0") + " xp\n" + next + "\n" + SkillHint(skill);
                }
            }

            GUI.Label(new Rect(rect.x + 12, rect.yMax - 24, 280, 20),
                "Total level " + skills.TotalLevel() + "    Combat level " + skills.CombatLevel(), small);
        }

        static string SkillHint(Skill skill)
        {
            switch (skill)
            {
                case Skill.Attack: return "Trained in the Slash style. Raises accuracy and lets you wield better weapons.";
                case Skill.Strength: return "Trained in the Crush style. Raises your max hit.";
                case Skill.Defense: return "Trained in the Shield bash style (needs a shield). Blocks damage; lets you wear better armour.";
                case Skill.Vitality: return "Trained by all combat. 10 HP per level.";
                case Skill.Ranged: return "Equip a bow (craft one from logs) and craft arrows.";
                case Skill.Magic: return "Wield a staff; it burns spark stones from your bag (copper/tin rocks and Gnarls drop them).";
                case Skill.Woodcutting: return "Wield an axe and click trees. Pine 1, Oak 15, Willow 30, Maple 45, Yew 60, Ashwood 75.";
                case Skill.Mining: return "Wield a pickaxe and click rocks. Copper/Tin 1, Iron 15, Coal 30, Verdite 40, Cobalt 55, Cinderite 70.";
                case Skill.Smithing: return "Smelt bars at a furnace, then hammer them into gear at an anvil.";
                case Skill.Crafting: return "Bows, arrows and leather armour in the Make tab (I).";
                case Skill.Pyro: return "Buy a flint and steel, then click logs in your bag. Same log levels as Woodcutting.";
                case Skill.Cooking: return "Cook raw meat on any fire (town hearths too). Burns less as you level.";
            }
            return "";
        }
    }
}
