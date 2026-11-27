using System;
using RPG.Saving;
using UnityEngine;

namespace RPG.Stats
{
    /// <summary>The player's twelve skills. Saved with the existing save system (S / L keys).</summary>
    public class SkillSet : MonoBehaviour, ISaveable
    {
        public const int StartingVitalityLevel = 10;

        double[] xp = new double[SkillInfo.Count];

        // skill, xp amount gained
        public event Action<Skill, double> onXpGained;

        // skill, new level
        public event Action<Skill, int> onLevelUp;

        // fired after a save is loaded
        public event Action onRestored;

        private void Awake()
        {
            xp[(int)Skill.Vitality] = ExperienceTable.XpForLevel(StartingVitalityLevel);
        }

        public int GetLevel(Skill skill)
        {
            return ExperienceTable.LevelForXp(xp[(int)skill]);
        }

        public double GetXp(Skill skill)
        {
            return xp[(int)skill];
        }

        public int TotalLevel()
        {
            int total = 0;
            for (int i = 0; i < SkillInfo.Count; i++) total += GetLevel((Skill)i);
            return total;
        }

        public int CombatLevel()
        {
            return CombatMath.CombatLevel(
                GetLevel(Skill.Attack), GetLevel(Skill.Strength), GetLevel(Skill.Defense),
                GetLevel(Skill.Vitality), GetLevel(Skill.Ranged), GetLevel(Skill.Magic));
        }

        public void AddXp(Skill skill, double amount)
        {
            if (amount <= 0) return;
            int before = GetLevel(skill);
            double cap = ExperienceTable.XpForLevel(ExperienceTable.MaxLevel) * 15.0; // generous cap past 99
            xp[(int)skill] = Math.Min(xp[(int)skill] + amount, cap);
            int after = GetLevel(skill);

            if (onXpGained != null) onXpGained(skill, amount);
            if (after > before && onLevelUp != null) onLevelUp(skill, after);
        }

        public object CaptureState()
        {
            return (double[])xp.Clone();
        }

        public void RestoreState(object state)
        {
            double[] saved = state as double[];
            if (saved == null) return;
            for (int i = 0; i < xp.Length && i < saved.Length; i++) xp[i] = saved[i];
            if (onRestored != null) onRestored();
        }
    }
}
