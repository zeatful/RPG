using System.Collections.Generic;
using RPG.Content;
using RPG.Control;
using RPG.Core;
using RPG.Drops;
using RPG.Gathering;
using RPG.Items;
using RPG.Saving;
using RPG.Stats;
using RPG.UI;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

namespace RPG.Game
{
    /// <summary>
    /// Boots the RPG systems into any scene that has a Player, with no manual setup:
    /// adds skills/inventory/gathering to the player, gives enemies profiles and drop tables,
    /// spawns trees and rocks on the NavMesh near the player, and adds the HUD.
    /// </summary>
    public class GameDirector : MonoBehaviour
    {
        const int LayoutSeed = 20261126;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
            EnsureDirector();
        }

        static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            EnsureDirector();
        }

        static void EnsureDirector()
        {
            if (FindFirstObjectByType<GameDirector>() != null) return;
            if (GameObject.FindWithTag("Player") == null) return;
            new GameObject("RPG Systems").AddComponent<GameDirector>();
        }

        private void Awake()
        {
            GameObject player = GameObject.FindWithTag("Player");
            if (player == null) return;

            SetupPlayer(player);
            GameObject bossTemplate = FindBossTemplate();
            SetupEnemies();
            SpawnBoss(bossTemplate, player.transform.position);
            gameObject.AddComponent<DayNightCycle>();
            SpawnNodes(player.transform.position);
            gameObject.AddComponent<RpgHud>();

            GameLog.Clear();
            GameLog.Add("Welcome! Click trees and rocks to gather, enemies to fight, loot to pick it up.");
            GameLog.Add("I = inventory and crafting, K = skills, S / L = save / load.");
        }

        static T Ensure<T>(GameObject go) where T : Component
        {
            T component = go.GetComponent<T>();
            return component != null ? component : go.AddComponent<T>();
        }

        void SetupPlayer(GameObject player)
        {
            // Order matters: later components look up earlier ones in Awake.
            Ensure<SkillSet>(player);
            Inventory inventory = Ensure<Inventory>(player);
            Ensure<DropLuck>(player);
            Ensure<PlayerStats>(player);
            Ensure<Gatherer>(player);
            Ensure<PickupCollector>(player);

            if (inventory.IsEmpty())
            {
                inventory.Model.SetEquipped("sword_bronze", null);
                inventory.TryAdd("raw_meat", 2);
            }
        }

        void SetupEnemies()
        {
            List<AIController> enemies = new List<AIController>(FindObjectsByType<AIController>(FindObjectsSortMode.None));
            // stable order so the same enemy gets the same profile every run
            enemies.Sort((a, b) =>
            {
                int byX = a.transform.position.x.CompareTo(b.transform.position.x);
                return byX != 0 ? byX : a.transform.position.z.CompareTo(b.transform.position.z);
            });

            for (int i = 0; i < enemies.Count; i++)
            {
                string id = GameContent.SceneEnemyRotation[i % GameContent.SceneEnemyRotation.Length];
                EnemyInfo info;
                if (!GameContent.Enemies.TryGetValue(id, out info)) continue;
                EnemyProfile profile = Ensure<EnemyProfile>(enemies[i].gameObject);
                profile.Configure(info);
            }
        }

        void SpawnNodes(Vector3 center)
        {
            System.Random random = new System.Random(LayoutSeed);
            List<Vector3> placed = new List<Vector3>();

            // Trees spread over one arc around the start point, rocks over another.
            SpawnGroup("pine", 5, center, 20f, 200f, random, placed);
            SpawnGroup("oak", 3, center, 20f, 200f, random, placed);
            SpawnGroup("willow", 2, center, 20f, 200f, random, placed);
            SpawnGroup("copper", 3, center, 220f, 340f, random, placed);
            SpawnGroup("tin", 3, center, 220f, 340f, random, placed);
            SpawnGroup("iron", 2, center, 220f, 340f, random, placed);
        }

        void SpawnGroup(string nodeId, int count, Vector3 center, float minAngle, float maxAngle, System.Random random, List<Vector3> placed)
        {
            NodeInfo info;
            if (!GameContent.Nodes.TryGetValue(nodeId, out info)) return;

            int spawned = 0;
            for (int attempt = 0; attempt < count * 40 && spawned < count; attempt++)
            {
                float angle = Mathf.Lerp(minAngle, maxAngle, (float)random.NextDouble()) * Mathf.Deg2Rad;
                float distance = Mathf.Lerp(6f, 20f, (float)random.NextDouble());
                Vector3 candidate = center + new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle)) * distance;

                NavMeshHit hit;
                if (!NavMesh.SamplePosition(candidate, out hit, 2f, NavMesh.AllAreas)) continue;
                if (TooClose(hit.position, placed, info.Kind == NodeKind.Tree ? 3.5f : 2.5f)) continue;

                ResourceNode.Create(info, hit.position, (float)random.NextDouble() * 360f);
                placed.Add(hit.position);
                spawned++;
            }
        }

        // Clone an enemy from the scene before profiles/tints are applied, to use as the boss body.
        GameObject FindBossTemplate()
        {
            AIController[] enemies = FindObjectsByType<AIController>(FindObjectsSortMode.None);
            if (enemies.Length == 0) return null;
            GameObject template = Instantiate(enemies[0].gameObject);
            template.SetActive(false);
            template.name = "Boss Template";
            return template;
        }

        void SpawnBoss(GameObject template, Vector3 playerStart)
        {
            if (template == null) return;
            EnemyInfo info;
            if (!GameContent.Enemies.TryGetValue(GameContent.BossId, out info)) return;

            // Old Gullet lurks by the bridge if there is one, otherwise a short walk from the start.
            Vector3 lair = playerStart + new Vector3(18f, 0, 18f);
            GameObject bridgePath = GameObject.Find("BridgePatrolPath");
            if (bridgePath != null && bridgePath.transform.childCount > 0)
            {
                Vector3 sum = Vector3.zero;
                foreach (Transform waypoint in bridgePath.transform) sum += waypoint.position;
                lair = sum / bridgePath.transform.childCount;
            }
            NavMeshHit hit;
            if (NavMesh.SamplePosition(lair, out hit, 6f, NavMesh.AllAreas)) lair = hit.position;

            template.transform.position = lair;
            // Never save the boss: its SaveableEntity id would clash with the enemy it was cloned from.
            SaveableEntity saveable = template.GetComponent<SaveableEntity>();
            if (saveable != null) Destroy(saveable);

            AIController ai = template.GetComponent<AIController>();
            ai.SetPatrolPath(null);
            ai.SetChaseDistance(7f);
            template.name = info.Name;
            template.SetActive(true);

            EnemyProfile profile = Ensure<EnemyProfile>(template);
            profile.Configure(info);
            template.AddComponent<BossController>();
        }

        static bool TooClose(Vector3 position, List<Vector3> placed, float minDistance)
        {
            foreach (Vector3 other in placed)
            {
                if (Vector3.Distance(position, other) < minDistance) return true;
            }
            return false;
        }
    }
}
