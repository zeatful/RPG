using System.Collections.Generic;
using RPG.Combat;
using RPG.Content;
using RPG.Control;
using RPG.Core;
using RPG.Gathering;
using RPG.Items;
using RPG.Stats;
using UnityEngine;

namespace RPG.Game
{
    /// <summary>
    /// Turns game events into sound effects: swings and hits (a different sound for slash, crush, shield
    /// bash, bow and staff), monster voices, chopping, mining, eating, equipping, level-ups and deaths.
    /// </summary>
    public class SoundDirector : MonoBehaviour
    {
        GameObject player;
        PlayerStats stats;
        readonly Dictionary<GameObject, float> lastVoice = new Dictionary<GameObject, float>();
        float lastStep;

        private void Start()
        {
            player = GameObject.FindWithTag("Player");
            stats = player != null ? player.GetComponent<PlayerStats>() : null;
            Fighter.onSwing += OnSwing;
            Fighter.onAttackResolved += OnAttackResolved;
            Gatherer.onSwing += OnGatherSwing;
            if (player != null)
            {
                Inventory inventory = player.GetComponent<Inventory>();
                if (inventory != null)
                {
                    inventory.onEquipped += OnEquipped;
                    inventory.onAte += OnAte;
                }
                SkillSet skills = player.GetComponent<SkillSet>();
                if (skills != null) skills.onLevelUp += OnLevelUp;
                Health health = player.GetComponent<Health>();
                if (health != null) health.onDeath += OnPlayerDeath;
            }
            foreach (EnemyProfile enemy in EnemyProfile.All) Watch(enemy);
        }

        void Watch(EnemyProfile enemy)
        {
            if (enemy == null || enemy.Health == null) return;
            EnemyProfile captured = enemy;
            enemy.Health.onDeath += killer => Voice(captured, true);
        }

        private void OnDestroy()
        {
            Fighter.onSwing -= OnSwing;
            Fighter.onAttackResolved -= OnAttackResolved;
            Gatherer.onSwing -= OnGatherSwing;
        }

        static string VoiceOf(EnemyProfile enemy)
        {
            if (enemy == null || enemy.Info == null) return null;
            switch (enemy.Info.Id)
            {
                case "thistlehog": return "bite";
                case "gnarl": return "ogre";
                case "boar":
                case "grey_wolf":
                case "ash_wolf": return "beast";
                case "stone_golem":
                case "magma_golem": return "giant";
                case "bog_slime": return "slime";
                case "cinder_imp": return "shade";
                case GameContent.BossId: return "ogre";
            }
            return null;
        }

        void Voice(EnemyProfile enemy, bool force)
        {
            string voice = VoiceOf(enemy);
            if (voice == null) return;
            float last;
            if (!force && lastVoice.TryGetValue(enemy.gameObject, out last) && Time.time - last < 2.5f) return;
            lastVoice[enemy.gameObject] = Time.time;
            Sfx.At(voice, enemy.transform.position, force ? 0.9f : 0.6f);
        }

        void OnSwing(GameObject attacker)
        {
            if (attacker != player || stats == null) return;
            switch (stats.Animation)
            {
                case AttackAnimation.Shoot: Sfx.At("bow", attacker.transform.position, 0.8f); break;
                case AttackAnimation.Cast: Sfx.At("magic", attacker.transform.position, 0.7f); break;
                default: Sfx.At("swing", attacker.transform.position, 0.55f, 0.12f); break;
            }
        }

        void OnAttackResolved(GameObject attacker, GameObject defender, int damage)
        {
            if (attacker == null || defender == null) return;
            Vector3 at = defender.transform.position + Vector3.up;
            if (attacker == player && stats != null)
            {
                if (damage <= 0) return; // the swing sound already played
                switch (stats.Animation)
                {
                    case AttackAnimation.Slash: Sfx.At("slash", at, 0.85f); break;
                    case AttackAnimation.Crush: Sfx.At("thud", at, 1f); Sfx.At("slash", at, 0.35f); break;
                    case AttackAnimation.Bash: Sfx.At("clash", at, 0.75f); Sfx.At("thud", at, 0.6f); break;
                    default: Sfx.At("thud", at, 0.7f); break;
                }
                Voice(defender.GetComponent<EnemyProfile>(), false);
            }
            else if (defender == player)
            {
                EnemyProfile enemy = attacker.GetComponent<EnemyProfile>();
                Voice(enemy, false);
                if (damage > 0) Sfx.At("hurt", at, 0.8f);
                else Sfx.At(stats != null && stats.HasShield ? "clash" : "swing", at, 0.45f);
            }
        }

        void OnGatherSwing(NodeKind kind)
        {
            if (player == null) return;
            Sfx.At(kind == NodeKind.Tree ? "chop" : "mine", player.transform.position + player.transform.forward, 0.8f);
        }

        void OnEquipped(ItemInfo info)
        {
            if (info == null) return;
            string sound = "equip_cloth";
            if (info.Slot == EquipSlot.Weapon) sound = info.Tool != ToolKind.None || info.TwoHanded ? "equip_wood" : "unsheathe";
            else if (info.Id.StartsWith("platebody") || info.Id.StartsWith("platelegs") || info.Id.StartsWith("helm_")) sound = "equip_armor";
            else if (info.Id.StartsWith("kiteshield")) sound = "equip_metal";
            else if (info.Id.StartsWith("shield_")) sound = "equip_wood";
            Sfx.Ui(sound, 0.7f);
        }

        void OnAte()
        {
            Sfx.Ui("eat", 0.7f);
        }

        void OnLevelUp(Skill skill, int level)
        {
            Sfx.Ui("levelup", 0.6f);
        }

        void OnPlayerDeath(GameObject killer)
        {
            // a low "bad news" bwomp (an original sound in the spirit of the classic Mac alert)
            Sfx.Ui("death", 0.95f);
        }

        private void Update()
        {
            // soft footsteps while walking or running
            if (player == null) return;
            UnityEngine.AI.NavMeshAgent agent = player.GetComponent<UnityEngine.AI.NavMeshAgent>();
            RPG.Movement.Mover mover = player.GetComponent<RPG.Movement.Mover>();
            float speed = Mathf.Max(agent != null && agent.enabled ? agent.velocity.magnitude : 0f, mover != null ? mover.ManualSpeed : 0f);
            if (speed < 0.8f) return;
            float interval = speed > 4f ? 0.32f : 0.5f;
            if (Time.time - lastStep < interval) return;
            lastStep = Time.time;
            Sfx.At("step", player.transform.position, 0.18f, 0.15f);
        }
    }
}
