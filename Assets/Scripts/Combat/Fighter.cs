using UnityEngine;
using RPG.Movement;
using RPG.Core;
using RPG.Stats;

namespace RPG.Combat
{
    public class Fighter : MonoBehaviour, IAction
    {
        [SerializeField] float weaponRange = 2f;
        [SerializeField] float timeBetweenAttacks = 1f;
        [SerializeField] float weaponDamage = 5f;
        float timeSinceLastAttack = Mathf.Infinity;

        // >1 attacks faster (used by boss enrage).
        public float AttackSpeedMultiplier = 1f;

        Health target;
        Mover mover;
        ActionScheduler actionScheduler;
        Animator animator;

        private void Start()
        {
            mover = GetComponent<Mover>();
            actionScheduler = GetComponent<ActionScheduler>();
            animator = GetComponent<Animator>();
        }

        private void Update()
        {
            timeSinceLastAttack += Time.deltaTime;

            if (target == null) return;

            // stop fighting a dead target so we don't resume when it respawns
            if (target.IsDead())
            {
                Cancel();
                return;
            }

            // ensure there is already a target
            if (target != null && !IsInRange())
            {
                // move until in weapon range
                mover.MoveTo(target.transform.position, 1f);
            }
            else
            {
                // in weapon range, stop moving
                mover.Cancel();
                AttackBehavior();
            }
        }

        private void AttackBehavior()
        {
            transform.LookAt(target.transform);

            if (timeSinceLastAttack > timeBetweenAttacks / Mathf.Max(0.1f, AttackSpeedMultiplier))
            {
                TriggerAttack();
                timeSinceLastAttack = 0;
            }
        }

        private void TriggerAttack()
        {
            // This will trigger the Hit() event
            animator.ResetTrigger("stopAttack");
            animator.SetTrigger("attack");
        }

        // Fired for every swing that lands its animation: attacker, defender, damage (0 = miss).
        public static event System.Action<GameObject, GameObject, int> onAttackResolved;

        // Lets non-Fighter damage sources (boss slams) show hitsplats and award defense XP.
        public static void RaiseAttackResolved(GameObject attacker, GameObject defender, int damage)
        {
            if (onAttackResolved != null) onAttackResolved(attacker, defender, damage);
        }

        // Animation Event
        void Hit()
        {
            if (target == null || target.IsDead()) return;

            ICombatStats mine = GetComponent<ICombatStats>();
            if (mine == null)
            {
                // No stats component: keep the original tutorial behaviour.
                target.TakeDamage(weaponDamage, gameObject);
                return;
            }

            string ammoMessage;
            if (!mine.TryUseAmmo(out ammoMessage))
            {
                if (!string.IsNullOrEmpty(ammoMessage)) GameLog.Add(ammoMessage, GameLog.Warning);
                Cancel();
                return;
            }
            if (mine.Style != CombatStyle.Melee)
            {
                Projectile.Launch(transform.position + Vector3.up * 1.3f, target.transform, mine.Style);
            }

            ICombatStats theirs = target.GetComponent<ICombatStats>();
            int defenseLevel = theirs != null ? theirs.DefenseLevel : 1;
            int defenseBonus = theirs != null ? theirs.DefenseBonus : 0;

            float chance = CombatMath.HitChance(mine.AttackLevel, mine.AttackBonus, defenseLevel, defenseBonus);
            int damage = 0;
            if (Random.value < chance)
            {
                int maxHit = CombatMath.MaxHit(mine.StrengthLevel, mine.StrengthBonus);
                damage = CombatMath.RollDamage(maxHit, Random.value * 0.9999f);
                damage = Mathf.Min(damage, Mathf.CeilToInt(target.GetHealth()));
            }

            Health defender = target;
            defender.TakeDamage(damage, gameObject);
            if (onAttackResolved != null) onAttackResolved(gameObject, defender.gameObject, damage);
        }

        // determine if fighter is in range of target
        private bool IsInRange()
        {
            float range = weaponRange;
            ICombatStats stats = GetComponent<ICombatStats>();
            if (stats != null && stats.AttackRange > 0) range = stats.AttackRange;
            return Vector3.Distance(transform.position, target.transform.position) < range;
        }

        public bool CanAttack(GameObject combatTarget)
        {
            if (combatTarget == null) return false;

            Health targetToTest = combatTarget.GetComponent<Health>();
            return (targetToTest != null && !targetToTest.IsDead());
        }

        public void Attack(GameObject combatTarget)
        {
            actionScheduler.StartAction(this);
            target = combatTarget.GetComponent<Health>();
        }

        public void Cancel()
        {
            StopAttack();
            target = null;
            mover.Cancel();
        }

        private void StopAttack()
        {
            animator.ResetTrigger("attack");
            animator.SetTrigger("stopAttack");
        }
    }
}