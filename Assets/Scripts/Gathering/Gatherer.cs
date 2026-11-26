using RPG.Content;
using RPG.Core;
using RPG.Items;
using RPG.Movement;
using RPG.Stats;
using UnityEngine;

namespace RPG.Gathering
{
    /// <summary>Player action: walk to a tree or rock and gather from it every 1.2 seconds.</summary>
    public class Gatherer : MonoBehaviour, IAction
    {
        const float AttemptInterval = 1.2f;
        const float Range = 2.2f;

        ResourceNode target;
        float timer;

        Mover mover;
        ActionScheduler scheduler;
        Animator animator;
        SkillSet skills;
        Inventory inventory;
        Health health;

        private void Awake()
        {
            mover = GetComponent<Mover>();
            scheduler = GetComponent<ActionScheduler>();
            animator = GetComponent<Animator>();
            skills = GetComponent<SkillSet>();
            inventory = GetComponent<Inventory>();
            health = GetComponent<Health>();
        }

        public void Gather(ResourceNode node)
        {
            if (node == null) return;
            NodeInfo info = node.Info;

            if (node.IsDepleted)
            {
                GameLog.Add("There's nothing left to gather here right now.");
                return;
            }
            if (skills.GetLevel(info.Skill) < info.Level)
            {
                GameLog.Add("You need a " + info.Skill + " level of " + info.Level + " to " + node.ActionVerb.ToLower() + " this.", GameLog.Warning);
                return;
            }
            if (!inventory.Model.CanAdd(info.ItemId, 1))
            {
                GameLog.Add("Your inventory is too full to hold any more.", GameLog.Warning);
                return;
            }
            if (target == node) return;

            scheduler.StartAction(this);
            target = node;
            timer = 0;
            GameLog.Add(info.Kind == NodeKind.Tree ? "You swing your axe at the tree." : "You swing your pick at the rock.");
        }

        private void Update()
        {
            if (target == null || health.IsDead()) return;

            if (target.IsDepleted)
            {
                Stop();
                return;
            }

            if (RpgUtil.FlatDistance(transform.position, target.transform.position) > Range)
            {
                mover.MoveTo(target.transform.position, 1f);
                return;
            }

            mover.Cancel();
            Vector3 look = target.transform.position;
            look.y = transform.position.y;
            transform.LookAt(look);

            timer += Time.deltaTime;
            if (timer < AttemptInterval) return;
            timer = 0;

            // Reuse the attack animation as a chop/mine swing. Its Hit() event is ignored because
            // the Fighter has no target while we're gathering.
            animator.ResetTrigger("stopAttack");
            animator.SetTrigger("attack");
            Attempt();
        }

        void Attempt()
        {
            NodeInfo info = target.Info;
            if (!inventory.Model.CanAdd(info.ItemId, 1))
            {
                GameLog.Add("Your inventory is too full to hold any more.", GameLog.Warning);
                Stop();
                return;
            }

            double chance = GameContent.GatherChance(skills.GetLevel(info.Skill), info.Level);
            if (Random.value >= chance) return;

            inventory.TryAdd(info.ItemId, 1);
            skills.AddXp(info.Skill, info.Xp);
            ItemInfo item = GameContent.Item(info.ItemId);
            GameLog.Add("You get some " + (item != null ? item.Name.ToLower() : info.ItemId) + ".");

            if (!string.IsNullOrEmpty(info.BonusItemId) && Random.value < info.BonusChance && inventory.TryAdd(info.BonusItemId, 1))
            {
                ItemInfo bonus = GameContent.Item(info.BonusItemId);
                GameLog.Add("You also find a " + (bonus != null ? bonus.Name.ToLower() : info.BonusItemId) + "!", GameLog.Rare);
            }

            if (Random.value < info.DepleteChance)
            {
                target.Deplete();
                Stop();
            }
        }

        void Stop()
        {
            scheduler.CancelCurrentAction();
            target = null;
        }

        public void Cancel()
        {
            target = null;
            animator.ResetTrigger("attack");
            animator.SetTrigger("stopAttack");
        }
    }
}
