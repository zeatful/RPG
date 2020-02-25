using UnityEngine;
using UnityEngine.AI;
using RPG.Combat;
using RPG.Core;

namespace RPG.Movement
{
    public class Mover : MonoBehaviour, IAction
    {
        // player target to navigate towards
        [SerializeField] Transform target;
        [SerializeField] float maxSpeed = 6f;

        NavMeshAgent navMeshAgent;
        Animator animator;
        Fighter fighter;
        ActionScheduler actionScheduler;
        Health health;

        private void Start()
        {
            animator = GetComponent<Animator>();
            navMeshAgent = GetComponent<NavMeshAgent>();
            fighter = GetComponent<Fighter>();
            actionScheduler = GetComponent<ActionScheduler>();
            health = GetComponent<Health>();
        }

        // Update is called once per frame
        void Update()
        {
            navMeshAgent.enabled = !health.IsDead();
            UpdateAnimator();
        }

        public void StartMoveAction(Vector3 destination, float speedFaction)
        {
            actionScheduler.StartAction(this);
            MoveTo(destination, speedFaction);
        }

        public void MoveTo(Vector3 destination, float speedFaction)
        {
            navMeshAgent.destination = destination;
            // ensure the number is between 0 and 1 only
            navMeshAgent.speed = maxSpeed * Mathf.Clamp01(speedFaction);
            navMeshAgent.isStopped = false;
        }

        public void Cancel()
        {
            navMeshAgent.isStopped = true;
        }

        private void UpdateAnimator()
        {
            // Convert global velocity to local velocity for the animator
            Vector3 velocity = navMeshAgent.velocity;
            Vector3 localVelocity = transform.InverseTransformDirection(velocity);
            float speed = localVelocity.z;
            animator.SetFloat("ForwardSpeed", speed);
        }
    }
}