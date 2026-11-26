using RPG.Combat;
using RPG.Core;
using UnityEngine;

namespace RPG.Control
{
    /// <summary>
    /// Old Gullet's mechanics on top of normal enemy AI:
    ///  - Slam: a red circle appears under the player; 1.6s later anyone inside takes heavy damage.
    ///  - Enrage below 30% HP: attacks 60% faster and slams more often.
    /// </summary>
    public class BossController : MonoBehaviour
    {
        const float EngageRange = 14f;
        const float SlamRadius = 3f;
        const float SlamWindup = 1.6f;
        const int SlamDamage = 35;

        public static BossController Active { get; private set; }

        EnemyProfile profile;
        Fighter fighter;
        GameObject player;
        Health playerHealth;

        GameObject telegraph;
        Vector3 slamCenter;
        float slamTimer;
        float windupTimer = -1f;
        bool enraged;

        public bool IsEngaged { get; private set; }
        public EnemyProfile Profile { get { return profile; } }

        private void Start()
        {
            profile = GetComponent<EnemyProfile>();
            fighter = GetComponent<Fighter>();
            player = GameObject.FindWithTag("Player");
            playerHealth = player != null ? player.GetComponent<Health>() : null;
            profile.Health.onDeath += OnDeath;
            Active = this;

            telegraph = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            Destroy(telegraph.GetComponent<Collider>());
            telegraph.name = "Slam Telegraph";
            telegraph.GetComponent<Renderer>().material.color = new Color(0.8f, 0.1f, 0.05f);
            telegraph.SetActive(false);
        }

        private void OnDestroy()
        {
            if (Active == this) Active = null;
            if (telegraph != null) Destroy(telegraph);
        }

        private void Update()
        {
            if (profile == null || player == null) return;

            bool alive = !profile.Health.IsDead();
            float distance = Vector3.Distance(transform.position, player.transform.position);
            IsEngaged = alive && !playerHealth.IsDead() && distance < EngageRange;

            if (!alive)
            {
                CancelSlam();
                if (enraged)
                {
                    enraged = false;
                    fighter.AttackSpeedMultiplier = 1f;
                }
                return;
            }

            if (!enraged && profile.Health.GetFraction() < 0.3f)
            {
                enraged = true;
                fighter.AttackSpeedMultiplier = 1.6f;
                GameLog.Add("Old Gullet thrashes wildly!", GameLog.Warning);
            }

            if (windupTimer >= 0)
            {
                windupTimer += Time.deltaTime;
                float t = Mathf.Clamp01(windupTimer / SlamWindup);
                telegraph.transform.localScale = new Vector3(SlamRadius * 2f * t, 0.02f, SlamRadius * 2f * t);
                if (windupTimer >= SlamWindup) ResolveSlam();
                return;
            }

            if (!IsEngaged) return;

            slamTimer += Time.deltaTime;
            if (slamTimer >= (enraged ? 5f : 9f))
            {
                slamTimer = 0;
                StartSlam();
            }
        }

        void StartSlam()
        {
            slamCenter = player.transform.position;
            telegraph.transform.position = slamCenter + Vector3.up * 0.05f;
            telegraph.transform.localScale = new Vector3(0.1f, 0.02f, 0.1f);
            telegraph.SetActive(true);
            windupTimer = 0;
            GameLog.Add("Old Gullet rears up... move!", GameLog.Warning);
        }

        void ResolveSlam()
        {
            CancelSlam();
            if (playerHealth.IsDead()) return;

            float distance = RpgUtil.FlatDistance(player.transform.position, slamCenter);
            int damage = distance <= SlamRadius ? SlamDamage : 0;
            if (damage > 0) playerHealth.TakeDamage(damage, gameObject);
            Fighter.RaiseAttackResolved(gameObject, player, damage);
            GameLog.Add(damage > 0 ? "The slam hits you for " + damage + "!" : "You dodge the slam.");
        }

        void CancelSlam()
        {
            windupTimer = -1f;
            if (telegraph != null) telegraph.SetActive(false);
        }

        void OnDeath(GameObject killer)
        {
            CancelSlam();
            GameLog.Add("Old Gullet sinks beneath the water... for now.", GameLog.Rare);
        }
    }
}
