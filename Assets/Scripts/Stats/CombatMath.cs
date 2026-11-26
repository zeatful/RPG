using System;

namespace RPG.Stats
{
    /// <summary>
    /// Accuracy and damage formulas. HP in this game is on a x10 scale (100 HP at Vitality 10),
    /// so max hits are 10x a RuneScape-style max hit. Pure C# for unit testing.
    /// </summary>
    public static class CombatMath
    {
        public const float MinHitChance = 0.05f;
        public const float MaxHitChance = 0.95f;

        public static float HitChance(int attackLevel, int attackBonus, int defenseLevel, int defenseBonus)
        {
            float attackRoll = (attackLevel + 8f) * (attackBonus + 64f);
            float defenseRoll = (defenseLevel + 8f) * (defenseBonus + 64f);

            float chance;
            if (attackRoll > defenseRoll)
            {
                chance = 1f - (defenseRoll + 2f) / (2f * (attackRoll + 1f));
            }
            else
            {
                chance = attackRoll / (2f * (defenseRoll + 1f));
            }
            return Clamp(chance, MinHitChance, MaxHitChance);
        }

        public static int MaxHit(int strengthLevel, int strengthBonus)
        {
            double effective = strengthLevel + 8;
            double baseHit = 0.5 + effective * (strengthBonus + 64) / 640.0;
            return Math.Max(1, (int)Math.Floor(baseHit * 10.0));
        }

        /// <summary>Damage for a successful hit: uniform 1..maxHit. roll01 is a random value in [0,1).</summary>
        public static int RollDamage(int maxHit, double roll01)
        {
            if (maxHit < 1) return 1;
            int damage = 1 + (int)Math.Floor(roll01 * maxHit);
            return Math.Min(damage, maxHit);
        }

        /// <summary>Combat level = (Atk + Str + Def + Vit) / 4 + max(Ranged, Magic) / 4.</summary>
        public static int CombatLevel(int attack, int strength, int defense, int vitality, int ranged, int magic)
        {
            double melee = (attack + strength + defense + vitality) / 4.0;
            double other = Math.Max(ranged, magic) / 4.0;
            return (int)Math.Floor(melee + other);
        }

        static float Clamp(float value, float min, float max)
        {
            if (value < min) return min;
            if (value > max) return max;
            return value;
        }
    }
}
