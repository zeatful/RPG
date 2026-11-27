using System;
using RPG.Content;
using RPG.Core;
using RPG.Saving;
using RPG.Stats;
using UnityEngine;
using Random = UnityEngine.Random;

namespace RPG.Items
{
    /// <summary>Unity wrapper around InventoryModel: saving, equipping, eating, dropping, recipes.</summary>
    public class Inventory : MonoBehaviour, ISaveable
    {
        public InventoryModel Model { get; private set; }

        public event Action onChanged;
        public event Action<ItemInfo> onEquipped;
        public event Action onAte;

        SkillSet skills;

        private void Awake()
        {
            Model = new InventoryModel(GameContent.Item);
            skills = GetComponent<SkillSet>();
        }

        int LevelOf(Skill skill)
        {
            if (skills == null) skills = GetComponent<SkillSet>();
            return skills != null ? skills.GetLevel(skill) : 1;
        }

        /// <summary>Returns true if everything fit.</summary>
        public bool TryAdd(string itemId, int quantity)
        {
            int left = Model.Add(itemId, quantity);
            NotifyChanged();
            return left == 0;
        }

        public void UseSlot(int index)
        {
            ItemStack stack = Model.GetSlot(index);
            if (stack.IsEmpty) return;
            ItemInfo info = GameContent.Item(stack.ItemId);
            if (info == null) return;

            if (info.IsPet)
            {
                PetFollower.Toggle(info.Id, transform);
            }
            else if (info.IsEquippable)
            {
                string reason;
                if (Model.Equip(index, LevelOf, out reason))
                {
                    GameLog.Add(info.Slot == EquipSlot.Ammo ? "You equip " + Model.AmmoCount + " " + info.Name.ToLower() + "." : "You equip the " + info.Name + ".");
                    if (onEquipped != null) onEquipped(info);
                }
                else GameLog.Add(reason, GameLog.Warning);
            }
            else if (info.IsEdible)
            {
                Health health = GetComponent<Health>();
                if (health == null || health.IsDead()) return;
                Model.Remove(info.Id, 1);
                health.Heal(info.HealAmount);
                GameLog.Add("You eat the " + info.Name + ". It heals " + info.HealAmount + ".");
                if (onAte != null) onAte();
            }
            else if (GameContent.Burnables.ContainsKey(info.Id))
            {
                RPG.World.Firemaker firemaker = GetComponent<RPG.World.Firemaker>();
                if (firemaker != null) firemaker.Light(info.Id);
            }

            else
            {
                GameLog.Add(string.IsNullOrEmpty(info.Description) ? info.Name + "." : info.Description);
            }
            NotifyChanged();
        }

        public void DropSlot(int index)
        {
            ItemStack stack = Model.RemoveSlot(index);
            if (stack.IsEmpty) return;
            Pickup.Spawn(stack, transform.position + transform.forward * 0.8f, false);
            NotifyChanged();
        }

        public void Unequip(EquipSlot slot)
        {
            ItemInfo info = GameContent.Item(Model.Worn(slot));
            if (!Model.Unequip(slot)) GameLog.Add("Not enough inventory space.", GameLog.Warning);
            else if (info != null && onEquipped != null) onEquipped(info);
            NotifyChanged();
        }

        /// <summary>Makes a recipe once. Returns true if something was made (burnt food counts).</summary>
        public bool Make(Recipe recipe)
        {
            if (!RPG.World.Station.IsNear(recipe.Station, transform.position))
            {
                GameLog.Add("You need to be next to " + RPG.World.Station.Describe(recipe.Station) + " to do that.", GameLog.Warning);
                return false;
            }
            string reason;
            bool burnt;
            if (!recipe.TryMake(Model, LevelOf, Random.value, out reason, out burnt))
            {
                GameLog.Add(reason, GameLog.Warning);
                return false;
            }
            ItemInfo output = GameContent.Item(recipe.Output.ItemId);
            string outputName = output != null ? output.Name : recipe.Output.ItemId;
            if (burnt)
            {
                GameLog.Add("You accidentally burn the " + outputName.ToLower() + ".", GameLog.Warning);
            }
            else
            {
                if (skills != null) skills.AddXp(recipe.Skill, recipe.Xp);
                string verb = recipe.Station == StationKind.Furnace ? "smelt" : recipe.Station == StationKind.Anvil ? "hammer out" : recipe.Station == StationKind.Fire ? "cook" : "make";
                GameLog.Add("You " + verb + " " + (recipe.Output.Quantity > 1 ? recipe.Output.Quantity + " " + outputName.ToLower() : "a " + outputName.ToLower()) + ".");
            }
            NotifyChanged();
            return true;
        }

        public bool CanMake(Recipe recipe)
        {
            return recipe.CanMake(Model, LevelOf);
        }

        /// <summary>The wielded tool of this kind if the player has the level to use it, or null.</summary>
        public ItemInfo EquippedTool(ToolKind kind)
        {
            ItemInfo weapon = GameContent.Item(Model.WeaponId);
            if (weapon == null || weapon.Tool != kind || LevelOf(weapon.RequiredSkill) < weapon.RequiredLevel) return null;
            return weapon;
        }

        /// <summary>Best tool of a kind in the bag that the player has the level to use, or null.</summary>
        public ItemInfo BestTool(ToolKind kind)
        {
            ItemInfo best = null;
            for (int i = 0; i < InventoryModel.SlotCount; i++)
            {
                ItemStack stack = Model.GetSlot(i);
                if (stack.IsEmpty) continue;
                ItemInfo info = GameContent.Item(stack.ItemId);
                if (info == null || info.Tool != kind || LevelOf(info.RequiredSkill) < info.RequiredLevel) continue;
                if (best == null || info.ToolPower > best.ToolPower) best = info;
            }
            return best;
        }

        /// <summary>True if the bag (or weapon slot) has a tool of this kind at all (even one you can't use yet).</summary>
        public bool HasAnyTool(ToolKind kind)
        {
            ItemInfo weapon = GameContent.Item(Model.WeaponId);
            if (weapon != null && weapon.Tool == kind) return true;
            for (int i = 0; i < InventoryModel.SlotCount; i++)
            {
                ItemStack stack = Model.GetSlot(i);
                if (stack.IsEmpty) continue;
                ItemInfo info = GameContent.Item(stack.ItemId);
                if (info != null && info.Tool == kind) return true;
            }
            return false;
        }

        public bool IsEmpty()
        {
            return Model.FreeSlots() == InventoryModel.SlotCount && !Model.HasAnythingWorn();
        }

        public void NotifyChanged()
        {
            if (onChanged != null) onChanged();
        }

        public object CaptureState()
        {
            return Model.Capture();
        }

        public void RestoreState(object state)
        {
            InventorySaveData data = state as InventorySaveData;
            if (data == null) return;
            Model.Restore(data);
            NotifyChanged();
        }
    }
}
