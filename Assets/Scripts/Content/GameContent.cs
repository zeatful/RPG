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

        // Chance each successful gather depletes the node (rocks always deplete, trees sometimes).
        public double DepleteChance;
        public string ColorHex;
        public string AccentHex;

        // Optional extra item found alongside the main one (e.g. spark stones in copper rock).
        public string BonusItemId;
        public double BonusChance;
    }

    /// <summary>
    /// All game data in one place. Defined in code so the prototype runs with no editor setup;
    /// this can move to ScriptableObjects later. Pure C#.
    /// </summary>
    public static class GameContent
    {
        // Multiplies all combat XP. 1 = intended rate.
        public const double CombatXpRate = 1.0;

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

        public static readonly Dictionary<string, ItemInfo> Items = BuildItems();
        public static readonly Dictionary<string, EnemyInfo> Enemies = BuildEnemies();
        public static readonly Dictionary<string, DropTable> DropTables = BuildDropTables();
        public static readonly Dictionary<string, NodeInfo> Nodes = BuildNodes();
        public static readonly List<Recipe> Recipes = BuildRecipes();

        public const string LuckyTableId = "lucky";

        // Order enemies are handed out to the enemies placed in the scene.
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

        static Dictionary<string, ItemInfo> BuildItems()
        {
            Dictionary<string, ItemInfo> items = new Dictionary<string, ItemInfo>();

            // Resources
            Add(items, new ItemInfo { Id = "coins", Name = "Coins", ColorHex = "#E8C547", Stackable = true });
            Add(items, new ItemInfo { Id = "bones", Name = "Bones", ColorHex = "#EDE6D6" });
            Add(items, new ItemInfo { Id = "raw_meat", Name = "Raw meat", ColorHex = "#C0504D", HealAmount = 20, Description = "Edible, barely. Heals 20." });
            Add(items, new ItemInfo { Id = "hide", Name = "Hide", ColorHex = "#9C6B3F" });
            Add(items, new ItemInfo { Id = "wolfhide", Name = "Wolfhide", ColorHex = "#7D7F86" });
            Add(items, new ItemInfo { Id = "logs_pine", Name = "Pine logs", ColorHex = "#A0703A" });
            Add(items, new ItemInfo { Id = "logs_oak", Name = "Oak logs", ColorHex = "#7A5230" });
            Add(items, new ItemInfo { Id = "logs_willow", Name = "Willow logs", ColorHex = "#8FA35A" });
            Add(items, new ItemInfo { Id = "ore_copper", Name = "Copper ore", ColorHex = "#C8733A" });
            Add(items, new ItemInfo { Id = "ore_tin", Name = "Tin ore", ColorHex = "#B9BEC4" });
            Add(items, new ItemInfo { Id = "ore_iron", Name = "Iron ore", ColorHex = "#6E4A3A" });
            Add(items, new ItemInfo { Id = "bar_bronze", Name = "Bronze bar", ColorHex = "#B87333", Tier = "Bronze" });
            Add(items, new ItemInfo { Id = "bar_iron", Name = "Iron bar", ColorHex = "#8A8D91", Tier = "Iron" });

            // Weapons
            Add(items, Weapon("sword_bronze", "Bronze sword", "Bronze", 1, 6, 5));
            Add(items, Weapon("sword_iron", "Iron sword", "Iron", 10, 12, 10));
            Add(items, Weapon("sword_steel", "Steel sword", "Steel", 20, 18, 16));
            Add(items, new ItemInfo { Id = "alpha_fang", Name = "Alpha Fang", ColorHex = "#E6E6E6", Slot = EquipSlot.Weapon, AttackBonus = 15, StrengthBonus = 12, RequiredSkill = Skill.Attack, RequiredLevel = 10, Rare = true, Description = "A wolf's fang, sharpened into a dagger." });
            Add(items, new ItemInfo { Id = "gullet_hook", Name = "Gullet Hook", ColorHex = "#5F8A7A", Slot = EquipSlot.Weapon, AttackBonus = 16, StrengthBonus = 18, RequiredSkill = Skill.Attack, RequiredLevel = 15, Rare = true, Description = "Pulled from Old Gullet's jaw. Still smells of river." });

            // Ranged: bows fire arrows (one per shot)
            Add(items, new ItemInfo { Id = "pine_arrows", Name = "Pine arrows", ColorHex = "#C9A06A", Stackable = true, Description = "Ammo for bows." });
            Add(items, new ItemInfo { Id = "shortbow_pine", Name = "Pine shortbow", ColorHex = "#A0703A", Slot = EquipSlot.Weapon, Style = CombatStyle.Ranged, AmmoId = "pine_arrows", Range = 7f, AttackBonus = 8, StrengthBonus = 6, RequiredSkill = Skill.Ranged, RequiredLevel = 1 });
            Add(items, new ItemInfo { Id = "shortbow_oak", Name = "Oak shortbow", ColorHex = "#7A5230", Slot = EquipSlot.Weapon, Style = CombatStyle.Ranged, AmmoId = "pine_arrows", Range = 8f, AttackBonus = 14, StrengthBonus = 10, RequiredSkill = Skill.Ranged, RequiredLevel = 10 });

            // Magic: wands burn spark stones (one per cast)
            Add(items, new ItemInfo { Id = "spark_stone", Name = "Spark stone", ColorHex = "#FFB347", Stackable = true, Description = "Crackles when held. Wands burn one per cast." });
            Add(items, new ItemInfo { Id = "wand_pine", Name = "Pine wand", ColorHex = "#E0A060", Slot = EquipSlot.Weapon, Style = CombatStyle.Magic, AmmoId = "spark_stone", Range = 6f, AttackBonus = 9, StrengthBonus = 8, RequiredSkill = Skill.Magic, RequiredLevel = 1 });
            Add(items, new ItemInfo { Id = "wand_oak", Name = "Oak wand", ColorHex = "#C07A3A", Slot = EquipSlot.Weapon, Style = CombatStyle.Magic, AmmoId = "spark_stone", Range = 7f, AttackBonus = 15, StrengthBonus = 13, RequiredSkill = Skill.Magic, RequiredLevel = 10 });

            // Pets
            Add(items, new ItemInfo { Id = "pet_gullet", Name = "Little Gullet", ColorHex = "#4A6B5A", Rare = true, IsPet = true, Description = "A tiny river brute. Left-click to summon or dismiss." });

            // Armor
            Add(items, Armor("platebody_bronze", "Bronze platebody", "Bronze", Skill.Defense, 1, 15));
            Add(items, Armor("platebody_iron", "Iron platebody", "Iron", Skill.Defense, 10, 25));
            Add(items, new ItemInfo { Id = "leather_body", Name = "Leather body", ColorHex = "#9C6B3F", Slot = EquipSlot.Body, DefenseBonus = 8, RequiredSkill = Skill.Defense, RequiredLevel = 1 });
            Add(items, new ItemInfo { Id = "hard_leather_body", Name = "Hardened leather body", ColorHex = "#6F4A2A", Slot = EquipSlot.Body, DefenseBonus = 14, RequiredSkill = Skill.Defense, RequiredLevel = 10 });
            Add(items, new ItemInfo { Id = "wolfhide_body", Name = "Wolfhide body", ColorHex = "#7D7F86", Slot = EquipSlot.Body, DefenseBonus = 20, AttackBonus = 2, RequiredSkill = Skill.Defense, RequiredLevel = 20 });

            // Rares and Lucky Table
            Add(items, new ItemInfo { Id = "thistle_quill", Name = "Thistle Quill", ColorHex = "#D8C27A", Rare = true, Description = "Needle-sharp. Will fletch 10 arrows once ranged combat arrives." });
            Add(items, new ItemInfo { Id = "gnarl_crown", Name = "Gnarl Chief's Crown", ColorHex = "#5E8C31", Rare = true, Description = "Cosmetic. Gnarls would be furious." });
            Add(items, new ItemInfo { Id = "tattered_map", Name = "Tattered Map", ColorHex = "#D9C79E", Rare = true, Description = "A sketch of somewhere nearby. Treasure hunts are coming later." });
            Add(items, new ItemInfo { Id = "gem_jade", Name = "Uncut jade", ColorHex = "#3FA55A", Rare = true });
            Add(items, new ItemInfo { Id = "gem_opal", Name = "Uncut opal", ColorHex = "#F2E9FF", Rare = true });
            Add(items, new ItemInfo { Id = "seed_ghostwood", Name = "Ghostwood seed", ColorHex = "#BFEFFF", Rare = true, Description = "Glows faintly. Only sprouts at night." });

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

        static void Add(Dictionary<string, ItemInfo> items, ItemInfo info)
        {
            items[info.Id] = info;
        }

        static Dictionary<string, EnemyInfo> BuildEnemies()
        {
            Dictionary<string, EnemyInfo> enemies = new Dictionary<string, EnemyInfo>();
            enemies["thistlehog"] = new EnemyInfo { Id = "thistlehog", Name = "Thistlehog", Level = 2, Attack = 1, Strength = 1, Defense = 1, MaxHp = 25, RespawnSeconds = 15f, TintHex = "#B08A5A", DropTableId = "thistlehog" };
            enemies["gnarl"] = new EnemyInfo { Id = "gnarl", Name = "Gnarl", Level = 5, Attack = 3, Strength = 3, Defense = 3, MaxHp = 40, TintHex = "#7FA35A", DropTableId = "gnarl" };
            enemies["grey_wolf"] = new EnemyInfo { Id = "grey_wolf", Name = "Grey Wolf", Level = 12, Attack = 9, Strength = 10, Defense = 8, MaxHp = 60, TintHex = "#8C9099", DropTableId = "grey_wolf" };
            enemies[BossId] = new EnemyInfo { Id = BossId, Name = "Old Gullet", Level = 20, Attack = 16, Strength = 15, Defense = 14, DefenseBonus = 10, MaxHp = 250, RespawnSeconds = 90f, TintHex = "#3F6B5A", DropTableId = BossId, Scale = 1.7f, IsBoss = true };
            enemies["bandit"] = new EnemyInfo { Id = "bandit", Name = "Bandit", Level = 18, Attack = 15, Strength = 14, Defense = 12, AttackBonus = 8, StrengthBonus = 6, DefenseBonus = 8, MaxHp = 90, TintHex = "#A0524D", DropTableId = "bandit" };
            return enemies;
        }

        static Dictionary<string, DropTable> BuildDropTables()
        {
            Dictionary<string, DropTable> tables = new Dictionary<string, DropTable>();

            DropTable hog = new DropTable { Id = "thistlehog" };
            hog.Always.Add(new DropEntry("bones", 1, 1, 1));
            hog.Main.Add(new DropEntry("raw_meat", 1, 1, 50));
            hog.Main.Add(new DropEntry("hide", 1, 1, 30));
            hog.Main.Add(new DropEntry(null, 0, 0, 20));
            hog.Rares.Add(new RareDrop("thistle_quill", 1, 200));
            tables[hog.Id] = hog;

            DropTable gnarl = new DropTable { Id = "gnarl" };
            gnarl.Always.Add(new DropEntry("bones", 1, 1, 1));
            gnarl.Main.Add(new DropEntry("coins", 5, 25, 40));
            gnarl.Main.Add(new DropEntry("logs_pine", 1, 1, 25));
            gnarl.Main.Add(new DropEntry("hide", 1, 1, 15));
            gnarl.Main.Add(new DropEntry("sword_bronze", 1, 1, 8));
            gnarl.Main.Add(new DropEntry("spark_stone", 3, 8, 10));
            gnarl.Main.Add(new DropEntry(null, 0, 0, 2));
            gnarl.Rares.Add(new RareDrop("gnarl_crown", 1, 1000));
            tables[gnarl.Id] = gnarl;

            DropTable wolf = new DropTable { Id = "grey_wolf" };
            wolf.Always.Add(new DropEntry("bones", 1, 1, 1));
            wolf.Always.Add(new DropEntry("wolfhide", 1, 1, 1));
            wolf.Main.Add(new DropEntry("raw_meat", 1, 1, 60));
            wolf.Main.Add(new DropEntry(null, 0, 0, 40));
            wolf.Rares.Add(new RareDrop("alpha_fang", 1, 800));
            tables[wolf.Id] = wolf;

            DropTable bandit = new DropTable { Id = "bandit" };
            bandit.Always.Add(new DropEntry("bones", 1, 1, 1));
            bandit.Main.Add(new DropEntry("coins", 20, 80, 45));
            bandit.Main.Add(new DropEntry("ore_iron", 1, 1, 20));
            bandit.Main.Add(new DropEntry("sword_iron", 1, 1, 8));
            bandit.Main.Add(new DropEntry("platebody_iron", 1, 1, 4));
            bandit.Main.Add(new DropEntry(null, 0, 0, 23));
            bandit.Rares.Add(new RareDrop("tattered_map", 1, 150));
            bandit.Rares.Add(new RareDrop("sword_steel", 1, 128));
            tables[bandit.Id] = bandit;

            DropTable gullet = new DropTable { Id = BossId, LuckyTableChance = 1.0 / 8.0 };
            gullet.Always.Add(new DropEntry("coins", 80, 200, 1));
            gullet.Always.Add(new DropEntry("bones", 1, 1, 1));
            gullet.Main.Add(new DropEntry("ore_iron", 3, 6, 30));
            gullet.Main.Add(new DropEntry("spark_stone", 20, 40, 30));
            gullet.Main.Add(new DropEntry("pine_arrows", 30, 60, 25));
            gullet.Main.Add(new DropEntry("platebody_iron", 1, 1, 15));
            gullet.Rares.Add(new RareDrop("gullet_hook", 1, 20));
            gullet.Rares.Add(new RareDrop("pet_gullet", 1, 100));
            tables[gullet.Id] = gullet;

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
            Dictionary<string, NodeInfo> nodes = new Dictionary<string, NodeInfo>();
            nodes["pine"] = new NodeInfo { Id = "pine", Name = "Pine tree", Kind = NodeKind.Tree, Skill = Skill.Woodcutting, Level = 1, Xp = 25, ItemId = "logs_pine", RespawnSeconds = 10f, DepleteChance = 1.0, ColorHex = "#2C5E4A", AccentHex = "#6B4A2B" };
            nodes["oak"] = new NodeInfo { Id = "oak", Name = "Oak tree", Kind = NodeKind.Tree, Skill = Skill.Woodcutting, Level = 15, Xp = 38, ItemId = "logs_oak", RespawnSeconds = 15f, DepleteChance = 0.2, ColorHex = "#C9782C", AccentHex = "#5A3D22" };
            nodes["willow"] = new NodeInfo { Id = "willow", Name = "Willow tree", Kind = NodeKind.Tree, Skill = Skill.Woodcutting, Level = 30, Xp = 68, ItemId = "logs_willow", RespawnSeconds = 15f, DepleteChance = 0.15, ColorHex = "#A8C85A", AccentHex = "#6B5A3A" };
            nodes["copper"] = new NodeInfo { Id = "copper", Name = "Copper rock", Kind = NodeKind.Rock, Skill = Skill.Mining, Level = 1, Xp = 18, ItemId = "ore_copper", RespawnSeconds = 5f, DepleteChance = 1.0, ColorHex = "#7A6A5E", AccentHex = "#E07A3A", BonusItemId = "spark_stone", BonusChance = 0.15 };
            nodes["tin"] = new NodeInfo { Id = "tin", Name = "Tin rock", Kind = NodeKind.Rock, Skill = Skill.Mining, Level = 1, Xp = 18, ItemId = "ore_tin", RespawnSeconds = 5f, DepleteChance = 1.0, ColorHex = "#8A9096", AccentHex = "#E8F0F8", BonusItemId = "spark_stone", BonusChance = 0.15 };
            nodes["iron"] = new NodeInfo { Id = "iron", Name = "Iron rock", Kind = NodeKind.Rock, Skill = Skill.Mining, Level = 15, Xp = 35, ItemId = "ore_iron", RespawnSeconds = 8f, DepleteChance = 1.0, ColorHex = "#55504C", AccentHex = "#B5553A" };
            return nodes;
        }

        /// <summary>Chance per attempt (every 1.2s) to gather: better with levels above the requirement.</summary>
        public static double GatherChance(int playerLevel, int requiredLevel)
        {
            double chance = 0.30 + (playerLevel - requiredLevel) * 0.012;
            if (chance < 0.10) chance = 0.10;
            if (chance > 0.90) chance = 0.90;
            return chance;
        }

        static List<Recipe> BuildRecipes()
        {
            List<Recipe> recipes = new List<Recipe>();

            // Smithing
            recipes.Add(MakeRecipe("smelt_bronze", "Smelt Bronze bar", Skill.Smithing, 1, 6, "bar_bronze", 1, "ore_copper", 1, "ore_tin", 1));
            recipes.Add(MakeRecipe("smelt_iron", "Smelt Iron bar", Skill.Smithing, 15, 12, "bar_iron", 1, "ore_iron", 1));
            recipes.Add(MakeRecipe("forge_sword_bronze", "Forge Bronze sword", Skill.Smithing, 1, 12, "sword_bronze", 1, "bar_bronze", 1));
            recipes.Add(MakeRecipe("forge_plate_bronze", "Forge Bronze platebody", Skill.Smithing, 5, 37, "platebody_bronze", 1, "bar_bronze", 3));
            recipes.Add(MakeRecipe("forge_sword_iron", "Forge Iron sword", Skill.Smithing, 15, 25, "sword_iron", 1, "bar_iron", 1));
            recipes.Add(MakeRecipe("forge_plate_iron", "Forge Iron platebody", Skill.Smithing, 18, 75, "platebody_iron", 1, "bar_iron", 3));

            // Crafting
            recipes.Add(MakeRecipe("craft_arrows_pine", "Craft 15 Pine arrows", Skill.Crafting, 1, 5, "pine_arrows", 15, "logs_pine", 1));
            recipes.Add(MakeRecipe("craft_wand_pine", "Craft Pine wand", Skill.Crafting, 3, 10, "wand_pine", 1, "logs_pine", 1, "spark_stone", 1));
            recipes.Add(MakeRecipe("craft_wand_oak", "Craft Oak wand", Skill.Crafting, 12, 25, "wand_oak", 1, "logs_oak", 1, "spark_stone", 5));
            recipes.Add(MakeRecipe("craft_bow_pine", "Craft Pine shortbow", Skill.Crafting, 1, 5, "shortbow_pine", 1, "logs_pine", 1));
            recipes.Add(MakeRecipe("craft_leather_body", "Craft Leather body", Skill.Crafting, 1, 14, "leather_body", 1, "hide", 1));
            recipes.Add(MakeRecipe("craft_bow_oak", "Craft Oak shortbow", Skill.Crafting, 10, 17, "shortbow_oak", 1, "logs_oak", 1));
            recipes.Add(MakeRecipe("craft_hard_leather", "Craft Hardened leather body", Skill.Crafting, 10, 35, "hard_leather_body", 1, "hide", 2));
            recipes.Add(MakeRecipe("craft_wolfhide_body", "Craft Wolfhide body", Skill.Crafting, 20, 60, "wolfhide_body", 1, "wolfhide", 3));

            return recipes;
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
    }
}
