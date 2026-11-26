using System;
using System.Collections.Generic;
using RPG.Content;
using RPG.Drops;
using RPG.Items;
using RPG.Stats;
using UnityEditor;
using UnityEngine;

namespace RPG.EditorTools
{
    /// <summary>
    /// Logic tests that need no scene. Run from the menu (RPG > Run Self Tests) or headless:
    ///   Unity -batchmode -projectPath . -executeMethod RPG.EditorTools.RpgSelfTest.RunBatch -logFile -
    /// Exits with code 0 on success, 1 on failure in batch mode.
    /// </summary>
    public static class RpgSelfTest
    {
        static int passed;
        static readonly List<string> failures = new List<string>();

        [MenuItem("RPG/Run Self Tests")]
        public static void RunFromMenu()
        {
            bool ok = RunAll();
            EditorUtility.DisplayDialog("RPG Self Tests", ok ? "All " + passed + " checks passed." : failures.Count + " failed. See Console.", "OK");
        }

        public static void RunBatch()
        {
            bool ok = RunAll();
            EditorApplication.Exit(ok ? 0 : 1);
        }

        public static bool RunAll()
        {
            passed = 0;
            failures.Clear();

            Run("XP table", TestExperienceTable);
            Run("Combat math", TestCombatMath);
            Run("Inventory", TestInventory);
            Run("Equipment", TestEquipment);
            Run("Recipes", TestRecipes);
            Run("Drop roller", TestDropRoller);
            Run("Content integrity", TestContentIntegrity);

            foreach (string failure in failures) Debug.LogError("[RPG SelfTest] FAIL " + failure);
            Debug.Log("[RPG SelfTest] " + passed + " checks passed, " + failures.Count + " failed.");
            return failures.Count == 0;
        }

        static void Run(string name, Action test)
        {
            try { test(); }
            catch (Exception e) { failures.Add(name + ": threw " + e); }
        }

        static void Check(bool condition, string message)
        {
            if (condition) passed++;
            else failures.Add(message);
        }

        static void TestExperienceTable()
        {
            Check(ExperienceTable.XpForLevel(1) == 0, "level 1 should need 0 xp");
            Check(ExperienceTable.XpForLevel(2) == 83, "level 2 should need 83 xp, got " + ExperienceTable.XpForLevel(2));
            Check(ExperienceTable.XpForLevel(10) == 1154, "level 10 should need 1154 xp, got " + ExperienceTable.XpForLevel(10));
            Check(ExperienceTable.XpForLevel(92) == 6517253, "level 92 should need 6517253 xp, got " + ExperienceTable.XpForLevel(92));
            Check(ExperienceTable.XpForLevel(99) == 13034431, "level 99 should need 13034431 xp, got " + ExperienceTable.XpForLevel(99));
            Check(ExperienceTable.LevelForXp(0) == 1, "0 xp is level 1");
            Check(ExperienceTable.LevelForXp(82) == 1, "82 xp is level 1");
            Check(ExperienceTable.LevelForXp(83) == 2, "83 xp is level 2");
            Check(ExperienceTable.LevelForXp(200000000) == 99, "huge xp caps at 99");
            for (int level = 2; level <= 99; level++)
            {
                if (ExperienceTable.XpForLevel(level) <= ExperienceTable.XpForLevel(level - 1))
                {
                    Check(false, "xp table not increasing at level " + level);
                    return;
                }
            }
            Check(true, "xp table increasing");
            float progress = ExperienceTable.ProgressToNext(83 + (174 - 83) / 2.0);
            Check(progress > 0.45f && progress < 0.55f, "progress halfway between 2 and 3 should be ~0.5, got " + progress);
        }

        static void TestCombatMath()
        {
            float even = CombatMath.HitChance(1, 0, 1, 0);
            Check(even > 0.3f && even < 0.6f, "equal stats hit chance should be near 0.45, got " + even);
            Check(CombatMath.HitChance(99, 100, 1, 0) <= CombatMath.MaxHitChance, "hit chance capped");
            Check(CombatMath.HitChance(1, 0, 99, 100) >= CombatMath.MinHitChance, "hit chance floored");
            Check(CombatMath.HitChance(20, 10, 5, 0) > CombatMath.HitChance(5, 0, 20, 10), "stronger attacker hits more");
            Check(CombatMath.MaxHit(1, 0) >= 10, "level 1 max hit at least 10, got " + CombatMath.MaxHit(1, 0));
            Check(CombatMath.MaxHit(50, 20) > CombatMath.MaxHit(1, 0), "max hit grows with strength");
            Check(CombatMath.RollDamage(20, 0.0) == 1, "lowest roll is 1");
            Check(CombatMath.RollDamage(20, 0.99999) == 20, "highest roll is max hit");
            Check(CombatMath.CombatLevel(1, 1, 1, 10, 1, 1) == 3, "starting combat level 3, got " + CombatMath.CombatLevel(1, 1, 1, 10, 1, 1));
            Check(CombatMath.CombatLevel(99, 99, 99, 99, 99, 99) == 123, "maxed combat level 123");
        }

        static void TestInventory()
        {
            InventoryModel inv = new InventoryModel(GameContent.Item);
            Check(inv.FreeSlots() == 28, "starts with 28 free slots");
            Check(inv.Add("coins", 50) == 0, "coins fit");
            Check(inv.Add("coins", 25) == 0, "coins stack");
            Check(inv.Count("coins") == 75 && inv.FreeSlots() == 27, "coins share one slot");
            Check(inv.Add("logs_pine", 5) == 0 && inv.FreeSlots() == 22, "logs take one slot each");
            Check(inv.Add("logs_pine", 30) == 8, "only 22 more logs fit, 8 left over");
            Check(inv.FreeSlots() == 0, "inventory full");
            Check(!inv.CanAdd("bones", 1), "can't add to full inventory");
            Check(inv.CanAdd("coins", 10), "can still add to an existing stack when full");
            Check(inv.Remove("logs_pine", 3) && inv.Count("logs_pine") == 24, "remove logs");
            Check(!inv.Remove("logs_pine", 999), "can't remove more than you have");
            Check(inv.Count("logs_pine") == 24, "failed remove changes nothing");
            Check(inv.Add("not_an_item", 1) == 1, "unknown item is rejected");

            InventorySaveData data = inv.Capture();
            InventoryModel copy = new InventoryModel(GameContent.Item);
            copy.Restore(data);
            Check(copy.Count("coins") == 75 && copy.Count("logs_pine") == 24, "save/restore round trip");
        }

        static void TestEquipment()
        {
            InventoryModel inv = new InventoryModel(GameContent.Item);
            Func<Skill, int> low = s => 1;
            Func<Skill, int> high = s => 50;
            string reason;

            inv.Add("sword_bronze", 1);
            Check(inv.Equip(0, low, out reason) && inv.WeaponId == "sword_bronze", "equip bronze sword");
            Check(inv.GetSlot(0).IsEmpty, "slot empties after equip");
            Check(inv.TotalAttackBonus() == 6, "bronze sword attack bonus applies");

            inv.Add("sword_iron", 1);
            Check(!inv.Equip(0, low, out reason), "iron sword needs Attack 10");
            Check(inv.Equip(0, high, out reason) && inv.WeaponId == "sword_iron", "equip iron sword at level 50");
            Check(inv.GetSlot(0).ItemId == "sword_bronze", "old weapon swaps back into the slot");

            inv.Add("leather_body", 1);
            Check(inv.Equip(1, low, out reason) && inv.BodyId == "leather_body", "equip body armor");
            Check(inv.TotalDefenseBonus() == 8, "body defense bonus applies");
            Check(inv.Unequip(EquipSlot.Body) && inv.BodyId == null && inv.Count("leather_body") == 1, "unequip body");

            inv.Add("coins", 5);
            int coinSlot = -1;
            for (int i = 0; i < InventoryModel.SlotCount; i++) if (inv.GetSlot(i).ItemId == "coins") coinSlot = i;
            Check(!inv.Equip(coinSlot, high, out reason), "can't equip coins");
        }

        static void TestRecipes()
        {
            Recipe smelt = GameContent.Recipes.Find(r => r.Id == "smelt_bronze");
            Recipe forge = GameContent.Recipes.Find(r => r.Id == "forge_sword_bronze");
            Recipe ironBar = GameContent.Recipes.Find(r => r.Id == "smelt_iron");
            Check(smelt != null && forge != null && ironBar != null, "recipes exist");

            InventoryModel inv = new InventoryModel(GameContent.Item);
            Func<Skill, int> low = s => 1;
            string reason;

            Check(!smelt.TryMake(inv, low, out reason), "can't smelt without ore");
            inv.Add("ore_copper", 1);
            inv.Add("ore_tin", 1);
            Check(smelt.TryMake(inv, low, out reason), "smelt bronze bar");
            Check(inv.Count("bar_bronze") == 1 && inv.Count("ore_copper") == 0 && inv.Count("ore_tin") == 0, "ores consumed, bar produced");
            Check(forge.TryMake(inv, low, out reason) && inv.Count("sword_bronze") == 1, "forge bronze sword");

            inv.Add("ore_iron", 1);
            Check(!ironBar.TryMake(inv, low, out reason) && inv.Count("ore_iron") == 1, "iron bar needs Smithing 15 and keeps the ore");

            // full inventory with stackable output that needs a new slot: should fail and keep inputs
            InventoryModel full = new InventoryModel(GameContent.Item);
            full.Add("ore_copper", 1);
            full.Add("ore_tin", 1);
            full.Add("bones", 26);
            Check(full.FreeSlots() == 0, "test inventory is full");
            Check(smelt.TryMake(full, low, out reason) && full.Count("bar_bronze") == 1, "smelting frees slots so the bar fits");
        }

        static void TestDropRoller()
        {
            DropTable table = GameContent.DropTables["gnarl"];
            DropTable lucky = GameContent.DropTables[GameContent.LuckyTableId];
            DropRoller roller = new DropRoller(new System.Random(42));
            BadLuckTracker tracker = new BadLuckTracker();

            int bones = 0, coins = 0, kills = 2000;
            for (int i = 0; i < kills; i++)
            {
                foreach (DropResult result in roller.Roll(table, lucky, tracker))
                {
                    if (result.Stack.ItemId == "bones") bones++;
                    if (result.Stack.ItemId == "coins")
                    {
                        coins++;
                        Check(result.Stack.Quantity >= 5 && result.Stack.Quantity <= 25, "coin quantity in range");
                    }
                }
            }
            Check(bones == kills, "bones always drop (" + bones + "/" + kills + ")");
            float coinRate = coins / (float)kills;
            Check(coinRate > 0.33f && coinRate < 0.47f, "coins ~40% of kills, got " + coinRate);

            Check(Math.Abs(DropRoller.RareChance(100, 0) - 0.01) < 1e-9, "base rare chance 1/100");
            Check(Math.Abs(DropRoller.RareChance(100, 200) - 0.01) < 1e-9, "no protection until 2x rate");
            Check(DropRoller.RareChance(100, 210) > 0.02, "chance grows after 2x rate");
            Check(DropRoller.RareChance(100, 10000) == 1.0, "chance capped at 1");

            // a rare dropping resets its dry streak
            BadLuckTracker forced = new BadLuckTracker();
            forced.Set("gnarl:gnarl_crown", 100000);
            new DropRoller(new System.Random(1)).Roll(table, null, forced);
            Check(forced.Get("gnarl:gnarl_crown") == 0, "guaranteed rare resets dry streak");
        }

        static void TestContentIntegrity()
        {
            foreach (DropTable table in GameContent.DropTables.Values)
            {
                foreach (DropEntry entry in table.Always) Check(GameContent.Item(entry.ItemId) != null, table.Id + " always-drop item exists: " + entry.ItemId);
                foreach (DropEntry entry in table.Main)
                {
                    if (entry.ItemId != null) Check(GameContent.Item(entry.ItemId) != null, table.Id + " main-drop item exists: " + entry.ItemId);
                }
                foreach (RareDrop rare in table.Rares) Check(GameContent.Item(rare.ItemId) != null, table.Id + " rare item exists: " + rare.ItemId);
            }
            foreach (EnemyInfo enemy in GameContent.Enemies.Values)
            {
                Check(GameContent.DropTables.ContainsKey(enemy.DropTableId), enemy.Id + " has a drop table");
            }
            foreach (string id in GameContent.SceneEnemyRotation)
            {
                Check(GameContent.Enemies.ContainsKey(id), "scene rotation enemy exists: " + id);
            }
            foreach (NodeInfo node in GameContent.Nodes.Values)
            {
                Check(GameContent.Item(node.ItemId) != null, node.Id + " gives a real item");
                if (!string.IsNullOrEmpty(node.BonusItemId)) Check(GameContent.Item(node.BonusItemId) != null, node.Id + " bonus item exists");
            }
            foreach (ItemInfo item in GameContent.Items.Values)
            {
                if (item.Style == CombatStyle.Melee) continue;
                Check(item.Slot == EquipSlot.Weapon, item.Id + " ranged/magic item is a weapon");
                Check(item.Range > 2f, item.Id + " has a real range");
                ItemInfo ammo = GameContent.Item(item.AmmoId);
                Check(ammo != null && ammo.Stackable, item.Id + " uses stackable ammo");
            }
            EnemyInfo boss;
            Check(GameContent.Enemies.TryGetValue(GameContent.BossId, out boss) && boss.IsBoss, "boss is defined");
            foreach (Recipe recipe in GameContent.Recipes)
            {
                Check(GameContent.Item(recipe.Output.ItemId) != null, recipe.Id + " output exists");
                foreach (ItemStack input in recipe.Inputs) Check(GameContent.Item(input.ItemId) != null, recipe.Id + " input exists: " + input.ItemId);
            }
            foreach (ItemInfo item in GameContent.Items.Values)
            {
                if (!string.IsNullOrEmpty(item.Tier)) Check(GameContent.Tier(item.Tier) != null, item.Id + " tier exists");
                Color color;
                Check(ColorUtility.TryParseHtmlString(item.ColorHex, out color), item.Id + " has a valid colour");
            }
            Check(GameContent.Tiers.Count == 11, "11 material tiers");
        }
    }
}
