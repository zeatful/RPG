using System.Collections.Generic;
using RPG.Saving;
using UnityEngine;

namespace RPG.Drops
{
    /// <summary>Holds the player's bad-luck counters for rare drops, saved with the game.</summary>
    public class DropLuck : MonoBehaviour, ISaveable
    {
        public BadLuckTracker Tracker { get; private set; }

        private void Awake()
        {
            Tracker = new BadLuckTracker();
        }

        public object CaptureState()
        {
            return new Dictionary<string, int>(Tracker.DryStreaks);
        }

        public void RestoreState(object state)
        {
            Dictionary<string, int> saved = state as Dictionary<string, int>;
            if (saved == null) return;
            Tracker.DryStreaks = new Dictionary<string, int>(saved);
        }
    }
}
