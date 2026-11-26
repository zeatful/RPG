using RPG.Combat;
using RPG.Content;
using RPG.Core;
using RPG.Items;
using RPG.Stats;
using UnityEngine;

namespace RPG.Control
{
    /// <summary>
    /// Player side of combat: provides levels + gear bonuses to the Fighter, awards combat XP,
    /// keeps max HP in sync with Vitality, regenerates out of combat and respawns on death.
    /// </summary>
    public class PlayerStats : MonoBehaviour, ICombatStats
    {
        const float RespawnDelay = 5f;
        const float RegenDelay = 8f;
        const float RegenInterval = 3f;
        const float RegenFraction = 0.03f;

        SkillSet skills;
        Inventory inventory;
        Health health;
        Vector3 spawnPoint;
        Quaternion spawnRotation;
        float deadTimer;
        float regenTimer;

        // Ranged and magic use their own skill for both accuracy and damage.
        public int AttackLevel { get { return skills.GetLevel(OffenseSkill(Skill.Attack)); } }
        public int StrengthLevel { get { return skills.GetLevel(OffenseSkill(Skill.Strength)); } }
        public int DefenseLevel { get { return skills.GetLevel(Skill.Defense); } }
        public int AttackBonus { get { return inventory != null ? inventory.Model.TotalAttackBonus() : 0; } }
        public int StrengthBonus { get { return inventory != null ? inventory.Model.TotalStrengthBonus() : 0; } }
        public int DefenseBonus { get { return inventory != null ? inventory.Model.TotalDefenseBonus() : 0; } }
        public string DisplayName { get { return "You"; } }
        public int CombatLevel { get { return skills.CombatLevel(); } }

        public CombatStyle Style
        {
            get
            {
                ItemInfo weapon = Weapon();
                return weapon != null ? weapon.Style : CombatStyle.Melee;
            }
        }

        public float AttackRange
        {
            get
            {
                ItemInfo weapon = Weapon();
                return weapon != null && weapon.Range > 0 ? weapon.Range : 0f;
            }
        }

        public bool TryUseAmmo(out string failMessage)
        {
            failMessage = null;
            ItemInfo weapon = Weapon();
            if (weapon == null || string.IsNullOrEmpty(weapon.AmmoId)) return true;
            if (inventory.Model.Remove(weapon.AmmoId, 1))
            {
                inventory.NotifyChanged();
                return true;
            }
            ItemInfo ammo = GameContent.Item(weapon.AmmoId);
            failMessage = "You have no " + (ammo != null ? ammo.Name.ToLower() : weapon.AmmoId) + " left.";
            return false;
        }

        ItemInfo Weapon()
        {
            return inventory != null ? GameContent.Item(inventory.Model.WeaponId) : null;
        }

        Skill OffenseSkill(Skill meleeSkill)
        {
            switch (Style)
            {
                case CombatStyle.Ranged: return Skill.Ranged;
                case CombatStyle.Magic: return Skill.Magic;
                default: return meleeSkill;
            }
        }

        private void Awake()
        {
            skills = GetComponent<SkillSet>();
            inventory = GetComponent<Inventory>();
            health = GetComponent<Health>();
        }

        private void Start()
        {
            spawnPoint = transform.position;
            spawnRotation = transform.rotation;
            SyncMaxHealth(true);

            skills.onLevelUp += OnLevelUp;
            skills.onRestored += OnRestored;
            health.onDeath += OnDeath;
            Fighter.onAttackResolved += OnAttackResolved;
        }

        private void OnDestroy()
        {
            Fighter.onAttackResolved -= OnAttackResolved;
            if (skills != null)
            {
                skills.onLevelUp -= OnLevelUp;
                skills.onRestored -= OnRestored;
            }
            if (health != null) health.onDeath -= OnDeath;
        }

        private void Update()
        {
            if (health.IsDead())
            {
                deadTimer += Time.deltaTime;
                if (deadTimer >= RespawnDelay) Respawn();
                return;
            }

            if (health.TimeSinceLastDamaged > RegenDelay && health.GetFraction() < 1f)
            {
                regenTimer += Time.deltaTime;
                if (regenTimer >= RegenInterval)
                {
                    regenTimer = 0;
                    health.Heal(Mathf.Max(1f, health.GetMaxHealth() * RegenFraction));
                }
            }
            else
            {
                regenTimer = 0;
            }
        }

        void OnAttackResolved(GameObject attacker, GameObject defender, int damage)
        {
            double rate = GameContent.CombatXpRate;
            if (attacker == gameObject && damage > 0)
            {
                CombatStyle style = Style;
                if (style == CombatStyle.Melee)
                {
                    // Twist on RuneScape: melee XP is split between Attack and Strength automatically.
                    skills.AddXp(Skill.Attack, damage * 0.8 * rate);
                    skills.AddXp(Skill.Strength, damage * 0.8 * rate);
                }
                else
                {
                    skills.AddXp(style == CombatStyle.Ranged ? Skill.Ranged : Skill.Magic, damage * 1.6 * rate);
                }
                skills.AddXp(Skill.Vitality, damage * 0.33 * rate);
            }
            else if (defender == gameObject)
            {
                // Defense levels from being attacked: XP for damage absorbed, a bonus for every dodge.
                skills.AddXp(Skill.Defense, (damage > 0 ? damage * 0.5 : 4.0) * rate);
            }
        }

        void OnLevelUp(Skill skill, int level)
        {
            GameLog.Add("Congratulations! Your " + skill + " level is now " + level + ".", GameLog.LevelUp);
            if (skill == Skill.Vitality) SyncMaxHealth(false);
        }

        void OnRestored()
        {
            SyncMaxHealth(true);
        }

        void OnDeath(GameObject killer)
        {
            deadTimer = 0;
            GameLog.Add("You have fallen. You'll wake up where you started.", GameLog.Warning);
        }

        void Respawn()
        {
            deadTimer = 0;
            // The NavMeshAgent is disabled while dead (see Mover), so moving the transform is safe.
            transform.position = spawnPoint;
            transform.rotation = spawnRotation;
            health.Revive();
            GameLog.Add("You wake up, a little sore.");
        }

        void SyncMaxHealth(bool refill)
        {
            health.SetMaxHealth(skills.GetLevel(Skill.Vitality) * 10f, refill);
        }
    }
}
