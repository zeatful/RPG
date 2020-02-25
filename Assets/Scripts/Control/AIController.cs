namespace RPG.Control
{
    using System;
    using RPG.Combat;
    using RPG.Core;
    using RPG.Movement;
    using UnityEngine;

    public class AIController : MonoBehaviour
    {
        [SerializeField] PatrolPath patrolPath;
        [SerializeField] float chaseDistance = 5f;
        [SerializeField] float suspicionTime = 5f;
        [SerializeField] float waypointTolerance = 1f;
        [SerializeField] float waypointDwellTime = 3f;

        [Range(0, 1)]
        [SerializeField] float patrolSpeedFaction = 0.2f;

        GameObject player;
        Fighter fighter;
        Health health;
        Mover mover;
        ActionScheduler actionScheduler;

        Vector3 guardLocation;
        float timeSinceLastSawPlayer = Mathf.Infinity;
        float timeSinceArrivedAtWaypoint = Mathf.Infinity;
        int currentWaypointIndex = 0;

        private void Start()
        {
            fighter = GetComponent<Fighter>();
            player = GameObject.FindWithTag("Player");
            health = GetComponent<Health>();
            mover = GetComponent<Mover>();
            actionScheduler = GetComponent<ActionScheduler>();

            guardLocation = transform.position;

            // ensure we patrol at the nearest waypoint in the patrol path
            if (patrolPath != null)
            {
                currentWaypointIndex = patrolPath.GetClosestWayPointIndex(transform.position);
            }
        }

        private void Update()
        {
            if (health.IsDead()) return;

            if (InAttackRange() && fighter.CanAttack(player))
            {
                AttackBehavior();
            }
            else if (timeSinceLastSawPlayer < suspicionTime)
            {
                SuspicionBehavior();
            }
            else
            {
                PatrolBehavior();
            }

            UpdateTimers();
        }

        private void UpdateTimers()
        {
            timeSinceLastSawPlayer += Time.deltaTime;
            timeSinceArrivedAtWaypoint += Time.deltaTime;
        }

        private void PatrolBehavior()
        {
            Vector3 nextPosition = guardLocation;

            if (patrolPath != null)
            {
                if (AtWayPoint())
                {
                    timeSinceArrivedAtWaypoint = 0;
                    CycleWaypoint();
                }
                nextPosition = GetCurrentWayPoint();
            }

            if (timeSinceArrivedAtWaypoint > waypointDwellTime)
            {
                mover.StartMoveAction(nextPosition, patrolSpeedFaction);
            }
        }

        private void CycleWaypoint()
        {
            currentWaypointIndex = patrolPath.GetNexWayPointIndex(currentWaypointIndex);
        }

        private bool AtWayPoint()
        {
            float distanceToWaypoint = Vector3.Distance(transform.position, GetCurrentWayPoint());
            return (distanceToWaypoint < waypointTolerance);
        }

        private Vector3 GetNextWayPoint()
        {
            return patrolPath.GetNextWayPoint(currentWaypointIndex);
        }

        private Vector3 GetCurrentWayPoint()
        {
            return patrolPath.GetWaypoint(currentWaypointIndex);
        }

        private void SuspicionBehavior()
        {
            actionScheduler.CancelCurrentAction();
        }

        private void AttackBehavior()
        {
            timeSinceLastSawPlayer = 0;
            fighter.Attack(player);
        }

        private bool InAttackRange()
        {
            float distanceToPlayer = Vector3.Distance(player.transform.position, transform.position);
            return (distanceToPlayer < chaseDistance);
        }

        // Called by Unity
        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.blue;
            Gizmos.DrawWireSphere(transform.position, chaseDistance);
        }
    }
}