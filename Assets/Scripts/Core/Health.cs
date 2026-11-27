namespace RPG.Core
{
    using System;
    using UnityEngine;

    public class Health : MonoBehaviour
    {
        [SerializeField] float healthPoints = 100f;
        float maxHealthPoints = -1f;
        bool isDead = false;
        ActionScheduler actionScheduler;
        Animator animator;
        GameObject lastInstigator;

        // Damage taken (0 = a miss) and who dealt it.
        public event Action<float, GameObject> onDamaged;

        // Fired once on death with whoever landed the killing blow (may be null).
        public event Action<GameObject> onDeath;

        public float TimeSinceLastDamaged { get; private set; }

        private void Awake()
        {
            if (maxHealthPoints < 0) maxHealthPoints = healthPoints;
            TimeSinceLastDamaged = Mathf.Infinity;
        }

        private void Start()
        {
            actionScheduler = GetComponent<ActionScheduler>();
            animator = GetComponent<Animator>();
        }

        private void Update()
        {
            TimeSinceLastDamaged += Time.deltaTime;
        }

        public bool IsDead()
        {
            return isDead;
        }

        public float GetHealth()
        {
            return healthPoints;
        }

        public float GetMaxHealth()
        {
            return maxHealthPoints < 0 ? healthPoints : maxHealthPoints;
        }

        public float GetFraction()
        {
            float max = GetMaxHealth();
            return max <= 0 ? 0 : healthPoints / max;
        }

        public void SetMaxHealth(float max, bool refill)
        {
            float oldMax = GetMaxHealth();
            maxHealthPoints = Mathf.Max(1f, max);
            if (refill || isDead)
            {
                if (!isDead) healthPoints = maxHealthPoints;
            }
            else if (oldMax > 0)
            {
                // keep the same fraction of health when max changes
                healthPoints = Mathf.Clamp(healthPoints / oldMax * maxHealthPoints, 1f, maxHealthPoints);
            }
        }

        public void Heal(float amount)
        {
            if (isDead) return;
            healthPoints = Mathf.Min(healthPoints + amount, GetMaxHealth());
        }

        public void TakeDamage(float damage)
        {
            TakeDamage(damage, null);
        }

        public void TakeDamage(float damage, GameObject instigator)
        {
            if (isDead) return;
            if (instigator != null) lastInstigator = instigator;
            TimeSinceLastDamaged = 0;
            healthPoints = Mathf.Max(healthPoints - damage, 0);
            if (onDamaged != null) onDamaged(damage, instigator);
            if (healthPoints == 0)
            {
                Die();
            }
        }

        /// <summary>Bring a dead character back at full health and reset its animator.</summary>
        public void Revive()
        {
            if (!isDead) return;
            isDead = false;
            healthPoints = GetMaxHealth();
            lastInstigator = null;
            TimeSinceLastDamaged = Mathf.Infinity;
            if (animator != null)
            {
                animator.Rebind();
            }
        }

        private void Die()
        {
            if (isDead) return;
            isDead = true;
            animator.SetTrigger("die");
            actionScheduler.CancelCurrentAction();
            if (onDeath != null) onDeath(lastInstigator);
        }
    }
}
