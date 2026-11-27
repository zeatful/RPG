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

        // World enemies: passive ones only fight back after being hit; everyone gives up and walks home
        // when dragged too far from where they spawned; with no patrol path they wander nearby.
        bool aggressive = true;
        float leashDistance = 0f;
        float wanderRadius = 0f;
        float wanderTimer;
        bool returningHome;
        const float ProvokedSeconds = 12f;

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

        // Used when spawning enemies at runtime (e.g. the boss). Call before Start; null = guard the spawn point.
        public void SetPatrolPath(PatrolPath path)
        {
            patrolPath = path;
        }

        public void SetChaseDistance(float distance)
        {
            chaseDistance = distance;
        }

        public void ConfigureWorld(bool isAggressive, float chase, float leash, float wander)
        {
            aggressive = isAggressive;
            chaseDistance = chase;
            leashDistance = leash;
            wanderRadius = wander;
            wanderTimer = UnityEngine.Random.Range(2f, 8f);
        }

        private void Update()
        {
            if (health.IsDead()) return;

            if (leashDistance > 0)
            {
                float fromHome = Vector3.Distance(transform.position, guardLocation);
                if (fromHome > leashDistance) returningHome = true;
                if (returningHome)
                {
                    if (fromHome < 2f) returningHome = false;
                    else
                    {
                        timeSinceLastSawPlayer = Mathf.Infinity;
                        mover.StartMoveAction(guardLocation, 0.8f);
                        UpdateTimers();
                        return;
                    }
                }
            }

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

            if (patrolPath == null && wanderRadius > 0)
            {
                wanderTimer -= Time.deltaTime;
                if (wanderTimer > 0) return;
                wanderTimer = UnityEngine.Random.Range(5f, 12f);
                Vector2 offset = UnityEngine.Random.insideUnitCircle * wanderRadius;
                Vector3 spot = guardLocation + new Vector3(offset.x, 0, offset.y);
                UnityEngine.AI.NavMeshHit hit;
                if (UnityEngine.AI.NavMesh.SamplePosition(spot, out hit, 2f, UnityEngine.AI.NavMesh.AllAreas))
                {
                    mover.StartMoveAction(hit.position, patrolSpeedFaction);
                }
                return;
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
            // fight back after being hit, even if passive (but not across the map)
            if (health.TimeSinceLastDamaged < ProvokedSeconds && distanceToPlayer < Mathf.Max(chaseDistance, 12f)) return true;
            if (!aggressive) return false;
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