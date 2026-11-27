using System;
using System.Collections.Generic;
using RPG.Drops;
using RPG.Items;
using RPG.Stats;

namespace RPG.Content
{
    public class MaterialTier
    {
        public string Name;
        public int Level;
        public string ColorHex;
        public string GlowHex;
        public string Passive;

        public MaterialTier(string name, int level, string colorHex, string glowHex, string passive)
        {
            Name = name;
            Level = level;
            ColorHex = colorHex;
            GlowHex = glowHex;
            Passive = passive;
        }
    }

    public class EnemyInfo
    {
        public string Id;
        public string Name;
        public int Level;
        public int Attack;
        public int Strength;
        public int Defense;
        public int AttackBonus;
        public int StrengthBonus;
        public int DefenseBonus;
        public int MaxHp;
        public float RespawnSeconds = 30f;
        public string TintHex;
        public string DropTableId;
        public float Scale = 1f;
        public bool IsBoss;

        // Aggressive enemies attack on sight; the rest only fight back once you hit them.
        public bool Aggressive;
    }

    public enum NodeKind
    {
        Tree,
        Rock
    }

    public class NodeInfo
    {
        public string Id;
        public string Name;
        public NodeKind Kind;
        public Skill Skill;
        public int Level;
        public double Xp;
        public string ItemId;
        public float RespawnSeconds;

        // Work needed per log/ore. Each swing does SwingPower(level, tool) work, so
        // higher levels and better tools need fewer swings.
        public double Hardness;

        // How many logs/ores the node gives before it is depleted (random in range). Currently always 1.
        public int YieldMin = 1;
        public int YieldMax = 1;

        public string ColorHex;
        public string AccentHex;

        // Optional extra item found alongside the main one (e.g. spark stones in copper rock).
        public string BonusItemId;
        public double BonusChance;
    }

    /// <summary>Logs that can be lit with a flint and steel (Pyro skill).</summary>
    public class BurnInfo
    {
        public string LogId;
        public int Level;
        public double Xp;
        public float BurnSeconds;
    }

    public class ShopInfo
    {
        public string Id;
        public string Name;
        public List<string> Stock = new List<string>();
    }

    /// <summary>
    /// All game data in one place. Defined in code so the prototype runs with no editor setup;
    /// this can move to ScriptableObjects later. Pure C#.
    ///
    /// Economy rules of thumb: monsters drop a few coins (humanoids and golems only; animals drop meat
    /// and hides), shops sell at an item's Value and buy back at 40%, food is cheap and heals little,
    /// gear is expensive (an iron platebody is roughly an hour of bandit kills). Smithing turns ore
    /// into gear worth a bit more than the ore, but each tier needs more bars and more coal.
    /// </summary>
    public static class GameContent
    {
        // Multiplies all combat XP. 1 = intended rate.
        public const double CombatXpRate = 1.0;

        // Shops buy items back at this fraction of their value.
        public const double SellRate = 0.4;

        public static readonly List<MaterialTier> Tiers = new List<MaterialTier>
        {
            new MaterialTier("Bronze", 1, "#B87333", null, "None"),
            new MaterialTier("Iron", 10, "#8A8D91", null, "None"),
            new MaterialTier("Steel", 20, "#C8CDD2", null, "None"),
            new MaterialTier("Verdite", 30, "#3FA58A", null, "+10% damage vs undead and constructs"),
            new MaterialTier("Cobalt", 40, "#2B4EA8", null, "Light weapons attack 1 tick faster"),
            new MaterialTier("Cinderite", 50, "#2A1A14", "#FF6A1A", "15% chance to burn"),
            new MaterialTier("Rimesteel", 60, "#BFE6FF", "#E8F7FF", "15% chance to slow"),
            new MaterialTier("Voidglass", 70, "#5B2A86", "#B57CFF", "Ignores 20% armor, loses durability 2x faster"),
            new MaterialTier("Aurium", 80, "#F5D76E", "#FFFFFF", "Heals 5% of damage dealt"),
            new MaterialTier("Bloodstone", 90, "#8B0A1A", "#FF2A3A", "Up to +20% damage as HP drops"),
            new MaterialTier("Celestine", 99, "#9FE8FF", "#FFB3F5", "Takes on the passive of the current zone"),
        };

        /// <summary>
        /// The six tiers that can be mined and smithed. Smithing levels follow RuneScape's spacing; bars per
        /// item and coal per bar rise with the tier so higher gear is a real grind.
        /// </summary>
        public class SmithTier
        {
            public string Tier;
            public string OreId;          // main ore for the bar (bronze also needs tin)
            public int CoalPerBar;
            public int SmeltLevel;        // anvil items unlock at this level and above
            public double SmeltXp;
            public double XpPerBar;       // anvil xp per bar used
            public int BarValue;
            public int SwordAttack, SwordStrength, PlateDefense;
            public int SwordValue, PlateValue, AxeValue, PickaxeValue;
            public float ToolPower;
        }

        public static readonly List<SmithTier> SmithTiers = new List<SmithTier>
        {
            new SmithTier { Tier = "Bronze", OreId = "ore_copper", CoalPerBar = 0, SmeltLevel = 1, SmeltXp = 6.5, XpPerBar = 12.5, BarValue = 20,
                SwordAttack = 6, SwordStrength = 5, PlateDefense = 15, SwordValue = 80, PlateValue = 250, AxeValue = 30, PickaxeValue = 45, ToolPower = 1.0f },
            new SmithTier { Tier = "Iron", OreId = "ore_iron", CoalPerBar = 0, SmeltLevel = 15, SmeltXp = 12.5, XpPerBar = 25, BarValue = 60,
                SwordAttack = 12, SwordStrength = 10, PlateDefense = 25, SwordValue = 320, PlateValue = 1000, AxeValue = 120, PickaxeValue = 160, ToolPower = 1.3f },
            new SmithTier { Tier = "Steel", OreId = "ore_iron", CoalPerBar = 2, SmeltLevel = 30, SmeltXp = 17.5, XpPerBar = 37.5, BarValue = 160,
                SwordAttack = 18, SwordStrength = 16, PlateDefense = 35, SwordValue = 1000, PlateValue = 3200, AxeValue = 400, PickaxeValue = 520, ToolPower = 1.6f },
            new SmithTier { Tier = "Verdite", OreId = "ore_verdite", CoalPerBar = 3, SmeltLevel = 45, SmeltXp = 30, XpPerBar = 50, BarValue = 450,
                SwordAttack = 25, SwordStrength = 22, PlateDefense = 46, SwordValue = 2600, PlateValue = 8000, AxeValue = 1200, PickaxeValue = 1600, ToolPower = 2.0f },
            new SmithTier { Tier = "Cobalt", OreId = "ore_cobalt", CoalPerBar = 4, SmeltLevel = 60, SmeltXp = 40, XpPerBar = 62.5, BarValue = 1300,
                SwordAttack = 32, SwordStrength = 29, PlateDefense = 58, SwordValue = 7500, PlateValue = 24000, AxeValue = 3500, PickaxeValue = 4600, ToolPower = 2.4f },
            new SmithTier { Tier = "Cinderite", OreId = "ore_cinderite", CoalPerBar = 6, SmeltLevel = 75, SmeltXp = 55, XpPerBar = 75, BarValue = 3600,
                SwordAttack = 40, SwordStrength = 36, PlateDefense = 72, SwordValue = 22000, PlateValue = 70000, AxeValue = 10000, PickaxeValue = 13000, ToolPower = 2.9f },
        };

        // Bars needed per anvil item, and how many levels above the tier's smelt level it unlocks.
        public const int AxeBars = 1, SwordBars = 2, HelmBars = 2, PickaxeBars = 2, ShieldBars = 3, LegsBars = 3, PlateBars = 5;
        public const int AxeOffset = 0, SwordOffset = 2, HelmOffset = 3, PickaxeOffset = 4, ShieldOffset = 6, LegsOffset = 8, PlateOffset = 10;

        // Other armour pieces relative to the platebody of the same tier (defence and price).
        public const double HelmShare = 0.4, LegsShare = 0.6, ShieldShare = 0.5;

        public static readonly Dictionary<string, ItemInfo> Items = BuildItems();
        public static readonly Dictionary<string, EnemyInfo> Enemies = BuildEnemies();
        public static readonly Dictionary<string, DropTable> DropTables = BuildDropTables();
        public static readonly Dictionary<string, NodeInfo> Nodes = BuildNodes();
        public static readonly List<Recipe> Recipes = BuildRecipes();
        public static readonly Dictionary<string, BurnInfo> Burnables = BuildBurnables();
        public static readonly Dictionary<string, ShopInfo> Shops = BuildShops();

        public const string LuckyTableId = "lucky";
        public const string FlintId = "flint_and_steel";
        public const string HammerId = "hammer";

        // Order enemies are handed out to the enemies placed in the old Sandbox scene.
        public static readonly string[] SceneEnemyRotation = { "thistlehog", "gnarl", "grey_wolf", "bandit" };

        public const string BossId = "old_gullet";

        public static ItemInfo Item(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            ItemInfo info;
            return Items.TryGetValue(id, out info) ? info : null;
        }

        public static MaterialTier Tier(string name)
        {
            foreach (MaterialTier tier in Tiers) if (tier.Name == name) return tier;
            return null;
        }

        public static int BuyPrice(string itemId)
        {
            ItemInfo info = Item(itemId);
            return info != null ? Math.Max(1, info.Value) : 0;
        }

        /// <summary>What a shop pays for one; 0 means shops won't take it.</summary>
        public static int SellPrice(string itemId)
        {
            ItemInfo info = Item(itemId);
            if (info == null || info.Value <= 0) return 0;
            return (int)Math.Floor(info.Value * SellRate);
        }

        static Dictionary<string, ItemInfo> BuildItems()
        {
            Dictionary<string, ItemInfo> items = new Dictionary<string, ItemInfo>();

            // Resources
            Add(items, new ItemInfo { Id = "coins", Name = "Coins", ColorHex = "#E8C547", Stackable = true, Value = 0 });
            Add(items, new ItemInfo { Id = "bones", Name = "Bones", ColorHex = "#EDE6D6", Value = 5 });
            Add(items, new ItemInfo { Id = "hide", Name = "Hide", ColorHex = "#9C6B3F", Value = 12 });
            Add(items, new ItemInfo { Id = "wolfhide", Name = "Wolfhide", ColorHex = "#7D7F86", Value = 30 });
            Add(items, new ItemInfo { Id = "logs_pine", Name = "Pine logs", ColorHex = "#A0703A", Value = 4 });
            Add(items, new ItemInfo { Id = "logs_oak", Name = "Oak logs", ColorHex = "#7A5230", Value = 10 });
            Add(items, new ItemInfo { Id = "logs_willow", Name = "Willow logs", ColorHex = "#8FA35A", Value = 18 });
            Add(items, new ItemInfo { Id = "logs_maple", Name = "Maple logs", ColorHex = "#B8504A", Value = 35 });
            Add(items, new ItemInfo { Id = "logs_yew", Name = "Yew logs", ColorHex = "#5A3A2E", Value = 80 });
            Add(items, new ItemInfo { Id = "logs_ashwood", Name = "Ashwood logs", ColorHex = "#4A4440", Value = 180 });
            Add(items, new ItemInfo { Id = "ore_copper", Name = "Copper ore", ColorHex = "#C8733A", Value = 4 });
            Add(items, new ItemInfo { Id = "ore_tin", Name = "Tin ore", ColorHex = "#B9BEC4", Value = 4 });
            Add(items, new ItemInfo { Id = "ore_iron", Name = "Iron ore", ColorHex = "#6E4A3A", Value = 15 });
            Add(items, new ItemInfo { Id = "ore_coal", Name = "Coal", ColorHex = "#2E2E34", Value = 30 });
            Add(items, new ItemInfo { Id = "ore_verdite", Name = "Verdite ore", ColorHex = "#3FA58A", Value = 140 });
            Add(items, new ItemInfo { Id = "ore_cobalt", Name = "Cobalt ore", ColorHex = "#3A5BD0", Value = 380 });
            Add(items, new ItemInfo { Id = "ore_cinderite", Name = "Cinderite ore", ColorHex = "#E0602A", Value = 1000 });

            // Food: shop food is cheap but weak; cooking meat gives better healing.
            Add(items, new ItemInfo { Id = "cheese", Name = "Cheese", ColorHex = "#F2C94C", HealAmount = 8, Value = 4 });
            Add(items, new ItemInfo { Id = "bread", Name = "Bread", ColorHex = "#C98B4A", HealAmount = 14, Value = 8 });
            Add(items, new ItemInfo { Id = "meat_pie", Name = "Meat pie", ColorHex = "#B5793A", HealAmount = 20, Value = 18 });
            Add(items, new ItemInfo { Id = "raw_meat", Name = "Raw meat", ColorHex = "#C0504D", Value = 3, Description = "Cook it on a fire before eating." });
            Add(items, new ItemInfo { Id = "cooked_meat", Name = "Cooked meat", ColorHex = "#8A4A2A", HealAmount = 25, Value = 8 });
            Add(items, new ItemInfo { Id = "raw_prime_meat", Name = "Raw prime cut", ColorHex = "#D8405A", Value = 10, Description = "A thick cut from a big beast. Needs Cooking 30." });
            Add(items, new ItemInfo { Id = "prime_steak", Name = "Prime steak", ColorHex = "#7A3A22", HealAmount = 45, Value = 22 });
            Add(items, new ItemInfo { Id = "burnt_meat", Name = "Burnt meat", ColorHex = "#2A2220", Value = 0, Description = "Charcoal with ambitions." });

            // Tools
            Add(items, new ItemInfo { Id = FlintId, Name = "Flint and steel", ColorHex = "#8A8D91", Value = 12, Description = "Click logs in your bag to light a fire." });
            Add(items, new ItemInfo { Id = HammerId, Name = "Hammer", ColorHex = "#8A8D91", Value = 8, Description = "Needed to smith at an anvil." });

            // Smithable tiers: bars, swords, platebodies, axes, pickaxes
            foreach (SmithTier tier in SmithTiers)
            {
                string t = tier.Tier;
                string key = t.ToLower();
                MaterialTier material = Tier(t);
                Add(items, new ItemInfo { Id = "bar_" + key, Name = t + " bar", ColorHex = material.ColorHex, Tier = t, Value = tier.BarValue });

                ItemInfo sword = Weapon("sword_" + key, t + " sword", t, material.Level, tier.SwordAttack, tier.SwordStrength);
                sword.Value = tier.SwordValue;
                Add(items, sword);

                ItemInfo plate = Armor("platebody_" + key, t + " platebody", t, Skill.Defense, material.Level, tier.PlateDefense);
                plate.Value = tier.PlateValue;
                Add(items, plate);
                Add(items, Piece("helm_" + key, t + " full helm", t, EquipSlot.Head, material.Level, tier, HelmShare));
                Add(items, Piece("platelegs_" + key, t + " platelegs", t, EquipSlot.Legs, material.Level, tier, LegsShare));
                Add(items, Piece("kiteshield_" + key, t + " kiteshield", t, EquipSlot.Shield, material.Level, tier, ShieldShare));

                // Axes and pickaxes are wielded in the weapon slot (you must hold one to chop or mine). They
                // need the gathering level to wield and hit a little, but much worse than a sword.
                Add(items, new ItemInfo
                {
                    Id = "axe_" + key, Name = t + " axe", ColorHex = material.ColorHex, Tier = t, Value = tier.AxeValue,
                    Tool = ToolKind.Axe, ToolPower = tier.ToolPower, RequiredSkill = Skill.Woodcutting, RequiredLevel = material.Level,
                    Slot = EquipSlot.Weapon, AttackBonus = tier.SwordAttack / 3, StrengthBonus = tier.SwordStrength / 2
                });
                Add(items, new ItemInfo
                {
                    Id = "pickaxe_" + key, Name = t + " pickaxe", ColorHex = material.ColorHex, Tier = t, Value = tier.PickaxeValue,
                    Tool = ToolKind.Pickaxe, ToolPower = tier.ToolPower, RequiredSkill = Skill.Mining, RequiredLevel = material.Level,
                    Slot = EquipSlot.Weapon, AttackBonus = tier.SwordAttack / 3, StrengthBonus = tier.SwordStrength / 2
                });
            }

            Add(items, new ItemInfo { Id = "alpha_fang", Name = "Alpha Fang", ColorHex = "#E6E6E6", Slot = EquipSlot.Weapon, AttackBonus = 15, StrengthBonus = 12, RequiredSkill = Skill.Attack, RequiredLevel = 10, Rare = true, Value = 900, Description = "A wolf's fang, sharpened into a dagger." });
            Add(items, new ItemInfo { Id = "gullet_hook", Name = "Gullet Hook", ColorHex = "#5F8A7A", Slot = EquipSlot.Weapon, AttackBonus = 16, StrengthBonus = 18, RequiredSkill = Skill.Attack, RequiredLevel = 15, Rare = true, Value = 2500, Description = "Pulled from Old Gullet's jaw. Still smells of river." });

            // Ranged: two-handed bows fire arrows from the ammo slot (one per shot, up to 999 equipped)
            Add(items, new ItemInfo { Id = "pine_arrows", Name = "Pine arrows", ColorHex = "#C9A06A", Stackable = true, Value = 3, Slot = EquipSlot.Ammo, RequiredSkill = Skill.Ranged, Description = "Ammo for bows. Equip them (up to 999) to fire them." });
            Add(items, new ItemInfo { Id = "shortbow_pine", Name = "Pine shortbow", ColorHex = "#A0703A", Slot = EquipSlot.Weapon, TwoHanded = true, Style = CombatStyle.Ranged, AmmoId = "pine_arrows", Range = 7f, AttackBonus = 8, StrengthBonus = 6, RequiredSkill = Skill.Ranged, RequiredLevel = 1, Value = 60 });
            Add(items, new ItemInfo { Id = "shortbow_oak", Name = "Oak shortbow", ColorHex = "#7A5230", Slot = EquipSlot.Weapon, TwoHanded = true, Style = CombatStyle.Ranged, AmmoId = "pine_arrows", Range = 8f, AttackBonus = 14, StrengthBonus = 10, RequiredSkill = Skill.Ranged, RequiredLevel = 10, Value = 260 });

            // Magic: two-handed staves burn spark stones from the bag (one per cast)
            Add(items, new ItemInfo { Id = "spark_stone", Name = "Spark stone", ColorHex = "#FFB347", Stackable = true, Value = 4, Description = "Crackles when held. Staves burn one per cast." });
            Add(items, new ItemInfo { Id = "staff_pine", Name = "Pine staff", ColorHex = "#E0A060", Slot = EquipSlot.Weapon, TwoHanded = true, Style = CombatStyle.Magic, AmmoId = "spark_stone", Range = 6f, AttackBonus = 9, StrengthBonus = 8, RequiredSkill = Skill.Magic, RequiredLevel = 1, Value = 70 });
            Add(items, new ItemInfo { Id = "staff_oak", Name = "Oak staff", ColorHex = "#C07A3A", Slot = EquipSlot.Weapon, TwoHanded = true, Style = CombatStyle.Magic, AmmoId = "spark_stone", Range = 7f, AttackBonus = 15, StrengthBonus = 13, RequiredSkill = Skill.Magic, RequiredLevel = 10, Value = 300 });

            // Pets
            Add(items, new ItemInfo { Id = "pet_gullet", Name = "Little Gullet", ColorHex = "#4A6B5A", Rare = true, IsPet = true, Description = "A tiny river brute. Left-click to summon or dismiss." });

            // Leather armour and a wooden shield: cheap starter gear made from hides and logs
            Add(items, new ItemInfo { Id = "leather_cowl", Name = "Leather cowl", ColorHex = "#9C6B3F", Slot = EquipSlot.Head, DefenseBonus = 3, RequiredSkill = Skill.Defense, RequiredLevel = 1, Value = 25 });
            Add(items, new ItemInfo { Id = "leather_chaps", Name = "Leather chaps", ColorHex = "#8A5A35", Slot = EquipSlot.Legs, DefenseBonus = 5, RequiredSkill = Skill.Defense, RequiredLevel = 1, Value = 40 });
            Add(items, new ItemInfo { Id = "shield_wood", Name = "Wooden shield", ColorHex = "#8A6038", Slot = EquipSlot.Shield, DefenseBonus = 5, RequiredSkill = Skill.Defense, RequiredLevel = 1, Value = 30, Description = "Lets you fight in the Defense (shield bash) style." });
            Add(items, new ItemInfo { Id = "leather_body", Name = "Leather body", ColorHex = "#9C6B3F", Slot = EquipSlot.Body, DefenseBonus = 8, RequiredSkill = Skill.Defense, RequiredLevel = 1, Value = 60 });
            Add(items, new ItemInfo { Id = "hard_leather_body", Name = "Hardened leather body", ColorHex = "#6F4A2A", Slot = EquipSlot.Body, DefenseBonus = 14, RequiredSkill = Skill.Defense, RequiredLevel = 10, Value = 180 });
            Add(items, new ItemInfo { Id = "wolfhide_body", Name = "Wolfhide body", ColorHex = "#7D7F86", Slot = EquipSlot.Body, DefenseBonus = 20, AttackBonus = 2, RequiredSkill = Skill.Defense, RequiredLevel = 20, Value = 600 });

            // Rares and Lucky Table
            Add(items, new ItemInfo { Id = "thistle_quill", Name = "Thistle Quill", ColorHex = "#D8C27A", Rare = true, Value = 150, Description = "Needle-sharp. A collector in Brookhollow would pay well for it." });
            Add(items, new ItemInfo { Id = "gnarl_crown", Name = "Gnarl Chief's Crown", ColorHex = "#5E8C31", Rare = true, Value = 1500, Description = "Cosmetic. Gnarls would be furious." });
            Add(items, new ItemInfo { Id = "tattered_map", Name = "Tattered Map", ColorHex = "#D9C79E", Rare = true, Value = 200, Description = "A sketch of somewhere nearby. Treasure hunts are coming later." });
            Add(items, new ItemInfo { Id = "gem_jade", Name = "Uncut jade", ColorHex = "#3FA55A", Rare = true, Value = 400 });
            Add(items, new ItemInfo { Id = "gem_opal", Name = "Uncut opal", ColorHex = "#F2E9FF", Rare = true, Value = 650 });
            Add(items, new ItemInfo { Id = "gem_ember", Name = "Ember gem", ColorHex = "#FF6A1A", Rare = true, Value = 2400, Description = "Warm to the touch, forever." });
            Add(items, new ItemInfo { Id = "seed_ghostwood", Name = "Ghostwood seed", ColorHex = "#BFEFFF", Rare = true, Value = 300, Description = "Glows faintly. Only sprouts at night." });

            return items;
        }

        static ItemInfo Weapon(string id, string name, string tier, int level, int attack, int strength)
        {
            return new ItemInfo
            {
                Id = id, Name = name, Tier = tier, ColorHex = Tier(tier).ColorHex,
                Slot = EquipSlot.Weapon, AttackBonus = attack, StrengthBonus = strength,
                RequiredSkill = Skill.Attack, RequiredLevel = level
            };
        }

        static ItemInfo Armor(string id, string name, string tier, Skill skill, int level, int defense)
        {
            return new ItemInfo
            {
                Id = id, Name = name, Tier = tier, ColorHex = Tier(tier).ColorHex,
                Slot = EquipSlot.Body, DefenseBonus = defense,
                RequiredSkill = skill, RequiredLevel = level
            };
        }

        static ItemInfo Piece(string id, string name, string tier, EquipSlot slot, int level, SmithTier smith, double share)
        {
            ItemInfo info = Armor(id, name, tier, Skill.Defense, level, (int)Math.Round(smith.PlateDefense * share));
            info.Slot = slot;
            info.Value = (int)Math.Round(smith.PlateValue * share / 10.0) * 10;
            return info;
        }

        static void Add(Dictionary<string, ItemInfo> items, ItemInfo info)
        {
            items[info.Id] = info;
        }

        static Dictionary<string, EnemyInfo> BuildEnemies()
        {
            Dictionary<string, EnemyInfo> e = new Dictionary<string, EnemyInfo>();

            // Brookhollow Meadow (levels 1-10): nothing here attacks first
            e["thistlehog"] = new EnemyInfo { Id = "thistlehog", Name = "Thistlehog", Level = 2, Attack = 1, Strength = 1, Defense = 1, MaxHp = 25, RespawnSeconds = 15f, TintHex = "#B08A5A", DropTableId = "thistlehog" };
            e["gnarl"] = new EnemyInfo { Id = "gnarl", Name = "Gnarl", Level = 5, Attack = 3, Strength = 3, Defense = 3, MaxHp = 40, RespawnSeconds = 20f, TintHex = "#7FA35A", DropTableId = "gnarl" };
            e["boar"] = new EnemyInfo { Id = "boar", Name = "Tusker Boar", Level = 8, Attack = 6, Strength = 7, Defense = 5, MaxHp = 55, RespawnSeconds = 20f, TintHex = "#6A4A3A", DropTableId = "boar" };

            // Greyrock Hills (levels 10-25)
            e["grey_wolf"] = new EnemyInfo { Id = "grey_wolf", Name = "Grey Wolf", Level = 12, Attack = 9, Strength = 10, Defense = 8, MaxHp = 60, TintHex = "#8C9099", DropTableId = "grey_wolf", Aggressive = true };
            e["bandit"] = new EnemyInfo { Id = "bandit", Name = "Bandit", Level = 18, Attack = 15, Strength = 14, Defense = 12, AttackBonus = 8, StrengthBonus = 6, DefenseBonus = 8, MaxHp = 90, TintHex = "#A0524D", DropTableId = "bandit", Aggressive = true };
            e["stone_golem"] = new EnemyInfo { Id = "stone_golem", Name = "Stone Golem", Level = 24, Attack = 18, Strength = 22, Defense = 26, DefenseBonus = 20, MaxHp = 140, RespawnSeconds = 40f, TintHex = "#8A8578", DropTableId = "stone_golem" };

            // Mirefen Swamp (levels 15-30) and its boss
            e["bog_slime"] = new EnemyInfo { Id = "bog_slime", Name = "Bog Slime", Level = 16, Attack = 12, Strength = 13, Defense = 12, MaxHp = 75, TintHex = "#6E7A3A", DropTableId = "bog_slime", Aggressive = true };
            e[BossId] = new EnemyInfo { Id = BossId, Name = "Old Gullet", Level = 30, Attack = 24, Strength = 22, Defense = 20, DefenseBonus = 15, MaxHp = 320, RespawnSeconds = 90f, TintHex = "#3F6B5A", DropTableId = BossId, Scale = 1.7f, IsBoss = true, Aggressive = true };

            // Emberfall Ashlands (levels 30-50)
            e["cinder_imp"] = new EnemyInfo { Id = "cinder_imp", Name = "Cinder Imp", Level = 32, Attack = 28, Strength = 26, Defense = 24, AttackBonus = 10, StrengthBonus = 10, DefenseBonus = 10, MaxHp = 120, TintHex = "#A0302A", DropTableId = "cinder_imp", Aggressive = true };
            e["ash_wolf"] = new EnemyInfo { Id = "ash_wolf", Name = "Ash Wolf", Level = 38, Attack = 34, Strength = 36, Defense = 30, AttackBonus = 12, StrengthBonus = 12, DefenseBonus = 12, MaxHp = 160, TintHex = "#3A3432", DropTableId = "ash_wolf", Aggressive = true };
            e["magma_golem"] = new EnemyInfo { Id = "magma_golem", Name = "Magma Golem", Level = 48, Attack = 42, Strength = 46, Defense = 50, AttackBonus = 15, StrengthBonus = 20, DefenseBonus = 30, MaxHp = 260, RespawnSeconds = 60f, TintHex = "#3A2A26", DropTableId = "magma_golem", Aggressive = true };
            return e;
        }

        static Dictionary<string, DropTable> BuildDropTables()
        {
            Dictionary<string, DropTable> tables = new Dictionary<string, DropTable>();

            DropTable hog = new DropTable { Id = "thistlehog" };
            hog.Always.Add(new DropEntry("bones", 1, 1, 1));
            hog.Main.Add(new DropEntry("raw_meat", 1, 1, 45));
            hog.Main.Add(new DropEntry("hide", 1, 1, 25));
            hog.Main.Add(new DropEntry("coins", 1, 3, 10));
            hog.Main.Add(new DropEntry(null, 0, 0, 20));
            hog.Rares.Add(new RareDrop("thistle_quill", 1, 200));
            tables[hog.Id] = hog;

            DropTable gnarl = new DropTable { Id = "gnarl" };
            gnarl.Always.Add(new DropEntry("bones", 1, 1, 1));
            gnarl.Main.Add(new DropEntry("coins", 2, 8, 40));
            gnarl.Main.Add(new DropEntry("logs_pine", 1, 1, 25));
            gnarl.Main.Add(new DropEntry("logs_oak", 1, 1, 8));
            gnarl.Main.Add(new DropEntry("spark_stone", 2, 6, 15));
            gnarl.Main.Add(new DropEntry(null, 0, 0, 12));
            gnarl.Rares.Add(new RareDrop("gnarl_crown", 1, 1000));
            tables[gnarl.Id] = gnarl;

            DropTable boar = new DropTable { Id = "boar" };
            boar.Always.Add(new DropEntry("bones", 1, 1, 1));
            boar.Always.Add(new DropEntry("raw_meat", 1, 1, 1));
            boar.Main.Add(new DropEntry("hide", 1, 1, 40));
            boar.Main.Add(new DropEntry("raw_meat", 1, 1, 20));
            boar.Main.Add(new DropEntry(null, 0, 0, 40));
            tables[boar.Id] = boar;

            DropTable wolf = new DropTable { Id = "grey_wolf" };
            wolf.Always.Add(new DropEntry("bones", 1, 1, 1));
            wolf.Always.Add(new DropEntry("wolfhide", 1, 1, 1));
            wolf.Main.Add(new DropEntry("raw_meat", 1, 1, 60));
            wolf.Main.Add(new DropEntry(null, 0, 0, 40));
            wolf.Rares.Add(new RareDrop("alpha_fang", 1, 800));
            tables[wolf.Id] = wolf;

            DropTable bandit = new DropTable { Id = "bandit" };
            bandit.Always.Add(new DropEntry("bones", 1, 1, 1));
            bandit.Main.Add(new DropEntry("coins", 6, 24, 45));
            bandit.Main.Add(new DropEntry("bread", 1, 1, 12));
            bandit.Main.Add(new DropEntry("ore_iron", 1, 1, 12));
            bandit.Main.Add(new DropEntry("sword_iron", 1, 1, 3));
            bandit.Main.Add(new DropEntry("platebody_iron", 1, 1, 1));
            bandit.Main.Add(new DropEntry(null, 0, 0, 27));
            bandit.Rares.Add(new RareDrop("tattered_map", 1, 150));
            bandit.Rares.Add(new RareDrop("sword_steel", 1, 256));
            tables[bandit.Id] = bandit;

            DropTable golem = new DropTable { Id = "stone_golem" };
            golem.Main.Add(new DropEntry("coins", 3, 12, 30));
            golem.Main.Add(new DropEntry("ore_iron", 1, 2, 25));
            golem.Main.Add(new DropEntry("ore_coal", 1, 3, 25));
            golem.Main.Add(new DropEntry("spark_stone", 4, 10, 15));
            golem.Main.Add(new DropEntry(null, 0, 0, 5));
            golem.Rares.Add(new RareDrop("gem_jade", 1, 300));
            tables[golem.Id] = golem;

            DropTable slime = new DropTable { Id = "bog_slime" };
            slime.Main.Add(new DropEntry("coins", 2, 10, 35));
            slime.Main.Add(new DropEntry("spark_stone", 3, 8, 25));
            slime.Main.Add(new DropEntry("logs_willow", 1, 1, 15));
            slime.Main.Add(new DropEntry(null, 0, 0, 25));
            slime.Rares.Add(new RareDrop("seed_ghostwood", 1, 400));
            tables[slime.Id] = slime;

            DropTable gullet = new DropTable { Id = BossId, LuckyTableChance = 1.0 / 8.0 };
            gullet.Always.Add(new DropEntry("coins", 60, 150, 1));
            gullet.Always.Add(new DropEntry("bones", 1, 1, 1));
            gullet.Main.Add(new DropEntry("ore_iron", 3, 6, 25));
            gullet.Main.Add(new DropEntry("ore_coal", 3, 6, 20));
            gullet.Main.Add(new DropEntry("spark_stone", 20, 40, 25));
            gullet.Main.Add(new DropEntry("pine_arrows", 30, 60, 20));
            gullet.Main.Add(new DropEntry("platebody_iron", 1, 1, 10));
            gullet.Rares.Add(new RareDrop("gullet_hook", 1, 20));
            gullet.Rares.Add(new RareDrop("pet_gullet", 1, 100));
            tables[gullet.Id] = gullet;

            DropTable imp = new DropTable { Id = "cinder_imp" };
            imp.Always.Add(new DropEntry("bones", 1, 1, 1));
            imp.Main.Add(new DropEntry("coins", 8, 28, 45));
            imp.Main.Add(new DropEntry("spark_stone", 5, 15, 25));
            imp.Main.Add(new DropEntry("ore_coal", 1, 2, 15));
            imp.Main.Add(new DropEntry("meat_pie", 1, 1, 5));
            imp.Main.Add(new DropEntry(null, 0, 0, 10));
            imp.Rares.Add(new RareDrop("gem_ember", 1, 500));
            tables[imp.Id] = imp;

            DropTable ashWolf = new DropTable { Id = "ash_wolf" };
            ashWolf.Always.Add(new DropEntry("bones", 1, 1, 1));
            ashWolf.Always.Add(new DropEntry("wolfhide", 1, 1, 1));
            ashWolf.Main.Add(new DropEntry("raw_prime_meat", 1, 1, 60));
            ashWolf.Main.Add(new DropEntry("raw_meat", 1, 2, 25));
            ashWolf.Main.Add(new DropEntry(null, 0, 0, 15));
            ashWolf.Rares.Add(new RareDrop("alpha_fang", 1, 300));
            tables[ashWolf.Id] = ashWolf;

            DropTable magma = new DropTable { Id = "magma_golem" };
            magma.Main.Add(new DropEntry("coins", 12, 40, 40));
            magma.Main.Add(new DropEntry("ore_coal", 2, 5, 25));
            magma.Main.Add(new DropEntry("ore_cobalt", 1, 1, 12));
            magma.Main.Add(new DropEntry("ore_cinderite", 1, 1, 6));
            magma.Main.Add(new DropEntry(null, 0, 0, 17));
            magma.Rares.Add(new RareDrop("gem_ember", 1, 150));
            magma.Rares.Add(new RareDrop("sword_cobalt", 1, 400));
            tables[magma.Id] = magma;

            DropTable lucky = new DropTable { Id = LuckyTableId, LuckyTableChance = 0 };
            lucky.Main.Add(new DropEntry("gem_jade", 1, 1, 40));
            lucky.Main.Add(new DropEntry("gem_opal", 1, 1, 30));
            lucky.Main.Add(new DropEntry("tattered_map", 1, 1, 20));
            lucky.Main.Add(new DropEntry("seed_ghostwood", 1, 1, 10));
            tables[lucky.Id] = lucky;

            return tables;
        }

        static Dictionary<string, NodeInfo> BuildNodes()
        {
            Dictionary<string, NodeInfo> n = new Dictionary<string, NodeInfo>();
            n["pine"] = Tree("pine", "Pine tree", 1, 25, "logs_pine", 3, 8f, "#2C5E4A", "#6B4A2B");
            n["oak"] = Tree("oak", "Oak tree", 15, 38, "logs_oak", 5, 15f, "#C9782C", "#5A3D22");
            n["willow"] = Tree("willow", "Willow tree", 30, 68, "logs_willow", 7, 25f, "#A8C85A", "#6B5A3A");
            n["maple"] = Tree("maple", "Maple tree", 45, 100, "logs_maple", 9, 40f, "#C0392B", "#4A3020");
            n["yew"] = Tree("yew", "Yew tree", 60, 175, "logs_yew", 12, 70f, "#1F4A2E", "#3A2818");
            n["ashwood"] = Tree("ashwood", "Ashwood tree", 75, 250, "logs_ashwood", 15, 110f, "#FF8A2A", "#2A2420");

            n["copper"] = Rock("copper", "Copper rock", 1, 18, "ore_copper", 3, 5f, "#7A6A5E", "#E07A3A");
            n["copper"].BonusItemId = "spark_stone";
            n["copper"].BonusChance = 0.12;
            n["tin"] = Rock("tin", "Tin rock", 1, 18, "ore_tin", 3, 5f, "#8A9096", "#E8F0F8");
            n["tin"].BonusItemId = "spark_stone";
            n["tin"].BonusChance = 0.12;
            n["iron"] = Rock("iron", "Iron rock", 15, 35, "ore_iron", 5, 10f, "#55504C", "#B5553A");
            n["coal"] = Rock("coal", "Coal rock", 30, 50, "ore_coal", 7, 25f, "#4A4642", "#18181C");
            n["verdite"] = Rock("verdite", "Verdite rock", 40, 80, "ore_verdite", 9, 45f, "#4F5A55", "#3FE0A8");
            n["cobalt"] = Rock("cobalt", "Cobalt rock", 55, 105, "ore_cobalt", 12, 75f, "#3F4450", "#3A6BFF");
            n["cinderite"] = Rock("cinderite", "Cinderite rock", 70, 140, "ore_cinderite", 15, 120f, "#2E2522", "#FF6A1A");
            return n;
        }

        // Every tree and rock gives a single log/ore, then respawns; higher tiers take longer to come back.
        static NodeInfo Tree(string id, string name, int level, double xp, string item, double hardness, float respawn, string color, string accent)
        {
            return new NodeInfo { Id = id, Name = name, Kind = NodeKind.Tree, Skill = Skill.Woodcutting, Level = level, Xp = xp, ItemId = item, Hardness = hardness, RespawnSeconds = respawn, ColorHex = color, AccentHex = accent };
        }

        static NodeInfo Rock(string id, string name, int level, double xp, string item, double hardness, float respawn, string color, string accent)
        {
            return new NodeInfo { Id = id, Name = name, Kind = NodeKind.Rock, Skill = Skill.Mining, Level = level, Xp = xp, ItemId = item, Hardness = hardness, RespawnSeconds = respawn, ColorHex = color, AccentHex = accent };
        }

        /// <summary>
        /// Work done by one swing: the tool's power, +5% per level above the node's requirement.
        /// A bronze tool at the exact level needs Hardness swings per log/ore.
        /// </summary>
        public static double SwingPower(int playerLevel, int requiredLevel, double toolPower)
        {
            double levelFactor = 1.0 + Math.Max(0, playerLevel - requiredLevel) * 0.05;
            return Math.Max(0.1, toolPower) * levelFactor;
        }

        /// <summary>Swings needed per log/ore ignoring the ±25% swing variance (for tooltips and tests).</summary>
        public static int SwingsNeeded(NodeInfo node, int playerLevel, double toolPower)
        {
            return Math.Max(1, (int)Math.Ceiling(node.Hardness / SwingPower(playerLevel, node.Level, toolPower) - 1e-9));
        }

        /// <summary>Chance per attempt to light a fire: 50% at the requirement, up to 95%.</summary>
        public static double LightChance(int playerLevel, int requiredLevel)
        {
            double chance = 0.5 + (playerLevel - requiredLevel) * 0.025;
            return chance < 0.5 ? 0.5 : chance > 0.95 ? 0.95 : chance;
        }

        static Dictionary<string, BurnInfo> BuildBurnables()
        {
            Dictionary<string, BurnInfo> b = new Dictionary<string, BurnInfo>();
            b["logs_pine"] = new BurnInfo { LogId = "logs_pine", Level = 1, Xp = 40, BurnSeconds = 60f };
            b["logs_oak"] = new BurnInfo { LogId = "logs_oak", Level = 15, Xp = 60, BurnSeconds = 80f };
            b["logs_willow"] = new BurnInfo { LogId = "logs_willow", Level = 30, Xp = 90, BurnSeconds = 90f };
            b["logs_maple"] = new BurnInfo { LogId = "logs_maple", Level = 45, Xp = 135, BurnSeconds = 100f };
            b["logs_yew"] = new BurnInfo { LogId = "logs_yew", Level = 60, Xp = 202, BurnSeconds = 120f };
            b["logs_ashwood"] = new BurnInfo { LogId = "logs_ashwood", Level = 75, Xp = 304, BurnSeconds = 150f };
            return b;
        }

        static List<Recipe> BuildRecipes()
        {
            List<Recipe> recipes = new List<Recipe>();

            // Furnace: smelting
            foreach (SmithTier tier in SmithTiers)
            {
                string key = tier.Tier.ToLower();
                Recipe smelt = MakeRecipe("smelt_" + key, "Smelt " + tier.Tier + " bar", Skill.Smithing, tier.SmeltLevel, tier.SmeltXp, "bar_" + key, 1, tier.OreId, 1);
                if (tier.Tier == "Bronze") smelt.Inputs.Add(new ItemStack("ore_tin", 1));
                if (tier.CoalPerBar > 0) smelt.Inputs.Add(new ItemStack("ore_coal", tier.CoalPerBar));
                smelt.Station = StationKind.Furnace;
                recipes.Add(smelt);
            }

            // Anvil: smithing (needs a hammer)
            foreach (SmithTier tier in SmithTiers)
            {
                string t = tier.Tier;
                string key = t.ToLower();
                recipes.Add(Anvil("forge_axe_" + key, "Smith " + t + " axe", tier, AxeOffset, AxeBars, "axe_" + key));
                recipes.Add(Anvil("forge_sword_" + key, "Smith " + t + " sword", tier, SwordOffset, SwordBars, "sword_" + key));
                recipes.Add(Anvil("forge_helm_" + key, "Smith " + t + " full helm", tier, HelmOffset, HelmBars, "helm_" + key));
                recipes.Add(Anvil("forge_pickaxe_" + key, "Smith " + t + " pickaxe", tier, PickaxeOffset, PickaxeBars, "pickaxe_" + key));
                recipes.Add(Anvil("forge_shield_" + key, "Smith " + t + " kiteshield", tier, ShieldOffset, ShieldBars, "kiteshield_" + key));
                recipes.Add(Anvil("forge_legs_" + key, "Smith " + t + " platelegs", tier, LegsOffset, LegsBars, "platelegs_" + key));
                recipes.Add(Anvil("forge_plate_" + key, "Smith " + t + " platebody", tier, PlateOffset, PlateBars, "platebody_" + key));
            }

            // Fire: cooking (food can burn)
            Recipe meat = MakeRecipe("cook_meat", "Cook meat", Skill.Cooking, 1, 30, "cooked_meat", 1, "raw_meat", 1);
            meat.Station = StationKind.Fire;
            meat.BurntItemId = "burnt_meat";
            recipes.Add(meat);
            Recipe prime = MakeRecipe("cook_prime", "Cook prime steak", Skill.Cooking, 30, 80, "prime_steak", 1, "raw_prime_meat", 1);
            prime.Station = StationKind.Fire;
            prime.BurntItemId = "burnt_meat";
            recipes.Add(prime);

            // Crafting: anywhere
            recipes.Add(MakeRecipe("craft_arrows_pine", "Craft 15 Pine arrows", Skill.Crafting, 1, 5, "pine_arrows", 15, "logs_pine", 1));
            recipes.Add(MakeRecipe("craft_staff_pine", "Craft Pine staff", Skill.Crafting, 3, 10, "staff_pine", 1, "logs_pine", 2, "spark_stone", 1));
            recipes.Add(MakeRecipe("craft_staff_oak", "Craft Oak staff", Skill.Crafting, 12, 25, "staff_oak", 1, "logs_oak", 2, "spark_stone", 5));
            recipes.Add(MakeRecipe("craft_shield_wood", "Craft Wooden shield", Skill.Crafting, 1, 12, "shield_wood", 1, "logs_pine", 3));
            recipes.Add(MakeRecipe("craft_leather_cowl", "Craft Leather cowl", Skill.Crafting, 1, 10, "leather_cowl", 1, "hide", 1));
            recipes.Add(MakeRecipe("craft_leather_chaps", "Craft Leather chaps", Skill.Crafting, 4, 14, "leather_chaps", 1, "hide", 1));
            recipes.Add(MakeRecipe("craft_bow_pine", "Craft Pine shortbow", Skill.Crafting, 1, 5, "shortbow_pine", 1, "logs_pine", 1));
            recipes.Add(MakeRecipe("craft_leather_body", "Craft Leather body", Skill.Crafting, 1, 14, "leather_body", 1, "hide", 1));
            recipes.Add(MakeRecipe("craft_bow_oak", "Craft Oak shortbow", Skill.Crafting, 10, 17, "shortbow_oak", 1, "logs_oak", 1));
            recipes.Add(MakeRecipe("craft_hard_leather", "Craft Hardened leather body", Skill.Crafting, 10, 35, "hard_leather_body", 1, "hide", 2));
            recipes.Add(MakeRecipe("craft_wolfhide_body", "Craft Wolfhide body", Skill.Crafting, 20, 60, "wolfhide_body", 1, "wolfhide", 3));

            return recipes;
        }

        static Recipe Anvil(string id, string name, SmithTier tier, int levelOffset, int bars, string outputId)
        {
            Recipe recipe = MakeRecipe(id, name, Skill.Smithing, tier.SmeltLevel + levelOffset, tier.XpPerBar * bars, outputId, 1, "bar_" + tier.Tier.ToLower(), bars);
            recipe.Station = StationKind.Anvil;
            recipe.ToolId = HammerId;
            return recipe;
        }

        static Recipe MakeRecipe(string id, string name, Skill skill, int level, double xp, string outputId, int outputQty, params object[] inputs)
        {
            Recipe recipe = new Recipe { Id = id, Name = name, Skill = skill, Level = level, Xp = xp, Output = new ItemStack(outputId, outputQty) };
            for (int i = 0; i + 1 < inputs.Length; i += 2)
            {
                recipe.Inputs.Add(new ItemStack((string)inputs[i], (int)inputs[i + 1]));
            }
            return recipe;
        }

        static Dictionary<string, ShopInfo> BuildShops()
        {
            Dictionary<string, ShopInfo> shops = new Dictionary<string, ShopInfo>();
            shops["brookhollow"] = Shop("brookhollow", "Brookhollow General Store",
                "cheese", "bread", FlintId, HammerId, "axe_bronze", "pickaxe_bronze", "pine_arrows", "spark_stone",
                "shortbow_pine", "staff_pine", "sword_bronze", "shield_wood", "leather_cowl", "leather_body", "leather_chaps",
                "helm_bronze", "kiteshield_bronze", "platelegs_bronze", "platebody_bronze");
            shops["ironmere"] = Shop("ironmere", "Ironmere Supplies",
                "cheese", "bread", FlintId, HammerId, "axe_bronze", "pickaxe_bronze", "axe_iron", "pickaxe_iron", "pine_arrows", "spark_stone",
                "shortbow_oak", "sword_iron", "hard_leather_body", "helm_iron", "kiteshield_iron", "platelegs_iron", "platebody_iron");
            shops["emberfall"] = Shop("emberfall", "Emberfall Outfitters",
                "bread", "meat_pie", FlintId, HammerId, "axe_iron", "pickaxe_iron", "axe_steel", "pickaxe_steel", "pine_arrows", "spark_stone",
                "staff_oak", "sword_steel", "helm_steel", "kiteshield_steel", "platelegs_steel", "platebody_steel");
            return shops;
        }

        static ShopInfo Shop(string id, string name, params string[] stock)
        {
            ShopInfo shop = new ShopInfo { Id = id, Name = name };
            shop.Stock.AddRange(stock);
            return shop;
        }
    }
}
