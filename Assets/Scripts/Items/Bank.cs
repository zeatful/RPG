using RPG.Core;
using RPG.Saving;
using UnityEngine;

namespace RPG.Items
{
    /// <summary>The player's bank (shared by every bank chest). Saved with the player.</summary>
    public class Bank : MonoBehaviour, ISaveable
    {
        public BankModel Model { get; private set; }

        Inventory inventory;

        private void Awake()
        {
            Model = new BankModel();
            inventory = GetComponent<Inventory>();
        }

        public void Deposit(string itemId, int quantity)
        {
            int moved = Model.Deposit(inventory.Model, itemId, quantity);
            if (moved == 0) GameLog.Add("Your bank is full.", GameLog.Warning);
            inventory.NotifyChanged();
        }

        public void DepositAll()
        {
            Model.DepositAll(inventory.Model);
            inventory.NotifyChanged();
        }

        public void Withdraw(string itemId, int quantity)
        {
            int moved = Model.Withdraw(inventory.Model, itemId, quantity);
            if (moved == 0) GameLog.Add("You don't have enough inventory space.", GameLog.Warning);
            inventory.NotifyChanged();
        }

        public object CaptureState()
        {
            return Model.Capture();
        }

        public void RestoreState(object state)
        {
            Model.Restore(state as BankSaveData);
        }
    }
}
