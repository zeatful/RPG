using System;
using RPG.Content;
using RPG.Core;
using RPG.Saving;
using RPG.Stats;
using UnityEngine;

namespace RPG.Items
{
    /// <summary>Unity wrapper around InventoryModel: saving, equipping, eating, dropping, recipes.</summary>
    public class Inventory : MonoBehaviour, ISaveable
    {
        public InventoryModel Model { get; private set; }

        public event Action onChanged;

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
                if (Model.Equip(index, LevelOf, out reason)) GameLog.Add("You equip the " + info.Name + ".");
                else GameLog.Add(reason, GameLog.Warning);
            }
            else if (info.IsEdible)
            {
                Health health = GetComponent<Health>();
                if (health == null || health.IsDead()) return;
                Model.Remove(info.Id, 1);
                health.Heal(info.HealAmount);
                GameLog.Add("You eat the " + info.Name + ". It heals " + info.HealAmount + ".");
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
            if (!Model.Unequip(slot)) GameLog.Add("Not enough inventory space.", GameLog.Warning);
            NotifyChanged();
        }

        public void Make(Recipe recipe)
        {
            string reason;
            if (!recipe.TryMake(Model, LevelOf, out reason))
            {
                GameLog.Add(reason, GameLog.Warning);
                return;
            }
            if (skills != null) skills.AddXp(recipe.Skill, recipe.Xp);
            ItemInfo output = GameContent.Item(recipe.Output.ItemId);
            GameLog.Add("You make a " + (output != null ? output.Name : recipe.Output.ItemId) + ".");
            NotifyChanged();
        }

        public bool CanMake(Recipe recipe)
        {
            return recipe.CanMake(Model, LevelOf);
        }

        public bool IsEmpty()
        {
            return Model.FreeSlots() == InventoryModel.SlotCount
                && string.IsNullOrEmpty(Model.WeaponId) && string.IsNullOrEmpty(Model.BodyId);
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
