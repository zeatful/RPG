using UnityEngine;
using UnityEngine.AI;
using RPG.Saving;
using RPG.Combat;
using RPG.Core;

namespace RPG.Movement
{
    public class Mover : MonoBehaviour, IAction, ISaveable
    {
        // player target to navigate towards
        [SerializeField] Transform target;
        [SerializeField] float maxSpeed = 6f;

        NavMeshAgent navMeshAgent;
        Animator animator;
        Fighter fighter;
        ActionScheduler actionScheduler;
        Health health;
        RPG.Stats.Stamina stamina;
        float speedFraction = 1f;

        private void Start()
        {
            animator = GetComponent<Animator>();
            navMeshAgent = GetComponent<NavMeshAgent>();
            fighter = GetComponent<Fighter>();
            actionScheduler = GetComponent<ActionScheduler>();
            health = GetComponent<Health>();
            stamina = GetComponent<RPG.Stats.Stamina>();
        }

        // Update is called once per frame
        void Update()
        {
            navMeshAgent.enabled = !health.IsDead();
            // the player walks or runs depending on stamina (picked up live when toggled)
            if (stamina != null && navMeshAgent.enabled) navMeshAgent.speed = maxSpeed * speedFraction * stamina.SpeedMultiplier;
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
            speedFraction = Mathf.Clamp01(speedFaction);
            navMeshAgent.speed = maxSpeed * speedFraction * (stamina != null ? stamina.SpeedMultiplier : 1f);
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

        public object CaptureState()
        {
            return new SerializableVector3(transform.position);
        }

        public void RestoreState(object state)
        {
            // temporarily disable to prevent conflicts
            navMeshAgent.enabled = false;
            transform.position = ((SerializableVector3)state).ToVector();
            navMeshAgent.enabled = true;
        }
    }
}