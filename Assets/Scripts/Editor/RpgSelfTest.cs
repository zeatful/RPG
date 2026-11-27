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
            Run("Smithing ladder", TestSmithingLadder);
            Run("Economy", TestEconomy);
            Run("Bank", TestBank);
            Run("Shops", TestShops);
            Run("Gathering swings", TestGatheringSwings);
            Run("Cooking and fires", TestCookingAndFires);
            Run("World layout", TestWorldLayout);
            Run("Death drops", TestDeathDrops);
            Run("Stamina", TestStamina);
            Run("Node respawns", TestNodeRespawns);

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
            Check(smelt.Station == StationKind.Furnace && forge.Station == StationKind.Anvil, "smelting is at a furnace, smithing at an anvil");
            Check(!forge.TryMake(inv, s => 10, out reason) && inv.Count("bar_bronze") == 1, "a sword takes 2 bars");
            inv.Add("bar_bronze", 1);
            Check(!forge.TryMake(inv, s => 10, out reason) && reason.Contains("hammer"), "smithing needs a hammer: " + reason);
            inv.Add("hammer", 1);
            Check(!forge.TryMake(inv, low, out reason), "bronze sword needs Smithing 3");
            Check(forge.TryMake(inv, s => 3, out reason) && inv.Count("sword_bronze") == 1 && inv.Count("bar_bronze") == 0, "forge bronze sword from 2 bars");
            Check(inv.Count("hammer") == 1, "the hammer is not used up");

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
                        Check(result.Stack.Quantity >= 2 && result.Stack.Quantity <= 8, "coin quantity in range");
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
            foreach (ItemInfo item in GameContent.Items.Values)
            {
                if (item.Tool != ToolKind.None) Check(item.ToolPower >= 1f, item.Id + " tool has power");
            }
            Check(SkillInfo.Count == 12 && (int)Skill.Cooking == 11, "12 skills, Pyro and Cooking at the end (keeps old saves valid)");
        }

        static void TestSmithingLadder()
        {
            int previousSmelt = 0;
            int previousCoal = -1;
            foreach (GameContent.SmithTier tier in GameContent.SmithTiers)
            {
                string key = tier.Tier.ToLower();
                Recipe smelt = GameContent.Recipes.Find(r => r.Id == "smelt_" + key);
                Recipe plate = GameContent.Recipes.Find(r => r.Id == "forge_plate_" + key);
                Recipe sword = GameContent.Recipes.Find(r => r.Id == "forge_sword_" + key);
                Check(smelt != null && plate != null && sword != null, key + " recipes exist");
                if (smelt == null || plate == null || sword == null) continue;
                Check(smelt.Level > previousSmelt, key + " smelting level rises (" + smelt.Level + ")");
                Check(tier.CoalPerBar >= previousCoal, key + " needs at least as much coal as the tier below");
                Check(plate.Level <= 99 && plate.Level > sword.Level, key + " platebody unlocks after the sword, by 99");
                Check(plate.Inputs[0].Quantity == GameContent.PlateBars && sword.Inputs[0].Quantity == GameContent.SwordBars, key + " bar counts");
                Check(plate.Xp > sword.Xp, key + " platebody gives more xp than the sword");

                // the mining level for the tier's ore never exceeds the smelting level (you can always smelt what you can mine soon)
                foreach (NodeInfo node in GameContent.Nodes.Values)
                {
                    if (node.ItemId == tier.OreId) Check(node.Level <= smelt.Level + 25, key + " ore minable around the smelting level");
                }
                previousSmelt = smelt.Level;
                previousCoal = tier.CoalPerBar;
            }
        }

        static int Value(string id) { return GameContent.Item(id).Value; }

        static void TestEconomy()
        {
            foreach (ShopInfo shop in GameContent.Shops.Values)
            {
                foreach (string id in shop.Stock)
                {
                    Check(GameContent.Item(id) != null, shop.Id + " sells a real item: " + id);
                    if (GameContent.Item(id) == null) continue;
                    Check(GameContent.BuyPrice(id) > GameContent.SellPrice(id), id + ": buying costs more than selling pays");
                }
                Check(shop.Stock.Contains(GameContent.FlintId), shop.Id + " sells flint and steel");
                Check(shop.Stock.Contains("bread"), shop.Id + " sells food");
            }

            // food is cheap and weak; gear is expensive
            foreach (ItemInfo item in GameContent.Items.Values)
            {
                if (!item.IsEdible) continue;
                Check(item.HealAmount <= 50, item.Id + " heals a low-level amount (" + item.HealAmount + ")");
                Check(item.Value <= 25, item.Id + " is cheap (" + item.Value + ")");
            }
            Check(Value("bread") * 10 <= Value("sword_bronze"), "a bronze sword costs at least 10 bread");
            Check(Value("platebody_iron") >= 800, "an iron platebody is a real grind (" + Value("platebody_iron") + ")");

            // smithed gear is worth more than the bars that went into it, but by less than buying it new
            foreach (GameContent.SmithTier tier in GameContent.SmithTiers)
            {
                string key = tier.Tier.ToLower();
                Check(Value("sword_" + key) > GameContent.SwordBars * tier.BarValue, key + " sword worth more than its bars");
                Check(Value("platebody_" + key) > GameContent.PlateBars * tier.BarValue, key + " platebody worth more than its bars");
                int oreCost = Value(tier.OreId) + tier.CoalPerBar * Value("ore_coal") + (tier.Tier == "Bronze" ? Value("ore_tin") : 0);
                Check(tier.BarValue >= oreCost, key + " bar worth at least its ores (" + tier.BarValue + " vs " + oreCost + ")");
            }

            // monsters drop a few coins at most; a bandit kill is a small fraction of an iron platebody
            foreach (DropTable table in GameContent.DropTables.Values)
            {
                foreach (DropEntry entry in table.Main)
                {
                    if (entry.ItemId == "coins" && table.Id != GameContent.BossId) Check(entry.Max <= 50, table.Id + " drops a low amount of coins (max " + entry.Max + ")");
                }
            }
            DropEntry bandit = GameContent.DropTables["bandit"].Main.Find(e => e.ItemId == "coins");
            double perKill = (bandit.Min + bandit.Max) / 2.0 * bandit.Weight / 100.0;
            double kills = Value("platebody_iron") / perKill;
            Check(kills > 100, "iron platebody takes over 100 bandit kills of coins (" + kills.ToString("0") + ")");
            Check(GameContent.DropTables["boar"].Main.TrueForAll(e => e.ItemId != "coins") && GameContent.DropTables["grey_wolf"].Main.TrueForAll(e => e.ItemId != "coins"), "animals don't carry coins");
            Check(GameContent.DropTables["boar"].Always.Exists(e => e.ItemId == "raw_meat"), "boars always drop raw meat");
        }

        static void TestBank()
        {
            InventoryModel inv = new InventoryModel(GameContent.Item);
            BankModel bank = new BankModel();
            inv.Add("coins", 500);
            inv.Add("logs_pine", 10);
            inv.Add("sword_bronze", 2);
            Check(bank.Deposit(inv, "logs_pine", 4) == 4 && inv.Count("logs_pine") == 6 && bank.Amount("logs_pine") == 4, "deposit some logs");
            Check(bank.DepositAll(inv) > 0 && inv.FreeSlots() == InventoryModel.SlotCount, "deposit everything");
            Check(bank.Amount("logs_pine") == 10 && bank.Amount("coins") == 500 && bank.Amount("sword_bronze") == 2, "bank stacks everything");
            Check(bank.Count == 3, "three bank stacks");
            Check(bank.Withdraw(inv, "logs_pine", 100) == 10 && inv.Count("logs_pine") == 10 && bank.Amount("logs_pine") == 0, "withdraw all logs");
            Check(bank.Count == 2, "empty stacks disappear");
            inv.Add("bones", 17);
            Check(inv.FreeSlots() == 1, "one slot left");
            Check(bank.Withdraw(inv, "sword_bronze", 2) == 1 && bank.Amount("sword_bronze") == 1, "withdraw only what fits");
            Check(bank.Withdraw(inv, "coins", 50) == 0, "no room for a new coin stack");

            BankModel copy = new BankModel();
            copy.Restore(bank.Capture());
            Check(copy.Amount("coins") == 500 && copy.Amount("sword_bronze") == 1, "bank save/restore round trip");
        }

        static void TestShops()
        {
            InventoryModel inv = new InventoryModel(GameContent.Item);
            string message;
            Check(!ShopLogic.TryBuy(inv, "bread", 1, out message), "can't buy with no money");
            inv.Add("coins", 25);
            Check(ShopLogic.TryBuy(inv, GameContent.FlintId, 1, out message) && inv.Count(GameContent.FlintId) == 1, "buy a flint and steel");
            Check(inv.Count("coins") == 25 - GameContent.BuyPrice(GameContent.FlintId), "paid for it");
            int coins = inv.Count("coins");
            Check(ShopLogic.TryBuy(inv, "bread", 100, out message) && inv.Count("bread") == coins / GameContent.BuyPrice("bread"), "buy only what you can afford");
            int before = inv.Count("coins");
            Check(ShopLogic.TrySell(inv, "bread", 1, out message) && inv.Count("coins") == before + GameContent.SellPrice("bread"), "sell for 40%");
            Check(!ShopLogic.TrySell(inv, "coins", 1, out message), "can't sell coins");
            inv.Add("burnt_meat", 1);
            Check(!ShopLogic.TrySell(inv, "burnt_meat", 1, out message) && inv.Count("burnt_meat") == 1, "shops won't take worthless items");

            InventoryModel full = new InventoryModel(GameContent.Item);
            full.Add("coins", 1000);
            full.Add("bones", 27);
            Check(!ShopLogic.TryBuy(full, "sword_bronze", 1, out message) && full.Count("coins") == 1000, "no space, no sale, no charge");
            Check(ShopLogic.TryBuy(full, "pine_arrows", 10, out message) == false || full.Count("pine_arrows") == 0, "a new arrow stack needs a free slot");
        }

        static void TestGatheringSwings()
        {
            NodeInfo copper = GameContent.Nodes["copper"];
            NodeInfo iron = GameContent.Nodes["iron"];
            NodeInfo cinderite = GameContent.Nodes["cinderite"];
            Check(GameContent.SwingsNeeded(copper, 1, 1.0) == 3, "copper at level 1 with a bronze pick takes 3 swings, got " + GameContent.SwingsNeeded(copper, 1, 1.0));
            Check(GameContent.SwingsNeeded(iron, 15, 1.0) > GameContent.SwingsNeeded(copper, 15, 1.0), "iron is harder than copper");
            Check(GameContent.SwingsNeeded(iron, 45, 1.0) < GameContent.SwingsNeeded(iron, 15, 1.0), "higher Mining needs fewer swings");
            Check(GameContent.SwingsNeeded(iron, 15, 1.6) < GameContent.SwingsNeeded(iron, 15, 1.0), "a steel pick needs fewer swings than bronze");
            Check(GameContent.SwingsNeeded(cinderite, 70, 1.0) >= 15, "cinderite is a slog with a bronze pick");
            Check(GameContent.SwingsNeeded(copper, 99, 2.9) == 1, "maxed miner with the best pick: 1 swing");

            int previous = 0;
            foreach (string id in new[] { "pine", "oak", "willow", "maple", "yew", "ashwood" })
            {
                NodeInfo tree = GameContent.Nodes[id];
                Check(tree.Kind == NodeKind.Tree && tree.Level > previous, id + " needs a higher Woodcutting level than the tree before");
                Check(GameContent.Burnables.ContainsKey(tree.ItemId) && GameContent.Burnables[tree.ItemId].Level == tree.Level, id + " logs burn at the same Pyro level");
                previous = tree.Level;
            }
            previous = 0;
            foreach (string id in new[] { "copper", "iron", "coal", "verdite", "cobalt", "cinderite" })
            {
                NodeInfo rock = GameContent.Nodes[id];
                Check(rock.Kind == NodeKind.Rock && rock.Level >= previous && rock.Hardness > 0 && rock.YieldMax >= rock.YieldMin, id + " rock data sane");
                previous = rock.Level;
            }
        }

        static void TestCookingAndFires()
        {
            Recipe meat = GameContent.Recipes.Find(r => r.Id == "cook_meat");
            Check(meat != null && meat.Station == StationKind.Fire && meat.Skill == Skill.Cooking, "cooking happens on a fire");
            Check(Math.Abs(meat.BurnChance(1) - 0.5) < 1e-9 && meat.BurnChance(21) < 1e-9, "burn chance 50% at level 1, none 20 levels up");

            InventoryModel inv = new InventoryModel(GameContent.Item);
            inv.Add("raw_meat", 2);
            string reason;
            bool burnt;
            Check(meat.TryMake(inv, s => 1, 0.1, out reason, out burnt) && burnt && inv.Count("burnt_meat") == 1, "a low roll burns the meat");
            Check(meat.TryMake(inv, s => 1, 0.9, out reason, out burnt) && !burnt && inv.Count("cooked_meat") == 1, "a high roll cooks it");
            Check(!GameContent.Item("raw_meat").IsEdible && GameContent.Item("cooked_meat").IsEdible, "raw meat must be cooked first");

            Check(GameContent.LightChance(1, 1) == 0.5 && GameContent.LightChance(99, 1) == 0.95, "fire lighting chance 50%..95%");
            Check(GameContent.Burnables["logs_ashwood"].Xp > GameContent.Burnables["logs_pine"].Xp * 5, "high logs give much more Pyro xp");
        }

        static void TestDeathDrops()
        {
            InventoryModel inv = new InventoryModel(GameContent.Item);
            inv.SetEquipped("sword_bronze", "platebody_iron");
            inv.Add("coins", 5000);
            inv.Add("axe_bronze", 1);
            inv.Add("bread", 3);
            string kept;
            List<ItemStack> dropped = inv.TakeAllExceptMostValuable(out kept);
            Check(kept == "platebody_iron", "keeps the most valuable item (iron platebody), kept " + kept);
            Check(inv.BodyId == "platebody_iron" && inv.WeaponId == null, "the kept platebody stays worn, the sword drops");
            Check(inv.FreeSlots() == InventoryModel.SlotCount, "everything in the bag drops");
            Check(dropped.Exists(d => d.ItemId == "coins" && d.Quantity == 5000), "coins drop (they have no item value)");
            Check(dropped.Exists(d => d.ItemId == "sword_bronze") && dropped.FindAll(d => d.ItemId == "bread").Count == 3, "sword and all bread drop");

            InventoryModel bag = new InventoryModel(GameContent.Item);
            bag.SetEquipped("sword_bronze", null);
            bag.Add("bar_steel", 2);
            dropped = bag.TakeAllExceptMostValuable(out kept);
            Check(kept == "bar_steel" && bag.Count("bar_steel") == 1 && bag.WeaponId == null, "keeps just one of two steel bars");
            Check(dropped.Count == 2, "drops the other bar and the sword");

            InventoryModel empty = new InventoryModel(GameContent.Item);
            dropped = empty.TakeAllExceptMostValuable(out kept);
            Check(kept == null && dropped.Count == 0, "nothing to drop when empty");
            Check(Pickup.Lifetime == 900f, "ground items last 15 minutes");
        }

        static void TestStamina()
        {
            StaminaModel stamina = new StaminaModel();
            Check(stamina.Running && stamina.Energy == StaminaModel.Max, "start running with full stamina");
            stamina.Tick(10f, true);
            Check(stamina.Energy < StaminaModel.Max, "running drains stamina");
            float afterRun = stamina.Energy;
            stamina.Tick(10f, false);
            Check(stamina.Energy > afterRun, "resting refills stamina");
            stamina.Toggle();
            Check(!stamina.Running && stamina.SpeedMultiplier < 1f, "walking is slower");
            float before = stamina.Energy;
            stamina.Tick(5f, true);
            Check(stamina.Energy >= before, "walking doesn't drain stamina");
            stamina.Toggle();
            stamina.Tick(1000f, true);
            Check(stamina.Energy == 0f && !stamina.Running && stamina.SpeedMultiplier < 1f, "running out drops you to a walk");
            Check(!stamina.Toggle(), "can't run with no stamina");
            Check(StaminaModel.Max / StaminaModel.DrainPerSecond > 30f, "at least 30 s of running from full");
        }

        static void TestNodeRespawns()
        {
            foreach (NodeInfo node in GameContent.Nodes.Values)
            {
                Check(node.YieldMin == 1 && node.YieldMax == 1, node.Id + " gives a single log/ore");
                Check(node.RespawnSeconds > 0, node.Id + " respawns");
            }
            foreach (string[] ladder in new[] { new[] { "pine", "oak", "willow", "maple", "yew", "ashwood" }, new[] { "copper", "iron", "coal", "verdite", "cobalt", "cinderite" } })
            {
                for (int i = 1; i < ladder.Length; i++)
                {
                    Check(GameContent.Nodes[ladder[i]].RespawnSeconds > GameContent.Nodes[ladder[i - 1]].RespawnSeconds, ladder[i] + " respawns slower than " + ladder[i - 1]);
                }
            }
            Check(RPG.World.WorldBuilder.AggroRange <= 6f && RPG.World.WorldBuilder.ChaseLeash >= 12f && RPG.World.WorldBuilder.ChaseLeash <= 25f && RPG.World.WorldBuilder.RoamRadius <= 5f, "small aggro and roaming, medium chase");
        }

        static void TestWorldLayout()
        {
            Check(RPG.World.WorldLayout.Towns.Count == 3, "three towns");
            foreach (RPG.World.TownInfo town in RPG.World.WorldLayout.Towns)
            {
                Check(GameContent.Shops.ContainsKey(town.ShopId), town.Id + " has a shop");
                Check(RPG.World.WorldLayout.RoadAngles(town).Count > 0, town.Id + " has a road");
            }
            foreach (RPG.World.ZoneInfo zone in RPG.World.WorldLayout.Zones)
            {
                foreach (RPG.World.SpawnGroup group in zone.Enemies) Check(GameContent.Enemies.ContainsKey(group.Id), zone.Id + " enemy exists: " + group.Id);
                foreach (RPG.World.SpawnGroup group in zone.Nodes) Check(GameContent.Nodes.ContainsKey(group.Id), zone.Id + " node exists: " + group.Id);
            }
            HashSet<string> nodes = new HashSet<string>();
            foreach (RPG.World.ZoneInfo zone in RPG.World.WorldLayout.Zones) foreach (RPG.World.SpawnGroup group in zone.Nodes) nodes.Add(group.Id);
            foreach (string id in GameContent.Nodes.Keys) Check(nodes.Contains(id), "every tree and rock appears in the world: " + id);
            HashSet<string> enemies = new HashSet<string>();
            foreach (RPG.World.ZoneInfo zone in RPG.World.WorldLayout.Zones) foreach (RPG.World.SpawnGroup group in zone.Enemies) enemies.Add(group.Id);
            foreach (EnemyInfo enemy in GameContent.Enemies.Values)
            {
                if (!enemy.IsBoss) Check(enemies.Contains(enemy.Id), "every monster appears in the world: " + enemy.Id);
            }
            // monsters near Brookhollow never attack first
            foreach (RPG.World.SpawnGroup group in RPG.World.WorldLayout.Zones[0].Enemies)
            {
                Check(!GameContent.Enemies[group.Id].Aggressive, "starter monster " + group.Id + " is passive");
            }
        }
    }
}
