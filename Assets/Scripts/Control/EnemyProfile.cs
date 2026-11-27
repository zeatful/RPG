using System.Collections.Generic;
using RPG.Content;
using RPG.Core;
using RPG.Drops;
using RPG.Items;
using RPG.Stats;
using UnityEngine;

namespace RPG.Control
{
    /// <summary>Gives an enemy a name, levels, HP and a drop table. Drops loot on death and respawns.</summary>
    public class EnemyProfile : MonoBehaviour, ICombatStats
    {
        public static readonly List<EnemyProfile> All = new List<EnemyProfile>();

        static readonly System.Random random = new System.Random();
        static readonly DropRoller roller = new DropRoller(random);

        public EnemyInfo Info { get; private set; }
        public Health Health { get; private set; }

        /// <summary>How far above the feet the name and health bar float.</summary>
        public float NameplateHeight { get; private set; }

        Vector3 spawnPoint;
        Quaternion spawnRotation;
        float deadTimer;

        public int AttackLevel { get { return Info.Attack; } }
        public int StrengthLevel { get { return Info.Strength; } }
        public int DefenseLevel { get { return Info.Defense; } }
        public int AttackBonus { get { return Info.AttackBonus; } }
        public int StrengthBonus { get { return Info.StrengthBonus; } }
        public int DefenseBonus { get { return Info.DefenseBonus; } }
        public string DisplayName { get { return Info.Name; } }
        public int CombatLevel { get { return Info.Level; } }
        public CombatStyle Style { get { return CombatStyle.Melee; } }
        public float AttackRange { get { return 0f; } }

        public bool TryUseAmmo(out string failMessage)
        {
            failMessage = null;
            return true;
        }

        private void OnEnable() { All.Add(this); }
        private void OnDisable() { All.Remove(this); }

        public void Configure(EnemyInfo info)
        {
            Info = info;
            Health = GetComponent<Health>();
            spawnPoint = transform.position;
            spawnRotation = transform.rotation;
            gameObject.name = info.Name;
            if (info.Scale != 1f) transform.localScale = transform.localScale * info.Scale;
            Health.SetMaxHealth(info.MaxHp, true);

            bool tint;
            if (CreatureVisual.Apply(gameObject, info, out tint))
            {
                NameplateHeight = GetComponent<CreatureVisual>().Height + 0.45f;
            }
            else
            {
                NameplateHeight = 2.3f * info.Scale;
            }
            if (tint) RpgUtil.Tint(gameObject, RpgUtil.FromHex(info.TintHex, Color.white), 0.5f);
            Health.onDeath -= OnDeath;
            Health.onDeath += OnDeath;
        }

        private void Update()
        {
            if (Info == null || !Health.IsDead()) return;

            deadTimer += Time.deltaTime;
            if (deadTimer >= Info.RespawnSeconds)
            {
                deadTimer = 0;
                transform.position = spawnPoint;
                transform.rotation = spawnRotation;
                Health.Revive();
            }
        }

        void OnDeath(GameObject killer)
        {
            deadTimer = 0;
            if (killer != null && killer.CompareTag("Player"))
            {
                GameLog.Add("You defeat the " + Info.Name + ".");
            }
            DropLoot(killer);
        }

        void DropLoot(GameObject killer)
        {
            DropTable table;
            if (!GameContent.DropTables.TryGetValue(Info.DropTableId, out table)) return;
            DropTable lucky;
            GameContent.DropTables.TryGetValue(GameContent.LuckyTableId, out lucky);

            DropLuck luck = killer != null ? killer.GetComponent<DropLuck>() : null;
            List<DropResult> results = roller.Roll(table, lucky, luck != null ? luck.Tracker : null);

            for (int i = 0; i < results.Count; i++)
            {
                DropResult result = results[i];
                float angle = i * Mathf.PI * 2f / Mathf.Max(1, results.Count);
                Vector3 offset = new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle)) * (results.Count > 1 ? 0.7f : 0f);
                bool special = result.IsRare || result.FromLuckyTable;
                Pickup.Spawn(result.Stack, transform.position + offset, special);

                if (special)
                {
                    ItemInfo info = GameContent.Item(result.Stack.ItemId);
                    string label = result.FromLuckyTable ? "Lucky drop: " : "Rare drop: ";
                    GameLog.Add(label + (info != null ? info.Name : result.Stack.ItemId) + "!", GameLog.Rare);
                }
            }
        }
    }
}
