using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
using RPG.Stats;

namespace RPG.Items
{
    /// <summary>Worn equipment slots. Ammo holds one stack (up to InventoryModel.AmmoCap).</summary>
    public enum EquipSlot
    {
        None,
        Weapon,
        Body,
        Head,
        Legs,
        Shield,
        Ammo
    }

    /// <summary>Gathering tools: the best one in your bag that you have the level for is used.</summary>
    public enum ToolKind
    {
        None,
        Axe,
        Pickaxe
    }

    /// <summary>Where a recipe has to be made.</summary>
    public enum StationKind
    {
        None,
        Furnace,
        Anvil,
        Fire,
        Bank,
        Shop,
        Door
    }

    /// <summary>Static definition of an item. Pure C#.</summary>
    public class ItemInfo
    {
        public string Id;
        public string Name;
        public string ColorHex = "#CCCCCC";
        public bool Stackable;
        public EquipSlot Slot = EquipSlot.None;
        public int AttackBonus;
        public int StrengthBonus;
        public int DefenseBonus;
        public Skill RequiredSkill = Skill.Attack;
        public int RequiredLevel = 1;
        public int HealAmount;
        public string Tier;
        public bool Rare;
        public string Description = "";

        // Weapons only
        public CombatStyle Style = CombatStyle.Melee;
        public string AmmoId;
        public float Range;

        // Left-click in the inventory summons or dismisses a follower.
        public bool IsPet;

        // Base price in coins. Shops sell at this price and buy back at SellRate of it.
        public int Value;

        // Axes and pickaxes: work done per swing (bronze 1.0). RequiredSkill/Level gate their use.
        public ToolKind Tool = ToolKind.None;
        public float ToolPower;

        // Bows and staves need both hands, so no shield with them.
        public bool TwoHanded;

        public bool IsEquippable { get { return Slot != EquipSlot.None; } }
        public bool IsEdible { get { return HealAmount > 0; } }
    }

    [Serializable]
    public struct ItemStack
    {
        public string ItemId;
        public int Quantity;

        public ItemStack(string itemId, int quantity)
        {
            ItemId = itemId;
            Quantity = quantity;
        }

        public bool IsEmpty { get { return string.IsNullOrEmpty(ItemId) || Quantity <= 0; } }
    }

    /// <summary>Serializable snapshot of an inventory, used by the saving system.</summary>
    [Serializable]
    public class InventorySaveData
    {
        public string[] SlotIds;
        public int[] SlotQuantities;
        public string WeaponId;
        public string BodyId;
        [OptionalField] public string HeadId;
        [OptionalField] public string LegsId;
        [OptionalField] public string ShieldId;
        [OptionalField] public string AmmoId;
        [OptionalField] public int AmmoCount;
    }

    /// <summary>
    /// 28-slot inventory plus worn equipment (head, body, legs, weapon, shield, ammo). Pure C# so it can be
    /// unit tested. Stackable items share one slot; others take one slot each. A two-handed weapon (bow,
    /// staff) and a shield can't be worn together; the ammo slot holds one stack of up to AmmoCap.
    /// </summary>
    public class InventoryModel
    {
        public const int SlotCount = 28;
        public const int AmmoCap = 999;

        // Equipment slots in display / death-drop order (Ammo is handled separately as a stack).
        public static readonly EquipSlot[] WornSlots = { EquipSlot.Head, EquipSlot.Body, EquipSlot.Legs, EquipSlot.Weapon, EquipSlot.Shield };

        readonly ItemStack[] slots = new ItemStack[SlotCount];
        readonly Dictionary<EquipSlot, string> worn = new Dictionary<EquipSlot, string>();
        readonly Func<string, ItemInfo> lookup;

        public string WeaponId { get { return Worn(EquipSlot.Weapon); } }
        public string BodyId { get { return Worn(EquipSlot.Body); } }
        public string HeadId { get { return Worn(EquipSlot.Head); } }
        public string LegsId { get { return Worn(EquipSlot.Legs); } }
        public string ShieldId { get { return Worn(EquipSlot.Shield); } }
        public string AmmoId { get; private set; }
        public int AmmoCount { get; private set; }

        public InventoryModel(Func<string, ItemInfo> lookup)
        {
            this.lookup = lookup;
        }

        /// <summary>The item id worn in a slot (Ammo returns the ammo id), or null.</summary>
        public string Worn(EquipSlot slot)
        {
            if (slot == EquipSlot.Ammo) return AmmoCount > 0 ? AmmoId : null;
            string id;
            return worn.TryGetValue(slot, out id) ? id : null;
        }

        void SetWorn(EquipSlot slot, string id)
        {
            if (string.IsNullOrEmpty(id)) worn.Remove(slot);
            else worn[slot] = id;
        }

        public ItemStack GetSlot(int index)
        {
            return slots[index];
        }

        public int FreeSlots()
        {
            int free = 0;
            for (int i = 0; i < SlotCount; i++) if (slots[i].IsEmpty) free++;
            return free;
        }

        /// <summary>How many are in the bag (not counting anything worn).</summary>
        public int Count(string itemId)
        {
            int total = 0;
            for (int i = 0; i < SlotCount; i++)
            {
                if (!slots[i].IsEmpty && slots[i].ItemId == itemId) total += slots[i].Quantity;
            }
            return total;
        }

        public bool CanAdd(string itemId, int quantity)
        {
            ItemInfo info = lookup(itemId);
            if (info == null || quantity <= 0) return false;
            if (info.Stackable)
            {
                return FindSlot(itemId) >= 0 || FreeSlots() > 0;
            }
            return FreeSlots() >= quantity;
        }

        /// <summary>Adds as much as fits. Returns how many could NOT be added.</summary>
        public int Add(string itemId, int quantity)
        {
            ItemInfo info = lookup(itemId);
            if (info == null || quantity <= 0) return quantity;

            if (info.Stackable)
            {
                int existing = FindSlot(itemId);
                if (existing >= 0)
                {
                    slots[existing].Quantity += quantity;
                    return 0;
                }
                int empty = FindEmpty();
                if (empty < 0) return quantity;
                slots[empty] = new ItemStack(itemId, quantity);
                return 0;
            }

            int remaining = quantity;
            while (remaining > 0)
            {
                int empty = FindEmpty();
                if (empty < 0) break;
                slots[empty] = new ItemStack(itemId, 1);
                remaining--;
            }
            return remaining;
        }

        /// <summary>Removes quantity of an item if the full amount is present. Returns success.</summary>
        public bool Remove(string itemId, int quantity)
        {
            if (Count(itemId) < quantity) return false;
            int remaining = quantity;
            for (int i = SlotCount - 1; i >= 0 && remaining > 0; i--)
            {
                if (slots[i].IsEmpty || slots[i].ItemId != itemId) continue;
                int take = Math.Min(remaining, slots[i].Quantity);
                slots[i].Quantity -= take;
                remaining -= take;
                if (slots[i].Quantity <= 0) slots[i] = new ItemStack();
            }
            return true;
        }

        public ItemStack RemoveSlot(int index)
        {
            ItemStack stack = slots[index];
            slots[index] = new ItemStack();
            return stack;
        }

        /// <summary>
        /// Equips the item in a bag slot, putting whatever it replaces back in the bag. A two-handed weapon
        /// also takes off the shield (and a shield takes off a two-handed weapon). Ammo moves up to AmmoCap
        /// into the ammo slot. levelOf returns the player's level in a skill (for requirements).
        /// </summary>
        public bool Equip(int index, Func<Skill, int> levelOf, out string failReason)
        {
            failReason = null;
            ItemStack stack = slots[index];
            if (stack.IsEmpty) { failReason = "Nothing there."; return false; }

            ItemInfo info = lookup(stack.ItemId);
            if (info == null || !info.IsEquippable) { failReason = "You can't equip that."; return false; }

            if (levelOf(info.RequiredSkill) < info.RequiredLevel)
            {
                failReason = "You need " + info.RequiredSkill + " level " + info.RequiredLevel + " to equip " + info.Name + ".";
                return false;
            }

            if (info.Slot == EquipSlot.Ammo) return EquipAmmo(index, info, out failReason);

            // what has to come off: the same slot, plus a shield for a two-hander or a two-hander for a shield
            List<string> off = new List<string>();
            string same = Worn(info.Slot);
            if (same != null) off.Add(same);
            EquipSlot clash = EquipSlot.None;
            if (info.Slot == EquipSlot.Weapon && info.TwoHanded && ShieldId != null) clash = EquipSlot.Shield;
            if (info.Slot == EquipSlot.Shield)
            {
                ItemInfo weapon = lookup(WeaponId ?? "");
                if (weapon != null && weapon.TwoHanded) clash = EquipSlot.Weapon;
            }
            if (clash != EquipSlot.None) off.Add(Worn(clash));

            // the item's own slot frees up; anything beyond one needs another free slot
            if (off.Count > 1 && FreeSlots() < off.Count - 1)
            {
                failReason = "Not enough inventory space to take off your " + lookup(off[off.Count - 1]).Name.ToLower() + ".";
                return false;
            }

            slots[index] = new ItemStack();
            SetWorn(info.Slot, info.Id);
            if (clash != EquipSlot.None) SetWorn(clash, null);
            for (int i = 0; i < off.Count; i++)
            {
                if (i == 0) slots[index] = new ItemStack(off[i], 1);
                else Add(off[i], 1);
            }
            return true;
        }

        bool EquipAmmo(int index, ItemInfo info, out string failReason)
        {
            failReason = null;
            ItemStack stack = slots[index];
            if (AmmoCount > 0 && AmmoId != info.Id)
            {
                // swap: the old stack goes back to the bag
                string oldId = AmmoId;
                int oldCount = AmmoCount;
                slots[index] = new ItemStack();
                AmmoId = null;
                AmmoCount = 0;
                Add(oldId, oldCount);
            }
            int room = AmmoCap - AmmoCount;
            if (room <= 0) { failReason = "You can't hold more than " + AmmoCap + " " + info.Name.ToLower() + "."; return false; }
            int move = Math.Min(room, stack.Quantity);
            AmmoId = info.Id;
            AmmoCount += move;
            int left = stack.Quantity - move;
            ItemStack current = slots[index];
            if (!current.IsEmpty && current.ItemId == info.Id) slots[index] = left > 0 ? new ItemStack(info.Id, left) : new ItemStack();
            else if (left > 0) Add(info.Id, left);
            return true;
        }

        public bool Unequip(EquipSlot slot)
        {
            if (slot == EquipSlot.Ammo)
            {
                if (AmmoCount <= 0) return false;
                if (!CanAdd(AmmoId, AmmoCount)) return false;
                Add(AmmoId, AmmoCount);
                AmmoId = null;
                AmmoCount = 0;
                return true;
            }
            string id = Worn(slot);
            if (string.IsNullOrEmpty(id)) return false;
            if (FindEmpty() < 0) return false;
            Add(id, 1);
            SetWorn(slot, null);
            return true;
        }

        /// <summary>Uses one piece of ammo of this id from the ammo slot.</summary>
        public bool UseAmmo(string ammoId)
        {
            if (AmmoCount <= 0 || AmmoId != ammoId) return false;
            AmmoCount--;
            if (AmmoCount == 0) AmmoId = null;
            return true;
        }

        /// <summary>Puts items straight into slots (starter kit, tests). Doesn't check requirements.</summary>
        public void SetEquipped(string weaponId, string bodyId)
        {
            SetWorn(EquipSlot.Weapon, weaponId);
            SetWorn(EquipSlot.Body, bodyId);
        }

        public void SetWornDirect(EquipSlot slot, string id, int ammoCount = 0)
        {
            if (slot == EquipSlot.Ammo)
            {
                AmmoId = id;
                AmmoCount = string.IsNullOrEmpty(id) ? 0 : Math.Min(AmmoCap, ammoCount);
                return;
            }
            SetWorn(slot, id);
        }

        public bool HasAnythingWorn()
        {
            return worn.Count > 0 || AmmoCount > 0;
        }

        public int TotalAttackBonus() { return Bonus(i => i.AttackBonus); }
        public int TotalStrengthBonus() { return Bonus(i => i.StrengthBonus); }
        public int TotalDefenseBonus() { return Bonus(i => i.DefenseBonus); }

        int Bonus(Func<ItemInfo, int> pick)
        {
            int total = 0;
            foreach (string id in worn.Values)
            {
                ItemInfo info = lookup(id);
                if (info != null) total += pick(info);
            }
            return total;
        }

        /// <summary>
        /// On death: keep only the single most valuable item (by item Value; equipment counts), remove
        /// everything else and return it so it can be dropped on the ground. keptId is null if nothing
        /// was worth keeping. Coins have no item value, so they are always dropped. If the kept item is a
        /// stack (arrows in the bag or the ammo slot) the whole stack is kept.
        /// </summary>
        public List<ItemStack> TakeAllExceptMostValuable(out string keptId)
        {
            // find the most valuable single item and where it is: -1-n worn slot n, -100 ammo, else a bag slot
            int bestValue = 0;
            int bestWhere = int.MinValue;
            keptId = null;
            for (int s = 0; s < WornSlots.Length; s++) CheckBest(Worn(WornSlots[s]), -1 - s, ref bestValue, ref bestWhere, ref keptId);
            if (AmmoCount > 0) CheckBest(AmmoId, -100, ref bestValue, ref bestWhere, ref keptId);
            for (int i = 0; i < SlotCount; i++)
            {
                if (!slots[i].IsEmpty) CheckBest(slots[i].ItemId, i, ref bestValue, ref bestWhere, ref keptId);
            }

            List<ItemStack> dropped = new List<ItemStack>();
            for (int s = 0; s < WornSlots.Length; s++)
            {
                string id = Worn(WornSlots[s]);
                if (id == null || bestWhere == -1 - s) continue;
                dropped.Add(new ItemStack(id, 1));
                SetWorn(WornSlots[s], null);
            }
            if (AmmoCount > 0 && bestWhere != -100)
            {
                dropped.Add(new ItemStack(AmmoId, AmmoCount));
                AmmoId = null;
                AmmoCount = 0;
            }
            for (int i = 0; i < SlotCount; i++)
            {
                if (slots[i].IsEmpty || i == bestWhere) continue;
                dropped.Add(slots[i]);
                slots[i] = new ItemStack();
            }
            return dropped;
        }

        void CheckBest(string id, int where, ref int bestValue, ref int bestWhere, ref string bestId)
        {
            ItemInfo info = string.IsNullOrEmpty(id) ? null : lookup(id);
            if (info == null || info.Value <= bestValue) return;
            bestValue = info.Value;
            bestWhere = where;
            bestId = id;
        }

        public InventorySaveData Capture()
        {
            InventorySaveData data = new InventorySaveData();
            data.SlotIds = new string[SlotCount];
            data.SlotQuantities = new int[SlotCount];
            for (int i = 0; i < SlotCount; i++)
            {
                data.SlotIds[i] = slots[i].ItemId;
                data.SlotQuantities[i] = slots[i].Quantity;
            }
            data.WeaponId = WeaponId;
            data.BodyId = BodyId;
            data.HeadId = HeadId;
            data.LegsId = LegsId;
            data.ShieldId = ShieldId;
            data.AmmoId = AmmoCount > 0 ? AmmoId : null;
            data.AmmoCount = AmmoCount;
            return data;
        }

        public void Restore(InventorySaveData data)
        {
            for (int i = 0; i < SlotCount; i++)
            {
                bool hasData = data.SlotIds != null && i < data.SlotIds.Length && data.SlotQuantities != null && i < data.SlotQuantities.Length;
                slots[i] = hasData ? new ItemStack(data.SlotIds[i], data.SlotQuantities[i]) : new ItemStack();
            }
            worn.Clear();
            SetWorn(EquipSlot.Weapon, data.WeaponId);
            SetWorn(EquipSlot.Body, data.BodyId);
            SetWorn(EquipSlot.Head, data.HeadId);
            SetWorn(EquipSlot.Legs, data.LegsId);
            SetWorn(EquipSlot.Shield, data.ShieldId);
            AmmoId = data.AmmoCount > 0 ? data.AmmoId : null;
            AmmoCount = AmmoId != null ? data.AmmoCount : 0;
        }

        int FindSlot(string itemId)
        {
            for (int i = 0; i < SlotCount; i++)
            {
                if (!slots[i].IsEmpty && slots[i].ItemId == itemId) return i;
            }
            return -1;
        }

        int FindEmpty()
        {
            for (int i = 0; i < SlotCount; i++)
            {
                if (slots[i].IsEmpty) return i;
            }
            return -1;
        }
    }

    /// <summary>A smithing or crafting recipe. Pure C#.</summary>
    public class Recipe
    {
        public string Id;
        public string Name;
        public Skill Skill;
        public int Level;
        public double Xp;
        public List<ItemStack> Inputs = new List<ItemStack>();
        public ItemStack Output;

        // Where it has to be made (None = anywhere), and a tool that must be in the bag but isn't used up.
        public StationKind Station = StationKind.None;
        public string ToolId;

        // Cooking: chance to get this item instead (see BurnChance). Burnt food gives no xp.
        public string BurntItemId;

        public bool HasTool(InventoryModel inventory)
        {
            return string.IsNullOrEmpty(ToolId) || inventory.Count(ToolId) > 0 || inventory.WeaponId == ToolId;
        }

        public bool HasLevel(Func<Skill, int> levelOf)
        {
            return levelOf(Skill) >= Level;
        }

        public bool HasInputs(InventoryModel inventory)
        {
            foreach (ItemStack input in Inputs)
            {
                if (inventory.Count(input.ItemId) < input.Quantity) return false;
            }
            return true;
        }

        public bool CanMake(InventoryModel inventory, Func<Skill, int> levelOf)
        {
            return HasLevel(levelOf) && HasInputs(inventory) && HasTool(inventory);
        }

        /// <summary>Consumes inputs and adds output. Returns false (and changes nothing) if it can't be made.</summary>
        public bool TryMake(InventoryModel inventory, Func<Skill, int> levelOf, out string failReason)
        {
            bool burnt;
            return TryMake(inventory, levelOf, 1.0, out failReason, out burnt);
        }

        /// <summary>
        /// As TryMake, for food that can burn: roll01 is a random value in [0,1); below the burn chance
        /// the burnt item is produced instead and burnt is set.
        /// </summary>
        public bool TryMake(InventoryModel inventory, Func<Skill, int> levelOf, double roll01, out string failReason, out bool burnt)
        {
            failReason = null;
            burnt = false;
            if (!HasLevel(levelOf)) { failReason = "You need " + Skill + " level " + Level + " to make that."; return false; }
            if (!HasTool(inventory)) { failReason = "You need a " + ToolId.Replace('_', ' ') + " to make that."; return false; }
            if (!HasInputs(inventory)) { failReason = "You don't have the materials."; return false; }

            foreach (ItemStack input in Inputs) inventory.Remove(input.ItemId, input.Quantity);

            // If the output doesn't fit, put the inputs back and fail without changing anything.
            if (!inventory.CanAdd(Output.ItemId, Output.Quantity))
            {
                foreach (ItemStack input in Inputs) inventory.Add(input.ItemId, input.Quantity);
                failReason = "Your inventory is full.";
                return false;
            }

            if (!string.IsNullOrEmpty(BurntItemId) && roll01 < BurnChance(levelOf(Skill)))
            {
                burnt = true;
                inventory.Add(BurntItemId, Output.Quantity);
                return true;
            }
            inventory.Add(Output.ItemId, Output.Quantity);
            return true;
        }

        /// <summary>Cooking burn chance: 50% at the requirement, falling to 0 twenty levels above it.</summary>
        public double BurnChance(int level)
        {
            if (string.IsNullOrEmpty(BurntItemId)) return 0;
            double chance = 0.5 - (level - Level) * 0.025;
            return chance < 0 ? 0 : chance > 0.5 ? 0.5 : chance;
        }
    }
}
