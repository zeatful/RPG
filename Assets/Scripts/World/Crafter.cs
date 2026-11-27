using RPG.Core;
using RPG.Items;
using UnityEngine;

namespace RPG.World
{
    /// <summary>
    /// Player action: make a recipe several times in a row ("Make all"), one every few seconds,
    /// at the station it needs. Stops when you move, run out of materials or wander off.
    /// </summary>
    public class Crafter : MonoBehaviour, IAction
    {
        Recipe recipe;
        int remaining;
        float timer;

        Inventory inventory;
        ActionScheduler scheduler;
        Animator animator;
        Health health;

        public bool Busy { get { return recipe != null; } }
        public Recipe Current { get { return recipe; } }
        public int Remaining { get { return remaining; } }

        private void Awake()
        {
            inventory = GetComponent<Inventory>();
            scheduler = GetComponent<ActionScheduler>();
            animator = GetComponent<Animator>();
            health = GetComponent<Health>();
        }

        public static float Interval(Recipe recipe)
        {
            switch (recipe.Station)
            {
                case StationKind.Furnace: return 1.8f;
                case StationKind.Anvil: return 2.0f;
                case StationKind.Fire: return 1.8f;
            }
            return 1.2f;
        }

        public void Make(Recipe toMake, int count)
        {
            if (toMake == null || count <= 0) return;
            if (!Station.IsNear(toMake.Station, transform.position))
            {
                GameLog.Add("You need to be next to " + Station.Describe(toMake.Station) + " to do that.", GameLog.Warning);
                return;
            }
            if (!inventory.CanMake(toMake))
            {
                // let Make() explain why
                inventory.Make(toMake);
                return;
            }
            scheduler.StartAction(this);
            recipe = toMake;
            remaining = count;
            timer = Interval(toMake) * 0.6f;
        }

        private void Update()
        {
            if (recipe == null) return;
            if (health.IsDead()) { Stop(); return; }

            timer += Time.deltaTime;
            if (timer < Interval(recipe)) return;
            timer = 0;

            if (!Station.IsNear(recipe.Station, transform.position) || !inventory.CanMake(recipe))
            {
                Stop();
                return;
            }
            if (recipe.Station == StationKind.Anvil && animator != null)
            {
                animator.ResetTrigger("stopAttack");
                animator.SetTrigger("attack");
            }
            inventory.Make(recipe);
            remaining--;
            if (remaining <= 0 || !inventory.CanMake(recipe)) Stop();
        }

        void Stop()
        {
            recipe = null;
            remaining = 0;
            scheduler.CancelCurrentAction();
        }

        public void Cancel()
        {
            recipe = null;
            remaining = 0;
            if (animator != null)
            {
                animator.ResetTrigger("attack");
                animator.SetTrigger("stopAttack");
            }
        }
    }
}
