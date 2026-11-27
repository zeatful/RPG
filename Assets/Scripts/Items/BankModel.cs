using System;
using System.Collections.Generic;
using RPG.Content;

namespace RPG.Items
{
    [Serializable]
    public class BankSaveData
    {
        public string[] Ids;
        public int[] Quantities;
    }

    /// <summary>
    /// Shared bank storage: every bank chest in every town opens the same bank. Everything stacks in
    /// the bank (even swords), in the order it was first deposited. Pure C#.
    /// </summary>
    public class BankModel
    {
        public const int MaxDistinctItems = 400;

        readonly List<ItemStack> stacks = new List<ItemStack>();

        public int Count { get { return stacks.Count; } }

        public ItemStack Get(int index) { return stacks[index]; }

        public int Amount(string itemId)
        {
            int index = Find(itemId);
            return index >= 0 ? stacks[index].Quantity : 0;
        }

        /// <summary>Moves up to quantity of an item from the inventory into the bank. Returns how many moved.</summary>
        public int Deposit(InventoryModel inventory, string itemId, int quantity)
        {
            int have = inventory.Count(itemId);
            int amount = Math.Min(have, quantity);
            if (amount <= 0) return 0;
            int index = Find(itemId);
            if (index < 0 && stacks.Count >= MaxDistinctItems) return 0;

            inventory.Remove(itemId, amount);
            if (index >= 0) stacks[index] = new ItemStack(itemId, stacks[index].Quantity + amount);
            else stacks.Add(new ItemStack(itemId, amount));
            return amount;
        }

        /// <summary>Deposits every item in the inventory (equipment stays on). Returns the number of items moved.</summary>
        public int DepositAll(InventoryModel inventory)
        {
            int moved = 0;
            for (int i = 0; i < InventoryModel.SlotCount; i++)
            {
                ItemStack stack = inventory.GetSlot(i);
                if (stack.IsEmpty) continue;
                moved += Deposit(inventory, stack.ItemId, inventory.Count(stack.ItemId));
            }
            return moved;
        }

        /// <summary>Moves up to quantity into the inventory, as much as fits. Returns how many moved.</summary>
        public int Withdraw(InventoryModel inventory, string itemId, int quantity)
        {
            int index = Find(itemId);
            if (index < 0 || quantity <= 0) return 0;
            int amount = Math.Min(quantity, stacks[index].Quantity);

            ItemInfo info = GameContent.Item(itemId);
            if (info == null) return 0;
            if (info.Stackable)
            {
                if (!inventory.CanAdd(itemId, 1)) return 0;
            }
            else
            {
                amount = Math.Min(amount, inventory.FreeSlots());
            }
            if (amount <= 0) return 0;

            int left = inventory.Add(itemId, amount);
            int moved = amount - left;
            int remaining = stacks[index].Quantity - moved;
            if (remaining <= 0) stacks.RemoveAt(index);
            else stacks[index] = new ItemStack(itemId, remaining);
            return moved;
        }

        public BankSaveData Capture()
        {
            BankSaveData data = new BankSaveData { Ids = new string[stacks.Count], Quantities = new int[stacks.Count] };
            for (int i = 0; i < stacks.Count; i++)
            {
                data.Ids[i] = stacks[i].ItemId;
                data.Quantities[i] = stacks[i].Quantity;
            }
            return data;
        }

        public void Restore(BankSaveData data)
        {
            stacks.Clear();
            if (data == null || data.Ids == null || data.Quantities == null) return;
            for (int i = 0; i < data.Ids.Length && i < data.Quantities.Length; i++)
            {
                if (!string.IsNullOrEmpty(data.Ids[i]) && data.Quantities[i] > 0) stacks.Add(new ItemStack(data.Ids[i], data.Quantities[i]));
            }
        }

        int Find(string itemId)
        {
            for (int i = 0; i < stacks.Count; i++) if (stacks[i].ItemId == itemId) return i;
            return -1;
        }
    }

    /// <summary>Buying and selling with coins. Shops have unlimited stock. Pure C#.</summary>
    public static class ShopLogic
    {
        public const string Coins = "coins";

        public static bool TryBuy(InventoryModel inventory, string itemId, int quantity, out string message)
        {
            message = null;
            ItemInfo info = GameContent.Item(itemId);
            if (info == null || quantity <= 0) { message = "That isn't for sale."; return false; }

            int price = GameContent.BuyPrice(itemId);
            int affordable = price > 0 ? inventory.Count(Coins) / price : 0;
            int amount = Math.Min(quantity, affordable);
            if (amount <= 0) { message = "You can't afford that (" + price + " coins)."; return false; }

            if (!info.Stackable) amount = Math.Min(amount, inventory.FreeSlots());
            else if (!inventory.CanAdd(itemId, 1)) amount = 0;
            if (amount <= 0) { message = "You don't have enough inventory space."; return false; }

            inventory.Remove(Coins, amount * price);
            int left = inventory.Add(itemId, amount);
            if (left > 0) inventory.Add(Coins, left * price);
            int bought = amount - left;
            message = "You buy " + (bought > 1 ? bought + "x " : "a ") + info.Name + " for " + (bought * price) + " coins.";
            return bought > 0;
        }

        public static bool TrySell(InventoryModel inventory, string itemId, int quantity, out string message)
        {
            message = null;
            ItemInfo info = GameContent.Item(itemId);
            int price = GameContent.SellPrice(itemId);
            if (info == null || itemId == Coins) { message = "You can't sell that."; return false; }
            if (price <= 0) { message = "The shopkeeper doesn't want your " + info.Name.ToLower() + "."; return false; }

            int amount = Math.Min(quantity, inventory.Count(itemId));
            if (amount <= 0) return false;
            inventory.Remove(itemId, amount);
            // coins need a slot unless you already carry some (selling usually frees one)
            if (!inventory.CanAdd(Coins, amount * price))
            {
                inventory.Add(itemId, amount);
                message = "You don't have room for the coins.";
                return false;
            }
            inventory.Add(Coins, amount * price);
            message = "You sell " + (amount > 1 ? amount + "x " : "the ") + info.Name + " for " + (amount * price) + " coins.";
            return true;
        }
    }
}
