using RPG.Saving;
using UnityEngine;

namespace RPG.SceneManagement
{
    /// <summary>
    /// Lives on the Saving prefab next to the SavingSystem. Saving is per character now and is driven by
    /// RPG.Game.GameSession (autosave, Esc menu, F5 quick-save), so S / L no longer save and load:
    /// those keys are used for WASD movement.
    /// </summary>
    public class SavingWrapper : MonoBehaviour
    {
        public SavingSystem System { get { return GetComponent<SavingSystem>(); } }
    }
}
