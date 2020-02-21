using UnityEngine;
using RPG.Movement;

namespace RPG.Combat
{
    public class Fighter : MonoBehaviour
    {
        [SerializeField] float weaponRange = 2f;

        Transform target;

        Mover mover;

        private void Start()
        {
            mover = GetComponent<Mover>();
        }

        private void Update()
        {
            if (target == null) return;

            // ensure there is already a target
            if (target != null && !IsInRange())
            {
                // move until in weapon range
                mover.MoveTo(target.position);
            }
            else
            {
                // in weapon range, stop moving
                mover.Stop();
            }
        }

        // determine if fighter is in range of target
        private bool IsInRange()
        {
            return Vector3.Distance(transform.position, target.position) < weaponRange;
        }

        public void Attack(CombatTarget combatTarget)
        {
            target = combatTarget.transform;
        }

        public void Cancel()
        {
            target = null;
        }
    }
}