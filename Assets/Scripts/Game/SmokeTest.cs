using System;
using System.Collections;
using System.Collections.Generic;
using RPG.Combat;
using RPG.Content;
using RPG.Control;
using RPG.Core;
using RPG.Gathering;
using RPG.Items;
using RPG.Saving;
using RPG.Stats;
using RPG.World;
using UnityEngine;
using UnityEngine.AI;

namespace RPG.Game
{
    /// <summary>
    /// Automated play-through used to verify a real build: launch the game with -rpgSmokeTest and it
    /// chops a tree, kills an enemy, picks up loot, gets slammed by the boss and round-trips a save,
    /// then quits with exit code 0 (pass) or 1 (fail). Never active in normal play.
    ///
    /// -rpgWatch runs the same play-through in real time with walking instead of teleporting and an
    /// on-screen checklist, so a person can watch it. -rpgRecord &lt;dir&gt; also saves 15 fps frames there.
    /// </summary>
    public class SmokeTest : MonoBehaviour
    {
        const string SaveFile = "rpg_smoketest";

        public static bool Requested
        {
            get { return HasArg("-rpgSmokeTest") || Watch; }
        }

        public static bool Watch { get { return HasArg("-rpgWatch"); } }

        static bool HasArg(string name)
        {
            return Array.IndexOf(Environment.GetCommandLineArgs(), name) >= 0;
        }

        static string ArgValue(string name)
        {
            string[] args = Environment.GetCommandLineArgs();
            int index = Array.IndexOf(args, name);
            return index >= 0 && index + 1 < args.Length ? args[index + 1] : null;
        }

        class StepState
        {
            public string Title;
            public string Status = "";
        }

        readonly List<StepState> steps = new List<StepState>();
        StepState current;
        string detail = "";
        string verdict;
        bool watch;

        readonly List<string> failures = new List<string>();
        readonly List<string> errors = new List<string>();
        GameObject player;
        Vector3 playerStart;

        private void OnEnable() { Application.logMessageReceived += OnLog; }
        private void OnDisable() { Application.logMessageReceived -= OnLog; }

        void OnLog(string condition, string stackTrace, LogType type)
        {
            if (type == LogType.Exception || type == LogType.Error || type == LogType.Assert)
            {
                errors.Add(type + ": " + condition + "\n" + stackTrace);
            }
        }

        private IEnumerator Start()
        {
            watch = Watch;
            string recordDir = ArgValue("-rpgRecord");
            string[] titles =
            {
                "Check the world spawned", "Buy flint and steel at the store", "Chop a pine tree, fletch arrows",
                "Light a fire, cook meat", "Mine copper and tin", "Smelt a bar, smith an axe", "Bank: deposit and withdraw",
                "Kill an enemy, pick up loot", "Die, respawn, recover items", "Survive Old Gullet's slam", "Save, change, load"
            };
            foreach (string title in titles)
            {
                steps.Add(new StepState { Title = title });
            }

            Log("starting" + (watch ? " (watch mode)" : ""));
            if (!string.IsNullOrEmpty(recordDir)) StartCoroutine(Record(recordDir));
            yield return new WaitForSeconds(watch ? 3f : 1f);
            player = GameObject.FindWithTag("Player");
            if (player != null) playerStart = player.transform.position;
            Time.timeScale = watch ? 1f : 3f;
            // Plenty of HP so a wandering wolf can't end the test early; the boss step still checks damage.
            if (player != null) player.GetComponent<Health>().SetMaxHealth(100000, true);

            yield return Step(0, "setup", CheckSetup());
            yield return Step(1, "shop", TestShop());
            yield return Step(2, "gathering", TestGathering());
            yield return Step(3, "fire and cooking", TestFireAndCooking());
            yield return Step(4, "mining", TestMining());
            yield return Step(5, "smithing", TestSmithing());
            yield return Step(6, "bank", TestBank());
            yield return Step(7, "combat and loot", TestCombat());
            yield return Step(8, "death", TestDeath());
            yield return Step(9, "boss", TestBoss());
            yield return Step(10, "save and load", TestSaveLoad());

            foreach (string error in errors) failures.Add("logged " + error);
            Time.timeScale = 1f;
            current = null;
            if (failures.Count == 0)
            {
                Log("PASS");
                verdict = "ALL CHECKS PASSED";
            }
            else
            {
                foreach (string failure in failures) Log("FAIL " + failure);
                verdict = failures.Count + " CHECK(S) FAILED: " + failures[0];
            }
            detail = watch ? "Closing in 8 seconds..." : "";
            if (watch) yield return new WaitForSecondsRealtime(8f);
            Application.Quit(failures.Count == 0 ? 0 : 1);
        }

        IEnumerator Step(int index, string name, IEnumerator body)
        {
            int before = failures.Count;
            current = steps[index];
            current.Status = "running";
            detail = "";
            Log("step: " + name);
            yield return StartCoroutine(body);
            bool ok = failures.Count == before;
            current.Status = ok ? "ok" : "FAILED";
            Log(name + (ok ? " ok" : " FAILED"));
            if (watch) yield return new WaitForSeconds(1.5f);
        }

        // Generous time limits when walking everywhere in real time.
        float Limit(float seconds)
        {
            return watch ? seconds * 2.5f : seconds;
        }

        void Say(string text)
        {
            detail = text;
            Log("  " + text);
        }

        IEnumerator Record(string dir)
        {
            System.IO.Directory.CreateDirectory(dir);
            Time.captureFramerate = 30;
            int frame = 0;
            int saved = 0;
            while (true)
            {
                yield return new WaitForEndOfFrame();
                if (frame++ % 2 != 0) continue;
                ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(dir, "frame_" + saved.ToString("00000") + ".png"));
                saved++;
            }
        }

        private void OnGUI()
        {
            if (steps.Count == 0) return;
            GUI.depth = -100;
            float scale = Mathf.Max(1f, Screen.height / 720f);
            Matrix4x4 old = GUI.matrix;
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1));
            float width = 360f;
            float x = (Screen.width / scale - width) * 0.5f;
            float y = 12f;
            float height = 34f + steps.Count * 22f + 44f;
            GUI.color = new Color(0, 0, 0, 0.75f);
            GUI.DrawTexture(new Rect(x, y, width, height), Texture2D.whiteTexture);
            GUI.color = Color.white;

            GUIStyle title = new GUIStyle(GUI.skin.label) { fontSize = 15, fontStyle = FontStyle.Bold };
            GUIStyle line = new GUIStyle(GUI.skin.label) { fontSize = 13 };
            GUI.Label(new Rect(x + 10, y + 6, width - 20, 24), "AUTO PLAY-THROUGH" + (watch ? "" : "  (fast)"), title);
            for (int i = 0; i < steps.Count; i++)
            {
                StepState step = steps[i];
                string mark = step.Status == "ok" ? "[ok]" : step.Status == "FAILED" ? "[X]" : step == current ? "[..]" : "[  ]";
                GUI.color = step.Status == "ok" ? new Color(0.5f, 1f, 0.5f) : step.Status == "FAILED" ? new Color(1f, 0.45f, 0.45f) : step == current ? new Color(1f, 0.85f, 0.3f) : new Color(0.8f, 0.8f, 0.8f);
                GUI.Label(new Rect(x + 10, y + 32 + i * 22, width - 20, 22), mark + "  " + (i + 1) + ". " + step.Title, line);
            }
            GUI.color = verdict == null ? Color.white : failures.Count == 0 ? new Color(0.5f, 1f, 0.5f) : new Color(1f, 0.45f, 0.45f);
            float bottom = y + 32 + steps.Count * 22 + 2;
            GUI.Label(new Rect(x + 10, bottom, width - 20, 22), verdict ?? detail, line);
            if (verdict != null) GUI.Label(new Rect(x + 10, bottom + 18, width - 20, 22), detail, line);
            GUI.color = Color.white;
            GUI.matrix = old;
        }

        void Check(bool condition, string message)
        {
            if (!condition) failures.Add(message);
        }

        static void DumpGameLog()
        {
            foreach (GameLog.Message message in GameLog.Messages) Log("  chat: " + message.Text);
        }

        static void Log(string message)
        {
            Debug.Log("[RPG Smoke] " + message);
        }

        IEnumerator CheckSetup()
        {
            Check(player != null, "no player");
            if (player == null) yield break;
            Check(player.GetComponent<SkillSet>() != null, "player has no SkillSet");
            Check(player.GetComponent<Inventory>() != null, "player has no Inventory");
            Check(player.GetComponent<PlayerStats>() != null, "player has no PlayerStats");
            Check(player.GetComponent<Gatherer>() != null, "player has no Gatherer");
            Check(player.GetComponent<Bank>() != null && player.GetComponent<StationUser>() != null && player.GetComponent<Crafter>() != null && player.GetComponent<Firemaker>() != null, "player is missing bank/station/crafter/firemaker");
            Check(BossController.Active != null, "boss was not spawned");
            Check(DayNightCycle.Instance != null, "no day/night cycle");
            Check(WorldBuilder.Root != null, "the open world was not generated (is the World scene loaded?)");
            int nodes = FindObjectsByType<ResourceNode>(FindObjectsSortMode.None).Length;
            Log("world built in " + WorldBuilder.BuildSeconds.ToString("0.0") + "s: enemies " + EnemyProfile.All.Count + ", nodes " + nodes + ", stations " + Station.All.Count);
            Check(EnemyProfile.All.Count >= 60, "expected 60+ monsters, found " + EnemyProfile.All.Count);
            Check(nodes >= 100, "expected 100+ trees and rocks, found " + nodes);
            Check(WorldBuilder.BuildSeconds < 15f, "world took " + WorldBuilder.BuildSeconds + "s to build");

            HashSet<string> nodeTypes = new HashSet<string>();
            foreach (ResourceNode node in FindObjectsByType<ResourceNode>(FindObjectsSortMode.None)) nodeTypes.Add(node.Info.Id);
            foreach (string id in GameContent.Nodes.Keys) Check(nodeTypes.Contains(id), "no " + id + " in the world");
            HashSet<string> enemyTypes = new HashSet<string>();
            foreach (EnemyProfile enemy in EnemyProfile.All) enemyTypes.Add(enemy.Info.Id);
            foreach (string id in GameContent.Enemies.Keys) Check(enemyTypes.Contains(id), "no " + id + " in the world");

            foreach (TownInfo town in WorldLayout.Towns)
            {
                foreach (StationKind kind in new[] { StationKind.Bank, StationKind.Shop, StationKind.Furnace, StationKind.Anvil, StationKind.Fire })
                {
                    bool found = false;
                    foreach (Station station in Station.All) if (station.Kind == kind && station.Town == town.Name) found = true;
                    Check(found, town.Name + " has no " + kind);
                }
            }
            Check(Inventory().Model.Count("axe_bronze") == 1 && Inventory().Model.Count("hammer") == 1 && Inventory().Model.Count("coins") == 25, "starter kit missing");
            float fromStart = RpgUtil.FlatDistance(WorldBuilder.BossLairPosition, player.transform.position);
            Check(fromStart > 100f, "boss lair is only " + fromStart + " m from the start");
            yield break;
        }

        Inventory Inventory() { return player.GetComponent<Inventory>(); }
        SkillSet Skills() { return player.GetComponent<SkillSet>(); }

        Station FindStation(StationKind kind, string town)
        {
            Station best = null;
            foreach (Station station in Station.All)
            {
                if (station.Kind != kind || station.GetComponent<Animator>() != null) continue;
                if (town != null && station.Town != town) continue;
                if (best == null || RpgUtil.FlatDistance(station.transform.position, player.transform.position) < RpgUtil.FlatDistance(best.transform.position, player.transform.position)) best = station;
            }
            return best;
        }

        /// <summary>Walk (watch) or warp (fast) to a station and open it.</summary>
        IEnumerator OpenStation(Station station)
        {
            StationUser user = player.GetComponent<StationUser>();
            if (!watch) player.GetComponent<NavMeshAgent>().Warp(PointNear(station.ApproachPoint(player.transform.position), 0f));
            user.Use(station);
            float timeout = Time.time + Limit(10f + RpgUtil.FlatDistance(station.transform.position, player.transform.position) / 4f);
            while (Time.time < timeout && user.Open != station) yield return null;
            Check(user.Open == station, "could not open " + station.Label + " (" + station.DistanceTo(player.transform.position) + " m away)");
        }

        IEnumerator TestShop()
        {
            Station shop = FindStation(StationKind.Shop, "Brookhollow");
            Check(shop != null, "no shop in Brookhollow");
            if (shop == null) yield break;
            Say("Walking to the Brookhollow general store...");
            yield return OpenStation(shop);
            if (watch) yield return new WaitForSeconds(1.5f);
            InventoryModel model = Inventory().Model;
            int coins = model.Count("coins");
            string message;
            Check(ShopLogic.TryBuy(model, GameContent.FlintId, 1, out message), "could not buy flint and steel: " + message);
            Inventory().NotifyChanged();
            Check(model.Count(GameContent.FlintId) == 1 && model.Count("coins") == coins - GameContent.BuyPrice(GameContent.FlintId), "flint and steel purchase didn't add up");
            Say(message);
            if (watch) yield return new WaitForSeconds(1.5f);
            player.GetComponent<StationUser>().Close();
        }

        IEnumerator GoNear(Vector3 position, float offset)
        {
            Vector3 target = PointNear(position, offset);
            if (!watch)
            {
                player.GetComponent<NavMeshAgent>().Warp(target);
                yield break;
            }
            player.GetComponent<RPG.Movement.Mover>().StartMoveAction(target, 1f);
            float timeout = Time.time + 15f + RpgUtil.FlatDistance(player.transform.position, target) / 3f;
            while (Time.time < timeout && RpgUtil.FlatDistance(player.transform.position, target) > 1.2f) yield return null;
        }

        Vector3 PointNear(Vector3 position, float offset)
        {
            Vector3 direction = player.transform.position - position;
            direction.y = 0;
            if (direction.sqrMagnitude < 0.01f) direction = Vector3.forward;
            Vector3 target = position + direction.normalized * offset;
            NavMeshHit hit;
            if (NavMesh.SamplePosition(target, out hit, 3f, NavMesh.AllAreas)) target = hit.position;
            return target;
        }

        IEnumerator TestGathering()
        {
            ResourceNode tree = null;
            float best = float.MaxValue;
            foreach (ResourceNode node in FindObjectsByType<ResourceNode>(FindObjectsSortMode.None))
            {
                if (node.Info.Id != "pine" || node.IsDepleted) continue;
                float distance = Vector3.Distance(player.transform.position, node.transform.position);
                if (distance < best)
                {
                    best = distance;
                    tree = node;
                }
            }
            Check(tree != null, "no pine tree to chop");
            if (tree == null) yield break;

            Inventory inventory = player.GetComponent<Inventory>();
            SkillSet skills = player.GetComponent<SkillSet>();
            string logId = tree.Info.ItemId;
            int logsBefore = inventory.Model.Count(logId);
            Say("Walking to a pine tree to chop it...");
            if (!watch) yield return GoNear(tree.transform.position, 1.8f);
            player.GetComponent<Gatherer>().Gather(tree);

            float timeout = Time.time + Limit(60f + best / 4f);
            while (Time.time < timeout && inventory.Model.Count(logId) == logsBefore) yield return null;

            if (inventory.Model.Count(logId) == logsBefore)
            {
                Log("gather debug: distance " + RpgUtil.FlatDistance(player.transform.position, tree.transform.position)
                    + ", depleted " + tree.IsDepleted + ", wc level " + skills.GetLevel(Skill.Woodcutting)
                    + ", free slots " + inventory.Model.FreeSlots() + ", player " + player.transform.position + ", tree " + tree.transform.position);
                DumpGameLog();
            }
            Check(inventory.Model.Count(logId) > logsBefore, "chopping a pine never produced logs");
            Check(skills.GetXp(Skill.Woodcutting) > 0, "no woodcutting xp");
            player.GetComponent<ActionScheduler>().CancelCurrentAction();
            if (inventory.Model.Count(logId) == logsBefore) yield break;

            // turn the logs into arrows
            Recipe arrows = GameContent.Recipes.Find(r => r.Id == "craft_arrows_pine");
            Check(arrows != null, "pine arrow recipe missing");
            if (arrows == null) yield break;
            Say("Got logs! Fletching them into arrows...");
            if (watch) yield return new WaitForSeconds(1.5f);
            int arrowsBefore = inventory.Model.Count(arrows.Output.ItemId);
            Check(inventory.CanMake(arrows), "cannot craft pine arrows from fresh logs");
            inventory.Make(arrows);
            Check(inventory.Model.Count(arrows.Output.ItemId) > arrowsBefore, "crafting produced no arrows");
            Check(skills.GetXp(Skill.Crafting) > 0, "no crafting xp");
        }

        IEnumerator GatherOne(string nodeId)
        {
            ResourceNode target = null;
            float best = float.MaxValue;
            foreach (ResourceNode node in FindObjectsByType<ResourceNode>(FindObjectsSortMode.None))
            {
                if (node.Info.Id != nodeId || node.IsDepleted) continue;
                float distance = Vector3.Distance(player.transform.position, node.transform.position);
                if (distance < best)
                {
                    best = distance;
                    target = node;
                }
            }
            Check(target != null, "no " + nodeId + " to gather");
            if (target == null) yield break;
            string itemId = target.Info.ItemId;
            int before = Inventory().Model.Count(itemId);
            if (!watch) yield return GoNear(target.transform.position, 1.8f);
            player.GetComponent<Gatherer>().Gather(target);
            float timeout = Time.time + Limit(60f + best / 4f);
            while (Time.time < timeout && Inventory().Model.Count(itemId) == before)
            {
                if (target.IsDepleted) break;
                yield return null;
            }
            Check(Inventory().Model.Count(itemId) > before, "gathering " + nodeId + " produced nothing");
            player.GetComponent<ActionScheduler>().CancelCurrentAction();
        }

        IEnumerator TestFireAndCooking()
        {
            InventoryModel model = Inventory().Model;
            if (model.Count("logs_pine") == 0)
            {
                Say("Need another log first...");
                yield return GatherOne("pine");
            }
            Check(model.Count("logs_pine") > 0, "no pine logs to burn");
            if (model.Count("logs_pine") == 0) yield break;

            // step onto open ground first
            Vector3 clear = PointNear(player.transform.position + new Vector3(2.5f, 0, 2.5f), 0f);
            yield return GoNear(clear, 0f);
            Say("Lighting the pine logs with the flint and steel...");
            int fires = CountFires();
            double pyro = Skills().GetXp(Skill.Pyro);
            int slot = SlotOf("logs_pine");
            Inventory().UseSlot(slot);
            float timeout = Time.time + Limit(30f);
            while (Time.time < timeout && CountFires() == fires) yield return null;
            Check(CountFires() > fires, "no fire was lit");
            Check(Skills().GetXp(Skill.Pyro) > pyro, "no Pyro xp for lighting a fire");
            if (CountFires() == fires) yield break;
            yield return new WaitForSeconds(watch ? 1.5f : 0.5f);

            Station fire = Station.Nearest(StationKind.Fire, player.transform.position, 6f);
            Check(fire != null, "can't find the new fire");
            if (fire == null) yield break;
            Inventory().TryAdd("raw_meat", 3);
            Say("Cooking 3 raw meat on the fire (the test hands over the meat)...");
            if (!fire.isActiveAndEnabled) yield break;
            yield return OpenStation(fire);
            Recipe cook = GameContent.Recipes.Find(r => r.Id == "cook_meat");
            player.GetComponent<Crafter>().Make(cook, 3);
            timeout = Time.time + Limit(20f);
            while (Time.time < timeout && model.Count("raw_meat") > 0) yield return null;
            Check(model.Count("raw_meat") == 0, "meat wasn't all cooked");
            Check(model.Count("cooked_meat") + model.Count("burnt_meat") == 3, "expected 3 cooked or burnt meat");
            Log("cooked " + model.Count("cooked_meat") + ", burnt " + model.Count("burnt_meat"));
            Say("Cooked " + model.Count("cooked_meat") + ", burnt " + model.Count("burnt_meat") + ".");
            player.GetComponent<StationUser>().Close();
        }

        int CountFires()
        {
            int count = 0;
            foreach (Station station in Station.All) if (station.Kind == StationKind.Fire && station.Town == null) count++;
            return count;
        }

        int SlotOf(string itemId)
        {
            for (int i = 0; i < InventoryModel.SlotCount; i++) if (Inventory().Model.GetSlot(i).ItemId == itemId) return i;
            return -1;
        }

        IEnumerator TestMining()
        {
            Say("Walking to the Brookhollow mine for copper...");
            yield return GatherOne("copper");
            Say("Now some tin...");
            yield return GatherOne("tin");
            Check(Skills().GetXp(Skill.Mining) > 0, "no mining xp");
        }

        IEnumerator TestSmithing()
        {
            InventoryModel model = Inventory().Model;
            Station furnace = FindStation(StationKind.Furnace, "Brookhollow");
            Station anvil = FindStation(StationKind.Anvil, "Brookhollow");
            Check(furnace != null && anvil != null, "Brookhollow has no furnace/anvil");
            if (furnace == null || anvil == null || model.Count("ore_copper") == 0 || model.Count("ore_tin") == 0) yield break;

            Recipe smelt = GameContent.Recipes.Find(r => r.Id == "smelt_bronze");
            Recipe axe = GameContent.Recipes.Find(r => r.Id == "forge_axe_bronze");
            Say("Carrying the ore back to the furnace in town...");
            yield return OpenStation(furnace);
            player.GetComponent<Crafter>().Make(smelt, 1);
            float timeout = Time.time + Limit(10f);
            while (Time.time < timeout && model.Count("bar_bronze") == 0) yield return null;
            Check(model.Count("bar_bronze") == 1, "smelting made no bronze bar");

            Say("Hammering the bar into an axe at the anvil...");
            yield return OpenStation(anvil);
            int axes = model.Count("axe_bronze");
            player.GetComponent<Crafter>().Make(axe, 1);
            timeout = Time.time + Limit(10f);
            while (Time.time < timeout && model.Count("axe_bronze") == axes) yield return null;
            Check(model.Count("axe_bronze") == axes + 1, "smithing made no axe");
            Check(Skills().GetXp(Skill.Smithing) > 0, "no smithing xp");
            player.GetComponent<StationUser>().Close();
        }

        IEnumerator TestBank()
        {
            Station chest = FindStation(StationKind.Bank, "Brookhollow");
            Check(chest != null, "no bank chest in Brookhollow");
            if (chest == null) yield break;
            Say("Banking everything at the Brookhollow bank chest...");
            yield return OpenStation(chest);
            Bank bank = player.GetComponent<Bank>();
            InventoryModel model = Inventory().Model;
            int coins = model.Count("coins");
            int axes = model.Count("axe_bronze");
            bank.DepositAll();
            Check(model.FreeSlots() == InventoryModel.SlotCount, "inventory not empty after deposit all");
            Check(bank.Model.Amount("axe_bronze") == axes && bank.Model.Amount("coins") == coins, "bank didn't receive the items");
            if (watch) yield return new WaitForSeconds(2f);
            Say("Taking the tools, food and coins back out...");
            foreach (string id in new[] { "axe_bronze", "pickaxe_bronze", "hammer", GameContent.FlintId, "coins", "cooked_meat", "bread", "pine_arrows" })
            {
                bank.Withdraw(id, id == "axe_bronze" ? 1 : int.MaxValue);
            }
            Check(model.Count("pickaxe_bronze") == 1 && model.Count("coins") == coins && model.Count("axe_bronze") == 1, "withdraw didn't return the items");
            Check(bank.Model.Amount("axe_bronze") == axes - 1, "spare axe should stay in the bank");
            if (watch) yield return new WaitForSeconds(1.5f);
            player.GetComponent<StationUser>().Close();
        }



        IEnumerator TestCombat()
        {
            // the nearest of the weakest monsters
            EnemyProfile target = null;
            foreach (EnemyProfile enemy in EnemyProfile.All)
            {
                if (enemy.GetComponent<BossController>() != null || enemy.Health.IsDead()) continue;
                if (target == null || enemy.Info.Level < target.Info.Level
                    || (enemy.Info.Level == target.Info.Level && Vector3.Distance(enemy.transform.position, player.transform.position) < Vector3.Distance(target.transform.position, player.transform.position)))
                {
                    target = enemy;
                }
            }
            Check(target != null, "no enemy to fight");
            if (target == null) yield break;
            Say("Attacking a " + target.Info.Name + " (level " + target.Info.Level + ")...");

            Health playerHealth = player.GetComponent<Health>();
            playerHealth.SetMaxHealth(100000, true);
            SkillSet skills = player.GetComponent<SkillSet>();
            int hits = 0;
            int enemySwings = 0;
            GameObject enemyObject = target.gameObject;
            Action<GameObject, GameObject, int> counter = (attacker, defender, damage) =>
            {
                if (attacker == player) hits++;
                if (attacker == enemyObject) enemySwings++;
            };
            Fighter.onAttackResolved += counter;

            if (!watch) yield return GoNear(target.transform.position, 1.5f);
            Fighter fighter = player.GetComponent<Fighter>();
            float timeout = Time.time + Limit(120f);
            while (Time.time < timeout && !target.Health.IsDead())
            {
                // keep re-issuing the attack in case something interrupted it
                if (fighter.CanAttack(target.gameObject)) fighter.Attack(target.gameObject);
                yield return new WaitForSeconds(0.5f);
            }
            Fighter.onAttackResolved -= counter;

            Check(hits > 0, "player attacks never resolved (animation Hit events not firing?)");
            Check(enemySwings > 0, target.Info.Name + " never attacked back (hidden puppet animator not firing Hit events?)");
            Log(target.Info.Name + " swung " + enemySwings + " times, player " + hits);
            Check(target.Health.IsDead(), target.Info.Name + " did not die within the time limit (" + hits + " swings)");
            Check(skills.GetXp(Skill.Attack) > 0 && skills.GetXp(Skill.Strength) > 0, "no attack/strength xp from combat");
            if (!target.Health.IsDead()) yield break;

            yield return new WaitForSeconds(0.5f);
            Pickup[] pickups = FindObjectsByType<Pickup>(FindObjectsSortMode.None);
            Check(pickups.Length > 0, target.Info.Name + " dropped nothing");
            if (pickups.Length == 0) yield break;

            Pickup loot = pickups[0];
            string itemId = loot.Stack.ItemId;
            Inventory inventory = player.GetComponent<Inventory>();
            int before = inventory.Model.Count(itemId);
            Say("Killed it! Picking up " + loot.Label + "...");
            player.GetComponent<PickupCollector>().Collect(loot);
            timeout = Time.time + Limit(30f);
            while (Time.time < timeout && inventory.Model.Count(itemId) == before) yield return null;
            Check(inventory.Model.Count(itemId) > before, "could not pick up " + itemId);
        }

        IEnumerator TestDeath()
        {
            Health health = player.GetComponent<Health>();
            InventoryModel model = Inventory().Model;
            int carried = InventoryModel.SlotCount - model.FreeSlots() + (model.WeaponId != null ? 1 : 0) + (model.BodyId != null ? 1 : 0);
            Vector3 deathSpot = player.transform.position;
            int pickupsBefore = FindObjectsByType<Pickup>(FindObjectsSortMode.None).Length;
            Say("Dropping dead on purpose (test) to check the death rules...");
            if (watch) yield return new WaitForSeconds(1f);
            health.TakeDamage(10000000f, null);
            yield return null;
            int keptKinds = InventoryModel.SlotCount - model.FreeSlots() + (model.WeaponId != null ? 1 : 0) + (model.BodyId != null ? 1 : 0);
            int dropped = FindObjectsByType<Pickup>(FindObjectsSortMode.None).Length - pickupsBefore;
            Check(keptKinds <= 1, "kept " + keptKinds + " items on death, expected just the most valuable");
            Check(dropped == carried - keptKinds, "dropped " + dropped + " piles, expected " + (carried - keptKinds));
            Log("died carrying " + carried + " items, kept " + keptKinds + ", dropped " + dropped);

            float timeout = Time.time + 15f;
            while (Time.time < timeout && health.IsDead()) yield return null;
            Check(!health.IsDead(), "never respawned");
            yield return null;
            float fromSpawn = RpgUtil.FlatDistance(player.transform.position, WorldBuilder.StartPosition);
            Check(fromSpawn < 3f, "respawned " + fromSpawn + " m from the spawn point");
            health.SetMaxHealth(100000, true);

            Say("Woke up at the spawn point. Running back for the dropped items...");
            PickupCollector collector = player.GetComponent<PickupCollector>();
            for (int round = 0; round < 40; round++)
            {
                Pickup nearest = null;
                foreach (Pickup pickup in FindObjectsByType<Pickup>(FindObjectsSortMode.None))
                {
                    if (RpgUtil.FlatDistance(pickup.transform.position, deathSpot) > 4f) continue;
                    if (nearest == null || RpgUtil.FlatDistance(pickup.transform.position, player.transform.position) < RpgUtil.FlatDistance(nearest.transform.position, player.transform.position)) nearest = pickup;
                }
                if (nearest == null) break;
                if (!watch) player.GetComponent<NavMeshAgent>().Warp(PointNear(nearest.transform.position, 0.5f));
                collector.Collect(nearest);
                float wait = Time.time + Limit(10f + RpgUtil.FlatDistance(nearest.transform.position, player.transform.position) / 3f);
                while (Time.time < wait && nearest != null) yield return null;
            }
            int left = 0;
            foreach (Pickup pickup in FindObjectsByType<Pickup>(FindObjectsSortMode.None)) if (RpgUtil.FlatDistance(pickup.transform.position, deathSpot) <= 4f) left++;
            Check(left == 0, left + " dropped items couldn't be picked back up");
            Check(model.Count("pickaxe_bronze") == 1 && model.Count("hammer") == 1, "tools recovered after death");
        }

        IEnumerator TestBoss()
        {
            BossController boss = BossController.Active;
            if (boss == null) yield break;
            Health playerHealth = player.GetComponent<Health>();
            playerHealth.SetMaxHealth(100000, true);
            float startHp = playerHealth.GetHealth();

            Say("Travelling to Mirefen Swamp to take a slam from Old Gullet (test HP boosted)...");
            yield return GoNear(boss.transform.position, 2f);
            float timeout = Time.time + Limit(30f);
            while (Time.time < timeout && playerHealth.GetHealth() >= startHp) yield return null;
            Check(playerHealth.GetHealth() < startHp, "boss never damaged the player");
            Say("Old Gullet hit for " + (startHp - playerHealth.GetHealth()) + ". Running away!");

            // run back along the way we came so it gives up the chase
            Vector3 away = boss.transform.position + (playerStart - boss.transform.position).normalized * 30f;
            yield return GoNear(away, 0f);
            player.GetComponent<ActionScheduler>().CancelCurrentAction();
        }

        IEnumerator TestSaveLoad()
        {
            SavingSystem saving = FindFirstObjectByType<SavingSystem>();
            Check(saving != null, "no SavingSystem in scene");
            if (saving == null) yield break;

            SkillSet skills = player.GetComponent<SkillSet>();
            Inventory inventory = player.GetComponent<Inventory>();
            Bank bank = player.GetComponent<Bank>();
            double xp = skills.GetXp(Skill.Woodcutting);
            double pyro = skills.GetXp(Skill.Pyro);
            int logs = inventory.Model.Count("logs_pine");
            int banked = bank.Model.Amount("axe_bronze");

            Say("Saving, adding 5000 xp + 3 logs + a banked axe, then loading...");
            if (watch) yield return new WaitForSeconds(1f);
            saving.Save(SaveFile);
            skills.AddXp(Skill.Woodcutting, 5000);
            skills.AddXp(Skill.Pyro, 5000);
            inventory.TryAdd("logs_pine", 3);
            inventory.TryAdd("axe_bronze", 1);
            bank.Deposit("axe_bronze", 1);
            saving.Load(SaveFile);
            yield return null;

            Check(Math.Abs(skills.GetXp(Skill.Woodcutting) - xp) < 0.001, "woodcutting xp not restored by load (" + skills.GetXp(Skill.Woodcutting) + " vs " + xp + ")");
            Check(Math.Abs(skills.GetXp(Skill.Pyro) - pyro) < 0.001, "pyro xp not restored by load");
            Check(inventory.Model.Count("logs_pine") == logs, "inventory not restored by load");
            Check(bank.Model.Amount("axe_bronze") == banked, "bank not restored by load (" + bank.Model.Amount("axe_bronze") + " vs " + banked + ")");
            saving.Delete(SaveFile);
            Say("Load restored the saved xp and inventory.");
        }
    }
}
