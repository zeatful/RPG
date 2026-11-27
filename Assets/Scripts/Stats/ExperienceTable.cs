using System;

namespace RPG.Stats
{
    /// <summary>
    /// RuneScape-style XP curve: xp(L) = floor(1/4 * sum_{l=1}^{L-1} floor(l + 300 * 2^(l/7))).
    /// Level 99 needs 13,034,431 XP and level 92 is roughly half of that.
    /// Pure C# (no UnityEngine) so it can be unit tested outside the editor.
    /// </summary>
    public static class ExperienceTable
    {
        public const int MaxLevel = 99;

        // Lower this to make levelling faster across the whole game.
        public const double CurveConstant = 300.0;

        static readonly int[] table = Build();

        static int[] Build()
        {
            int[] result = new int[MaxLevel + 1];
            double points = 0;
            result[0] = 0;
            result[1] = 0;
            for (int level = 1; level < MaxLevel; level++)
            {
                points += Math.Floor(level + CurveConstant * Math.Pow(2.0, level / 7.0));
                result[level + 1] = (int)Math.Floor(points / 4.0);
            }
            return result;
        }

        /// <summary>Total XP needed to reach a level (level 1 = 0 XP).</summary>
        public static int XpForLevel(int level)
        {
            if (level < 1) level = 1;
            if (level > MaxLevel) level = MaxLevel;
            return table[level];
        }

        public static int LevelForXp(double xp)
        {
            for (int level = MaxLevel; level > 1; level--)
            {
                if (xp >= table[level]) return level;
            }
            return 1;
        }

        /// <summary>0..1 progress from the current level to the next (1 at max level).</summary>
        public static float ProgressToNext(double xp)
        {
            int level = LevelForXp(xp);
            if (level >= MaxLevel) return 1f;
            double start = table[level];
            double end = table[level + 1];
            return (float)((xp - start) / (end - start));
        }
    }
}
