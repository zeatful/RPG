using RPG.Combat;
using RPG.Content;
using RPG.Core;
using RPG.Items;
using RPG.Saving;
using RPG.Stats;
using UnityEngine;

namespace RPG.Control
{
    /// <summary>
    /// Player side of combat: provides levels + gear bonuses to the Fighter, awards combat XP,
    /// keeps max HP in sync with Vitality, regenerates out of combat and respawns on death.
    /// </summary>
    public class PlayerStats : MonoBehaviour, ICombatStats, IAttackStyle, ISaveable
    {
        MeleeStance stance = MeleeStance.Slash;

        /// <summary>The chosen melee stance. Bash quietly falls back to Slash without a shield.</summary>
        public MeleeStance Stance
        {
            get
            {
                if (stance == MeleeStance.Bash && !HasShield) return MeleeStance.Slash;
                return stance;
            }
        }

        public MeleeStance ChosenStance { get { return stance; } }

        public bool HasShield { get { return inventory != null && inventory.Model.ShieldId != null; } }

        public event System.Action onStanceChanged;
        public event System.Action onRespawned;

        /// <summary>Picks a melee stance. Bash needs a shield. Returns false (with a message) if not allowed.</summary>
        public bool SetStance(MeleeStance value)
        {
            if (value == MeleeStance.Bash && !HasShield)
            {
                GameLog.Add("You need a shield equipped to fight in the Defense (shield bash) style.", GameLog.Warning);
                return false;
            }
            if (stance != value)
            {
                stance = value;
                GameLog.Add("Combat style: " + StanceName(value) + " (trains " + CombatMath.StanceSkill(value) + ").");
                if (onStanceChanged != null) onStanceChanged();
            }
            return true;
        }

        public static string StanceName(MeleeStance value)
        {
            switch (value)
            {
                case MeleeStance.Crush: return "Crush";
                case MeleeStance.Bash: return "Shield bash";
                default: return "Slash";
            }
        }

        public float IntervalMultiplier
        {
            get { return Style == CombatStyle.Melee ? CombatMath.StanceInterval(Stance) : 1f; }
        }

        public int MaxHit
        {
            get
            {
                if (Style != CombatStyle.Melee) return CombatMath.MaxHit(StrengthLevel, StrengthBonus);
                return CombatMath.StanceMaxHit(Stance, StrengthLevel, StrengthBonus);
            }
        }

        public AttackAnimation Animation
        {
            get
            {
                if (Style == CombatStyle.Ranged) return AttackAnimation.Shoot;
                if (Style == CombatStyle.Magic) return AttackAnimation.Cast;
                // no weapon: punch (the bash clip is a left-hand jab)
                if (inventory == null || inventory.Model.WeaponId == null) return AttackAnimation.Bash;
                switch (Stance)
                {
                    case MeleeStance.Crush: return AttackAnimation.Crush;
                    case MeleeStance.Bash: return AttackAnimation.Bash;
                    default: return AttackAnimation.Slash;
                }
            }
        }

        public object CaptureState()
        {
            return (int)stance;
        }

        public void RestoreState(object state)
        {
            if (state is int) stance = (MeleeStance)(int)state;
        }

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
            ItemInfo ammo = GameContent.Item(weapon.AmmoId);
            // arrows come from the ammo slot; spark stones (not wearable) from the bag
            bool used = ammo != null && ammo.Slot == EquipSlot.Ammo ? inventory.Model.UseAmmo(weapon.AmmoId) : inventory.Model.Remove(weapon.AmmoId, 1);
            if (used)
            {
                inventory.NotifyChanged();
                return true;
            }
            string name = ammo != null ? ammo.Name.ToLower() : weapon.AmmoId;
            failMessage = ammo != null && ammo.Slot == EquipSlot.Ammo
                ? "You have no " + name + " equipped. Click arrows in your bag to equip them."
                : "You have no " + name + " left.";
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
                    // melee XP goes to the skill of the chosen stance: Slash = Attack, Crush = Strength, Bash = Defense
                    skills.AddXp(CombatMath.StanceSkill(Stance), damage * 1.6 * rate);
                }
                else
                {
                    skills.AddXp(style == CombatStyle.Ranged ? Skill.Ranged : Skill.Magic, damage * 1.6 * rate);
                }
                skills.AddXp(Skill.Vitality, damage * 0.33 * rate);
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
            GameLog.Add("You have fallen. You'll wake up at the spawn point in Brookhollow.", GameLog.Warning);
            DropItemsOnDeath();
        }

        /// <summary>Everything you carry falls where you died, except your single most valuable item.</summary>
        void DropItemsOnDeath()
        {
            if (inventory == null) return;
            string kept;
            System.Collections.Generic.List<ItemStack> dropped = inventory.Model.TakeAllExceptMostValuable(out kept);
            for (int i = 0; i < dropped.Count; i++)
            {
                float angle = i * 2.39996f;
                float radius = 0.5f + 0.25f * Mathf.Sqrt(i);
                Vector3 offset = new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle)) * radius;
                Pickup.Spawn(dropped[i], transform.position + offset, false);
            }
            inventory.NotifyChanged();
            ItemInfo keptInfo = GameContent.Item(kept);
            if (keptInfo != null) GameLog.Add("You kept your " + keptInfo.Name + ".", GameLog.Rare);
            if (dropped.Count > 0) GameLog.Add("Everything else is on the ground where you fell. It vanishes after 15 minutes!", GameLog.Warning);
        }

        void Respawn()
        {
            deadTimer = 0;
            // The NavMeshAgent is disabled while dead (see Mover), so moving the transform is safe.
            transform.position = RPG.World.WorldBuilder.Root != null ? RPG.World.WorldBuilder.StartPosition : spawnPoint;
            transform.rotation = spawnRotation;
            if (onRespawned != null) onRespawned();
            health.Revive();
            GameLog.Add("You wake up, a little sore.");
        }

        void SyncMaxHealth(bool refill)
        {
            health.SetMaxHealth(skills.GetLevel(Skill.Vitality) * 10f, refill);
        }
    }
}
