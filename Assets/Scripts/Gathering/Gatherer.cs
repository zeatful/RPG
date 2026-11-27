using RPG.Content;
using RPG.Core;
using RPG.Items;
using RPG.Movement;
using RPG.Stats;
using UnityEngine;

namespace RPG.Gathering
{
    /// <summary>
    /// Player action: walk to a tree or rock and swing at it every 1.2 seconds. Each swing does work
    /// (GameContent.SwingPower: tool power, more with levels above the requirement); a log/ore drops each
    /// time the work reaches the node's hardness. Needs an axe or pickaxe in the bag.
    /// </summary>
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
            ToolKind toolKind = info.Kind == NodeKind.Tree ? ToolKind.Axe : ToolKind.Pickaxe;
            if (inventory.BestTool(toolKind) == null)
            {
                string tool = toolKind == ToolKind.Axe ? "an axe" : "a pickaxe";
                GameLog.Add(inventory.HasAnyTool(toolKind)
                    ? "You don't have the level to use your " + (toolKind == ToolKind.Axe ? "axe" : "pickaxe") + "."
                    : "You need " + tool + " to " + node.ActionVerb.ToLower() + " this. General stores sell them.", GameLog.Warning);
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

            ItemInfo tool = inventory.BestTool(info.Kind == NodeKind.Tree ? ToolKind.Axe : ToolKind.Pickaxe);
            if (tool == null)
            {
                Stop();
                return;
            }
            double power = GameContent.SwingPower(skills.GetLevel(info.Skill), info.Level, tool.ToolPower);
            target.Work += power * Random.Range(0.75f, 1.25f);
            target.SinceSwing = 0;
            if (target.Work < info.Hardness) return;

            inventory.TryAdd(info.ItemId, 1);
            skills.AddXp(info.Skill, info.Xp);
            ItemInfo item = GameContent.Item(info.ItemId);
            GameLog.Add("You get some " + (item != null ? item.Name.ToLower() : info.ItemId) + ".");

            if (!string.IsNullOrEmpty(info.BonusItemId) && Random.value < info.BonusChance && inventory.TryAdd(info.BonusItemId, 1))
            {
                ItemInfo bonus = GameContent.Item(info.BonusItemId);
                GameLog.Add("You also find a " + (bonus != null ? bonus.Name.ToLower() : info.BonusItemId) + "!", GameLog.Rare);
            }

            if (target.TakeOne())
            {
                GameLog.Add(info.Kind == NodeKind.Tree ? "The tree falls." : "The rock is mined out.");
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
