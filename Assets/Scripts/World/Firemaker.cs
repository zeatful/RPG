using RPG.Content;
using RPG.Core;
using RPG.Items;
using RPG.Movement;
using RPG.Stats;
using UnityEngine;
using UnityEngine.AI;

namespace RPG.World
{
    /// <summary>
    /// Player action (Pyro skill): strike a flint and steel over some logs until they catch, then step aside.
    /// Higher logs need higher Pyro levels; the chance to catch each try rises with level.
    /// </summary>
    public class Firemaker : MonoBehaviour, IAction
    {
        const float AttemptInterval = 1.2f;

        string logId;
        float timer;

        Inventory inventory;
        SkillSet skills;
        ActionScheduler scheduler;
        Mover mover;
        Animator animator;
        Health health;

        public bool Busy { get { return logId != null; } }

        private void Awake()
        {
            inventory = GetComponent<Inventory>();
            skills = GetComponent<SkillSet>();
            scheduler = GetComponent<ActionScheduler>();
            mover = GetComponent<Mover>();
            animator = GetComponent<Animator>();
            health = GetComponent<Health>();
        }

        /// <summary>Returns false (with a chat message) if these logs can't be lit right now.</summary>
        public bool Light(string logs)
        {
            BurnInfo burn;
            if (!GameContent.Burnables.TryGetValue(logs, out burn)) return false;
            if (inventory.Model.Count(GameContent.FlintId) == 0)
            {
                GameLog.Add("You need a flint and steel to light these. General stores sell them.", GameLog.Warning);
                return false;
            }
            if (skills.GetLevel(Skill.Pyro) < burn.Level)
            {
                GameLog.Add("You need a Pyro level of " + burn.Level + " to light " + Name(logs) + ".", GameLog.Warning);
                return false;
            }
            if (Station.Nearest(StationKind.Fire, transform.position, 0.6f) != null || Station.Nearest(StationKind.Bank, transform.position, 1.2f) != null)
            {
                GameLog.Add("You can't light a fire here.", GameLog.Warning);
                return false;
            }
            scheduler.StartAction(this);
            mover.Cancel();
            logId = logs;
            timer = AttemptInterval * 0.5f;
            GameLog.Add("You strike the flint over the " + Name(logs) + "...");
            return true;
        }

        private void Update()
        {
            if (logId == null) return;
            if (health.IsDead()) { Cancel(); return; }

            timer += Time.deltaTime;
            if (timer < AttemptInterval) return;
            timer = 0;

            BurnInfo burn = GameContent.Burnables[logId];
            if (inventory.Model.Count(logId) == 0)
            {
                Stop();
                return;
            }
            if (Random.value >= GameContent.LightChance(skills.GetLevel(Skill.Pyro), burn.Level)) return;

            inventory.Model.Remove(logId, 1);
            inventory.NotifyChanged();
            ItemInfo info = GameContent.Item(logId);
            Campfire.Create(transform.position, burn.BurnSeconds, info != null ? info.ColorHex : null, null);
            skills.AddXp(Skill.Pyro, burn.Xp);
            GameLog.Add("The fire catches and the " + Name(logId) + " begin to burn.");
            Stop();
            StepAside();
        }

        void StepAside()
        {
            // like RuneScape: step west, or any free direction
            Vector3[] directions = { Vector3.left, Vector3.right, Vector3.back, Vector3.forward };
            foreach (Vector3 direction in directions)
            {
                Vector3 target = transform.position + direction * 1.6f;
                NavMeshHit hit;
                if (NavMesh.SamplePosition(target, out hit, 0.6f, NavMesh.AllAreas))
                {
                    mover.StartMoveAction(hit.position, 0.6f);
                    return;
                }
            }
        }

        static string Name(string itemId)
        {
            ItemInfo info = GameContent.Item(itemId);
            return info != null ? info.Name.ToLower() : itemId;
        }

        void Stop()
        {
            logId = null;
            scheduler.CancelCurrentAction();
        }

        public void Cancel()
        {
            logId = null;
        }
    }
}
