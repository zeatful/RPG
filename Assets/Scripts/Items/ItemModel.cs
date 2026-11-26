using System;
using System.Collections.Generic;
using RPG.Stats;

namespace RPG.Items
{
    public enum EquipSlot
    {
        None,
        Weapon,
        Body
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
    }

    /// <summary>
    /// 28-slot inventory plus weapon/body equipment. Pure C# so it can be unit tested.
    /// Stackable items share one slot; others take one slot each.
    /// </summary>
    public class InventoryModel
    {
        public const int SlotCount = 28;

        readonly ItemStack[] slots = new ItemStack[SlotCount];
        readonly Func<string, ItemInfo> lookup;

        public string WeaponId { get; private set; }
        public string BodyId { get; private set; }

        public InventoryModel(Func<string, ItemInfo> lookup)
        {
            this.lookup = lookup;
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
        /// Equips the item in a slot, swapping any currently equipped item back into that slot.
        /// levelOf returns the player's level in a skill (for requirements).
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

            string previous = info.Slot == EquipSlot.Weapon ? WeaponId : BodyId;
            slots[index] = string.IsNullOrEmpty(previous) ? new ItemStack() : new ItemStack(previous, 1);
            if (info.Slot == EquipSlot.Weapon) WeaponId = info.Id; else BodyId = info.Id;
            return true;
        }

        public bool Unequip(EquipSlot slot)
        {
            string id = slot == EquipSlot.Weapon ? WeaponId : BodyId;
            if (string.IsNullOrEmpty(id)) return false;
            if (FindEmpty() < 0) return false;
            Add(id, 1);
            if (slot == EquipSlot.Weapon) WeaponId = null; else BodyId = null;
            return true;
        }

        public void SetEquipped(string weaponId, string bodyId)
        {
            WeaponId = weaponId;
            BodyId = bodyId;
        }

        public int TotalAttackBonus() { return Bonus(i => i.AttackBonus); }
        public int TotalStrengthBonus() { return Bonus(i => i.StrengthBonus); }
        public int TotalDefenseBonus() { return Bonus(i => i.DefenseBonus); }

        int Bonus(Func<ItemInfo, int> pick)
        {
            int total = 0;
            ItemInfo weapon = string.IsNullOrEmpty(WeaponId) ? null : lookup(WeaponId);
            ItemInfo body = string.IsNullOrEmpty(BodyId) ? null : lookup(BodyId);
            if (weapon != null) total += pick(weapon);
            if (body != null) total += pick(body);
            return total;
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
            return data;
        }

        public void Restore(InventorySaveData data)
        {
            for (int i = 0; i < SlotCount; i++)
            {
                bool hasData = data.SlotIds != null && i < data.SlotIds.Length && data.SlotQuantities != null && i < data.SlotQuantities.Length;
                slots[i] = hasData ? new ItemStack(data.SlotIds[i], data.SlotQuantities[i]) : new ItemStack();
            }
            WeaponId = data.WeaponId;
            BodyId = data.BodyId;
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
            return HasLevel(levelOf) && HasInputs(inventory);
        }

        /// <summary>Consumes inputs and adds output. Returns false (and changes nothing) if it can't be made.</summary>
        public bool TryMake(InventoryModel inventory, Func<Skill, int> levelOf, out string failReason)
        {
            failReason = null;
            if (!HasLevel(levelOf)) { failReason = "You need " + Skill + " level " + Level + " to make that."; return false; }
            if (!HasInputs(inventory)) { failReason = "You don't have the materials."; return false; }

            foreach (ItemStack input in Inputs) inventory.Remove(input.ItemId, input.Quantity);

            // If the output doesn't fit, put the inputs back and fail without changing anything.
            if (!inventory.CanAdd(Output.ItemId, Output.Quantity))
            {
                foreach (ItemStack input in Inputs) inventory.Add(input.ItemId, input.Quantity);
                failReason = "Your inventory is full.";
                return false;
            }

            inventory.Add(Output.ItemId, Output.Quantity);
            return true;
        }
    }
}
