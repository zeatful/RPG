using System.Collections.Generic;
using RPG.Combat;
using RPG.Content;
using RPG.Control;
using RPG.Core;
using RPG.Gathering;
using RPG.Items;
using RPG.Stats;
using UnityEngine;

namespace RPG.UI
{
    /// <summary>
    /// Immediate-mode HUD (OnGUI) so it needs no prefabs or canvas setup:
    /// HP bar, chat log, XP drops, level-up banner, hitsplats, enemy nameplates,
    /// hover tooltips, inventory + crafting panel (I) and skills panel (K).
    /// </summary>
    public class RpgHud : MonoBehaviour
    {
        const float NameplateRange = 25f;
        const int Columns = 4;
        const float Cell = 54f;

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

        bool showInventory = true;
        bool showSkills = false;
        bool craftingTab = false;
        Vector2 recipeScroll;

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
            Hitsplat splat = new Hitsplat();
            splat.Target = defender.transform;
            splat.Damage = damage;
            splat.Time = Time.time;
            splat.Jitter = new Vector2(Random.Range(-14f, 14f), Random.Range(-8f, 8f));
            hitsplats.Add(splat);
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.I)) showInventory = !showInventory;
            if (Input.GetKeyDown(KeyCode.K)) showSkills = !showSkills;
            UpdateHover();
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
                ResourceNode node = hit.transform.GetComponentInParent<ResourceNode>();
                if (node != null)
                {
                    hoverText = node.IsDepleted
                        ? node.Info.Name + " (depleted)"
                        : node.ActionVerb + " " + node.Info.Name + " (" + node.Info.Skill + " " + node.Info.Level + ")";
                    return;
                }
            }
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
            InitStyles();

            scale = Mathf.Max(1f, Screen.height / 900f);
            GUI.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, new Vector3(scale, scale, 1f));
            float width = Screen.width / scale;
            float height = Screen.height / scale;

            if (Event.current.type == EventType.Layout) panelRects.Clear();

            DrawNameplates();
            DrawHitsplats();
            DrawStatusBar();
            DrawBossBar(width);
            DrawXpDrops(width);
            DrawBanner(width);
            DrawLog(height);
            if (showInventory) DrawInventory(width, height);
            if (showSkills) DrawSkills(height);
            DrawHelp(width, height);
            DrawHoverTooltip();

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

                float fraction = enemy.Health.GetFraction();
                if (fraction < 1f)
                {
                    Rect bar = new Rect(pos.x - 30, pos.y - 10, 60, 6);
                    Box(bar, new Color(0.6f, 0.05f, 0.05f, 0.9f));
                    Box(new Rect(bar.x, bar.y, bar.width * fraction, bar.height), new Color(0.2f, 0.85f, 0.25f, 0.95f));
                }
            }
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

        void DrawStatusBar()
        {
            Rect rect = new Rect(12, 12, 250, 56);
            Panel(rect);
            float fraction = health.GetFraction();
            Rect bar = new Rect(22, 22, 230, 18);
            Box(bar, new Color(0.45f, 0.05f, 0.05f, 1f));
            Box(new Rect(bar.x, bar.y, bar.width * fraction, bar.height), new Color(0.15f, 0.75f, 0.2f, 1f));
            Shadow(bar, "HP " + Mathf.CeilToInt(health.GetHealth()) + " / " + Mathf.CeilToInt(health.GetMaxHealth()), shadowed, Color.white);
            GUI.Label(new Rect(22, 43, 230, 20), "Combat level " + skills.CombatLevel() + "    Total level " + skills.TotalLevel(), small);
            if (DayNightCycle.Instance != null)
            {
                Shadow(new Rect(12, 70, 250, 18), DayNightCycle.Instance.ClockText, small, new Color(1f, 1f, 1f, 0.85f));
            }
        }

        void DrawBossBar(float width)
        {
            BossController boss = BossController.Active;
            if (boss == null || !boss.IsEngaged || boss.Profile == null || boss.Profile.Info == null) return;
            Health bossHealth = boss.Profile.Health;
            Rect rect = new Rect(width / 2 - 200, 14, 400, 44);
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
                Shadow(new Rect(width - 260, y - age * 25f, 240, 20), drop.Text + " xp", bold, color);
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
            Shadow(new Rect(0, 90, width, 36), banner.Text, title, color);
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
                "Click: move / attack / gather / loot     I: inventory     K: skills     S / L: save / load",
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
            float panelHeight = 7 * Cell + 168;
            Rect rect = new Rect(width - panelWidth - 12, height - panelHeight - 40, panelWidth, panelHeight);
            Panel(rect);

            float x = rect.x + 12;
            float y = rect.y + 10;

            if (GUI.Toggle(new Rect(x, y, 104, 24), !craftingTab, "Bag", "Button")) craftingTab = false;
            if (GUI.Toggle(new Rect(x + 112, y, 104, 24), craftingTab, "Make", "Button")) craftingTab = true;
            y += 32;

            InventoryModel model = inventory.Model;

            // equipment
            y = DrawEquipRow(x, y, "Weapon", model.WeaponId, EquipSlot.Weapon);
            y = DrawEquipRow(x, y, "Body", model.BodyId, EquipSlot.Body);
            GUI.Label(new Rect(x, y, panelWidth, 18),
                "Atk +" + model.TotalAttackBonus() + "   Str +" + model.TotalStrengthBonus() + "   Def +" + model.TotalDefenseBonus()
                + "   Max hit " + CombatMath.MaxHit(stats.StrengthLevel, model.TotalStrengthBonus()), small);
            y += 18;
            ItemInfo weapon = GameContent.Item(model.WeaponId);
            string styleText = "Style: " + stats.Style;
            if (weapon != null && !string.IsNullOrEmpty(weapon.AmmoId))
            {
                ItemInfo ammo = GameContent.Item(weapon.AmmoId);
                styleText += "   " + (ammo != null ? ammo.Name : weapon.AmmoId) + ": " + model.Count(weapon.AmmoId);
            }
            GUI.Label(new Rect(x, y, panelWidth, 18), styleText, small);
            y += 22;

            if (craftingTab) DrawRecipes(new Rect(x, y, panelWidth - 24, rect.yMax - y - 10));
            else DrawSlots(x, y);
        }

        float DrawEquipRow(float x, float y, string slotName, string itemId, EquipSlot slot)
        {
            ItemInfo info = GameContent.Item(itemId);
            GUI.Label(new Rect(x, y, 60, 20), slotName + ":", bold);
            Texture2D icon = info != null ? ItemIcons.Get(itemId) : null;
            if (icon != null) GUI.DrawTexture(new Rect(x + 58, y, 20, 20), icon, ScaleMode.ScaleToFit);
            GUI.Label(new Rect(x + 80, y, 100, 20), info != null ? info.Name : "-", label);
            if (info != null && GUI.Button(new Rect(x + 180, y, 36, 18), "Off")) inventory.Unequip(slot);
            return y + 22;
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

        string ItemTooltip(ItemInfo info)
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
                if (info.Style != CombatStyle.Melee)
                {
                    ItemInfo ammo = GameContent.Item(info.AmmoId);
                    text += "\n" + info.Style + " weapon, range " + info.Range + "m, uses " + (ammo != null ? ammo.Name.ToLower() : "nothing");
                }
                text += "\nAtk +" + info.AttackBonus + "  Str +" + info.StrengthBonus + "  Def +" + info.DefenseBonus;
                text += "\nRequires " + info.RequiredSkill + " " + info.RequiredLevel;
                text += "\nLeft-click: equip    Right-click: drop";
            }
            else if (info.IsEdible)
            {
                text += "\nHeals " + info.HealAmount + "\nLeft-click: eat    Right-click: drop";
            }
            else
            {
                if (!string.IsNullOrEmpty(info.Description)) text += "\n" + info.Description;
                text += "\nRight-click: drop";
            }
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

        void DrawRecipes(Rect area)
        {
            List<Recipe> recipes = GameContent.Recipes;
            float rowHeight = 46;
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
            foreach (ItemStack input in recipe.Inputs)
            {
                ItemInfo info = GameContent.Item(input.ItemId);
                string name = info != null ? info.Name : input.ItemId;
                parts.Add(input.Quantity + "x " + name + " (" + inventory.Model.Count(input.ItemId) + ")");
            }
            return string.Join(", ", parts.ToArray());
        }

        // ---------------------------------------------------------------- Skills

        void DrawSkills(float height)
        {
            float rowHeight = 30;
            Rect rect = new Rect(12, 80, 300, SkillInfo.Count * rowHeight + 60);
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
                case Skill.Attack: return "Trained by dealing melee damage. Raises accuracy.";
                case Skill.Strength: return "Trained by dealing melee damage. Raises max hit.";
                case Skill.Defense: return "Trained by being attacked (bonus for dodges).";
                case Skill.Vitality: return "Trained by all combat. 10 HP per level.";
                case Skill.Ranged: return "Equip a bow (craft one from logs) and craft arrows.";
                case Skill.Magic: return "Equip a wand; spark stones come from copper/tin rocks and Gnarls.";
                case Skill.Woodcutting: return "Click trees. Pine 1, Oak 15, Willow 30.";
                case Skill.Mining: return "Click rocks. Copper/Tin 1, Iron 15.";
                case Skill.Smithing: return "Smelt bars and forge gear in the Make tab (I).";
                case Skill.Crafting: return "Bows and leather armour in the Make tab (I).";
            }
            return "";
        }
    }
}
