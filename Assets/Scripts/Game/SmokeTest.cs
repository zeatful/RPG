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
using UnityEngine;
using UnityEngine.AI;

namespace RPG.Game
{
    /// <summary>
    /// Automated play-through used to verify a real build: launch the game with -rpgSmokeTest and it
    /// chops a tree, kills an enemy, picks up loot, gets slammed by the boss and round-trips a save,
    /// then quits with exit code 0 (pass) or 1 (fail). Never active in normal play.
    /// </summary>
    public class SmokeTest : MonoBehaviour
    {
        const string SaveFile = "rpg_smoketest";

        public static bool Requested
        {
            get { return Array.IndexOf(Environment.GetCommandLineArgs(), "-rpgSmokeTest") >= 0; }
        }

        readonly List<string> failures = new List<string>();
        readonly List<string> errors = new List<string>();
        GameObject player;

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
            Log("starting");
            yield return new WaitForSeconds(1f);
            player = GameObject.FindWithTag("Player");
            Time.timeScale = 3f;

            yield return Step("setup", CheckSetup());
            yield return Step("gathering", TestGathering());
            yield return Step("combat and loot", TestCombat());
            yield return Step("boss", TestBoss());
            yield return Step("save and load", TestSaveLoad());

            foreach (string error in errors) failures.Add("logged " + error);
            Time.timeScale = 1f;
            if (failures.Count == 0)
            {
                Log("PASS");
            }
            else
            {
                foreach (string failure in failures) Log("FAIL " + failure);
            }
            Application.Quit(failures.Count == 0 ? 0 : 1);
        }

        IEnumerator Step(string name, IEnumerator body)
        {
            int before = failures.Count;
            Log("step: " + name);
            yield return StartCoroutine(body);
            Log(name + (failures.Count == before ? " ok" : " FAILED"));
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
            Check(EnemyProfile.All.Count >= 2, "expected enemies with profiles, found " + EnemyProfile.All.Count);
            Check(BossController.Active != null, "boss was not spawned");
            int nodes = FindObjectsByType<ResourceNode>(FindObjectsSortMode.None).Length;
            Check(nodes >= 10, "expected at least 10 resource nodes, found " + nodes);
            Check(DayNightCycle.Instance != null, "no day/night cycle");
            Log("enemies " + EnemyProfile.All.Count + ", nodes " + nodes);
            if (BossController.Active != null)
            {
                Vector3 lair = BossController.Active.transform.position;
                float nearestNode = float.MaxValue;
                foreach (ResourceNode node in FindObjectsByType<ResourceNode>(FindObjectsSortMode.None))
                {
                    nearestNode = Mathf.Min(nearestNode, RpgUtil.FlatDistance(lair, node.transform.position));
                }
                float fromStart = RpgUtil.FlatDistance(lair, player.transform.position);
                Log("boss lair " + lair + ": " + fromStart + " m from player start, nearest node " + nearestNode + " m");
                Check(fromStart > 16f, "boss lair is only " + fromStart + " m from the player start");
                Check(nearestNode > 16f, "a resource node is only " + nearestNode + " m from the boss lair");
            }
            yield break;
        }

        void WarpPlayerNear(Vector3 position, float offset)
        {
            Vector3 direction = player.transform.position - position;
            direction.y = 0;
            if (direction.sqrMagnitude < 0.01f) direction = Vector3.forward;
            Vector3 target = position + direction.normalized * offset;
            NavMeshHit hit;
            if (NavMesh.SamplePosition(target, out hit, 3f, NavMesh.AllAreas)) target = hit.position;
            player.GetComponent<NavMeshAgent>().Warp(target);
        }

        IEnumerator TestGathering()
        {
            ResourceNode tree = null;
            foreach (ResourceNode node in FindObjectsByType<ResourceNode>(FindObjectsSortMode.None))
            {
                if (node.Info.Id == "pine" && !node.IsDepleted) tree = node;
            }
            Check(tree != null, "no pine tree to chop");
            if (tree == null) yield break;

            Inventory inventory = player.GetComponent<Inventory>();
            SkillSet skills = player.GetComponent<SkillSet>();
            string logId = tree.Info.ItemId;
            int logsBefore = inventory.Model.Count(logId);
            WarpPlayerNear(tree.transform.position, 1.8f);
            player.GetComponent<Gatherer>().Gather(tree);

            float timeout = Time.time + 60f;
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
            int arrowsBefore = inventory.Model.Count(arrows.Output.ItemId);
            Check(inventory.CanMake(arrows), "cannot craft pine arrows from fresh logs");
            inventory.Make(arrows);
            Check(inventory.Model.Count(arrows.Output.ItemId) > arrowsBefore, "crafting produced no arrows");
            Check(skills.GetXp(Skill.Crafting) > 0, "no crafting xp");
        }

        IEnumerator TestCombat()
        {
            EnemyProfile target = null;
            foreach (EnemyProfile enemy in EnemyProfile.All)
            {
                if (enemy.GetComponent<BossController>() != null || enemy.Health.IsDead()) continue;
                if (target == null || enemy.Info.Level < target.Info.Level) target = enemy;
            }
            Check(target != null, "no enemy to fight");
            if (target == null) yield break;
            Log("fighting " + target.Info.Name);

            Health playerHealth = player.GetComponent<Health>();
            playerHealth.SetMaxHealth(100000, true);
            SkillSet skills = player.GetComponent<SkillSet>();
            int hits = 0;
            Action<GameObject, GameObject, int> counter = (attacker, defender, damage) =>
            {
                if (attacker == player) hits++;
            };
            Fighter.onAttackResolved += counter;

            WarpPlayerNear(target.transform.position, 1.5f);
            Fighter fighter = player.GetComponent<Fighter>();
            float timeout = Time.time + 120f;
            while (Time.time < timeout && !target.Health.IsDead())
            {
                // keep re-issuing the attack in case something interrupted it
                if (fighter.CanAttack(target.gameObject)) fighter.Attack(target.gameObject);
                yield return new WaitForSeconds(0.5f);
            }
            Fighter.onAttackResolved -= counter;

            Check(hits > 0, "player attacks never resolved (animation Hit events not firing?)");
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
            Log("picking up " + loot.Label);
            player.GetComponent<PickupCollector>().Collect(loot);
            timeout = Time.time + 30f;
            while (Time.time < timeout && inventory.Model.Count(itemId) == before) yield return null;
            Check(inventory.Model.Count(itemId) > before, "could not pick up " + itemId);
        }

        IEnumerator TestBoss()
        {
            BossController boss = BossController.Active;
            if (boss == null) yield break;
            Health playerHealth = player.GetComponent<Health>();
            playerHealth.SetMaxHealth(100000, true);
            float startHp = playerHealth.GetHealth();

            WarpPlayerNear(boss.transform.position, 2f);
            float timeout = Time.time + 30f;
            while (Time.time < timeout && playerHealth.GetHealth() >= startHp) yield return null;
            Check(playerHealth.GetHealth() < startHp, "boss never damaged the player");
            Log("boss dealt " + (startHp - playerHealth.GetHealth()) + " damage");

            // walk away from the boss so it stops fighting us
            Vector3 home = boss.transform.position + (player.transform.position - boss.transform.position).normalized * 30f;
            WarpPlayerNear(home, 0f);
            player.GetComponent<ActionScheduler>().CancelCurrentAction();
        }

        IEnumerator TestSaveLoad()
        {
            SavingSystem saving = FindFirstObjectByType<SavingSystem>();
            Check(saving != null, "no SavingSystem in scene");
            if (saving == null) yield break;

            SkillSet skills = player.GetComponent<SkillSet>();
            Inventory inventory = player.GetComponent<Inventory>();
            double xp = skills.GetXp(Skill.Woodcutting);
            int logs = inventory.Model.Count("logs_pine");

            saving.Save(SaveFile);
            skills.AddXp(Skill.Woodcutting, 5000);
            inventory.TryAdd("logs_pine", 3);
            saving.Load(SaveFile);
            yield return null;

            Check(Math.Abs(skills.GetXp(Skill.Woodcutting) - xp) < 0.001, "woodcutting xp not restored by load (" + skills.GetXp(Skill.Woodcutting) + " vs " + xp + ")");
            Check(inventory.Model.Count("logs_pine") == logs, "inventory not restored by load");
            saving.Delete(SaveFile);
        }
    }
}
