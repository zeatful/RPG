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
            foreach (string title in new[] { "Check the world spawned", "Chop a pine tree, fletch arrows", "Kill an enemy, pick up loot", "Survive Old Gullet's slam", "Save, change, load" })
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
            yield return Step(1, "gathering", TestGathering());
            yield return Step(2, "combat and loot", TestCombat());
            yield return Step(3, "boss", TestBoss());
            yield return Step(4, "save and load", TestSaveLoad());

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

        IEnumerator GoNear(Vector3 position, float offset)
        {
            Vector3 target = PointNear(position, offset);
            if (!watch)
            {
                player.GetComponent<NavMeshAgent>().Warp(target);
                yield break;
            }
            player.GetComponent<RPG.Movement.Mover>().StartMoveAction(target, 1f);
            float timeout = Time.time + 30f;
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
            Say("Walking to a pine tree to chop it...");
            if (!watch) yield return GoNear(tree.transform.position, 1.8f);
            player.GetComponent<Gatherer>().Gather(tree);

            float timeout = Time.time + Limit(60f);
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
            Say("Attacking a " + target.Info.Name + " (level " + target.Info.Level + ")...");

            Health playerHealth = player.GetComponent<Health>();
            playerHealth.SetMaxHealth(100000, true);
            SkillSet skills = player.GetComponent<SkillSet>();
            int hits = 0;
            Action<GameObject, GameObject, int> counter = (attacker, defender, damage) =>
            {
                if (attacker == player) hits++;
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

        IEnumerator TestBoss()
        {
            BossController boss = BossController.Active;
            if (boss == null) yield break;
            Health playerHealth = player.GetComponent<Health>();
            playerHealth.SetMaxHealth(100000, true);
            float startHp = playerHealth.GetHealth();

            Say("Walking up to Old Gullet to take a slam (test HP boosted)...");
            yield return GoNear(boss.transform.position, 2f);
            float timeout = Time.time + Limit(30f);
            while (Time.time < timeout && playerHealth.GetHealth() >= startHp) yield return null;
            Check(playerHealth.GetHealth() < startHp, "boss never damaged the player");
            Say("Old Gullet hit for " + (startHp - playerHealth.GetHealth()) + ". Heading back to the start!");

            // walk away from the boss so it stops fighting us
            yield return GoNear(playerStart, 0f);
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

            Say("Saving, adding 5000 xp + 3 logs, then loading...");
            if (watch) yield return new WaitForSeconds(1f);
            saving.Save(SaveFile);
            skills.AddXp(Skill.Woodcutting, 5000);
            inventory.TryAdd("logs_pine", 3);
            saving.Load(SaveFile);
            yield return null;

            Check(Math.Abs(skills.GetXp(Skill.Woodcutting) - xp) < 0.001, "woodcutting xp not restored by load (" + skills.GetXp(Skill.Woodcutting) + " vs " + xp + ")");
            Check(inventory.Model.Count("logs_pine") == logs, "inventory not restored by load");
            saving.Delete(SaveFile);
            Say("Load restored the saved xp and inventory.");
        }
    }
}
