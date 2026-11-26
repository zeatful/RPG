using RPG.Core;
using RPG.Movement;
using UnityEngine;

namespace RPG.Items
{
    /// <summary>Player action: walk to a loot pickup and put it in the inventory.</summary>
    public class PickupCollector : MonoBehaviour, IAction
    {
        const float Range = 1.3f;

        Pickup target;
        Mover mover;
        ActionScheduler scheduler;
        Inventory inventory;

        private void Awake()
        {
            mover = GetComponent<Mover>();
            scheduler = GetComponent<ActionScheduler>();
            inventory = GetComponent<Inventory>();
        }

        public void Collect(Pickup pickup)
        {
            if (pickup == null) return;
            scheduler.StartAction(this);
            target = pickup;
        }

        private void Update()
        {
            if (target == null) return;

            if (RpgUtil.FlatDistance(transform.position, target.transform.position) > Range)
            {
                mover.MoveTo(target.transform.position, 1f);
                return;
            }

            mover.Cancel();
            ItemStack stack = target.Stack;
            int left = inventory.Model.Add(stack.ItemId, stack.Quantity);
            if (left == stack.Quantity)
            {
                GameLog.Add("You don't have enough inventory space.", GameLog.Warning);
            }
            else
            {
                GameLog.Add("You pick up: " + target.Label + ".");
                if (left > 0) target.Stack = new ItemStack(stack.ItemId, left);
                else Destroy(target.gameObject);
                inventory.NotifyChanged();
            }
            target = null;
        }

        public void Cancel()
        {
            target = null;
        }
    }
}
