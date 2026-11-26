using System;
using System.Collections.Generic;
using RPG.Items;

namespace RPG.Drops
{
    /// <summary>One weighted entry in the main table. A null ItemId means "nothing".</summary>
    public class DropEntry
    {
        public string ItemId;
        public int Min = 1;
        public int Max = 1;
        public int Weight = 1;

        public DropEntry(string itemId, int min, int max, int weight)
        {
            ItemId = itemId;
            Min = min;
            Max = max;
            Weight = weight;
        }
    }

    /// <summary>An independent 1-in-N roll, with bad-luck protection.</summary>
    public class RareDrop
    {
        public string ItemId;
        public int Quantity = 1;
        public int OneIn = 100;

        public RareDrop(string itemId, int quantity, int oneIn)
        {
            ItemId = itemId;
            Quantity = quantity;
            OneIn = oneIn;
        }
    }

    public class DropTable
    {
        public string Id;
        public List<DropEntry> Always = new List<DropEntry>();
        public List<DropEntry> Main = new List<DropEntry>();
        public List<RareDrop> Rares = new List<RareDrop>();

        // Chance per kill to also roll the shared Lucky Table.
        public double LuckyTableChance = 1.0 / 64.0;
    }

    /// <summary>Counts kills since each rare last dropped. Serializable for saving.</summary>
    [Serializable]
    public class BadLuckTracker
    {
        public Dictionary<string, int> DryStreaks = new Dictionary<string, int>();

        public int Get(string key)
        {
            int value;
            return DryStreaks.TryGetValue(key, out value) ? value : 0;
        }

        public void Set(string key, int value)
        {
            DryStreaks[key] = value;
        }
    }

    public struct DropResult
    {
        public ItemStack Stack;
        public bool IsRare;
        public bool FromLuckyTable;
    }

    /// <summary>
    /// Rolls drop tables. Pure C#: the random source is injected so rolls are testable.
    /// Rules: every Always entry drops; one weighted Main entry drops; each Rare rolls 1/N independently,
    /// and after 2N dry kills its chance grows 10% (relative) per further kill; then a 1/64 Lucky Table roll.
    /// </summary>
    public class DropRoller
    {
        public const double BadLuckGrowth = 1.10;

        readonly Random random;

        public DropRoller(Random random)
        {
            this.random = random;
        }

        public static double RareChance(int oneIn, int dryStreak)
        {
            double chance = 1.0 / oneIn;
            int threshold = 2 * oneIn;
            if (dryStreak > threshold)
            {
                chance *= Math.Pow(BadLuckGrowth, dryStreak - threshold);
            }
            return Math.Min(1.0, chance);
        }

        public List<DropResult> Roll(DropTable table, DropTable luckyTable, BadLuckTracker tracker)
        {
            List<DropResult> results = new List<DropResult>();
            RollInto(table, tracker, results, false);

            if (luckyTable != null && random.NextDouble() < table.LuckyTableChance)
            {
                RollInto(luckyTable, tracker, results, true);
            }
            return results;
        }

        void RollInto(DropTable table, BadLuckTracker tracker, List<DropResult> results, bool lucky)
        {
            foreach (DropEntry entry in table.Always)
            {
                AddResult(results, entry.ItemId, Quantity(entry), false, lucky);
            }

            DropEntry main = PickWeighted(table.Main);
            if (main != null && !string.IsNullOrEmpty(main.ItemId))
            {
                AddResult(results, main.ItemId, Quantity(main), false, lucky);
            }

            foreach (RareDrop rare in table.Rares)
            {
                string key = table.Id + ":" + rare.ItemId;
                int dry = tracker != null ? tracker.Get(key) : 0;
                if (random.NextDouble() < RareChance(rare.OneIn, dry))
                {
                    AddResult(results, rare.ItemId, rare.Quantity, true, lucky);
                    if (tracker != null) tracker.Set(key, 0);
                }
                else if (tracker != null)
                {
                    tracker.Set(key, dry + 1);
                }
            }
        }

        DropEntry PickWeighted(List<DropEntry> entries)
        {
            int total = 0;
            foreach (DropEntry entry in entries) total += Math.Max(0, entry.Weight);
            if (total <= 0) return null;

            int roll = random.Next(total);
            foreach (DropEntry entry in entries)
            {
                int weight = Math.Max(0, entry.Weight);
                if (roll < weight) return entry;
                roll -= weight;
            }
            return null;
        }

        int Quantity(DropEntry entry)
        {
            if (entry.Max <= entry.Min) return entry.Min;
            return random.Next(entry.Min, entry.Max + 1);
        }

        static void AddResult(List<DropResult> results, string itemId, int quantity, bool rare, bool lucky)
        {
            DropResult result = new DropResult();
            result.Stack = new ItemStack(itemId, quantity);
            result.IsRare = rare;
            result.FromLuckyTable = lucky;
            results.Add(result);
        }
    }
}
