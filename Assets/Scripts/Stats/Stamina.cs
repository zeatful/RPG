using UnityEngine;

namespace RPG.Stats
{
    /// <summary>
    /// Run energy (0-100). Running drains it; walking or standing refills it. When it runs out you drop
    /// to a walk and have to switch running back on. Pure C# so it can be unit tested.
    /// </summary>
    public class StaminaModel
    {
        public const float Max = 100f;
        public const float DrainPerSecond = 1.6f;    // about a minute of running from full
        public const float WalkRegenPerSecond = 0.6f;
        public const float RestRegenPerSecond = 1.2f;
        public const float WalkSpeed = 0.55f;          // fraction of full speed when walking

        public float Energy = Max;
        public bool Running = true;

        public float SpeedMultiplier { get { return Running && Energy > 0f ? 1f : WalkSpeed; } }

        /// <summary>Advance time. moving = the character is actually moving this frame.</summary>
        public void Tick(float seconds, bool moving)
        {
            if (moving && Running && Energy > 0f)
            {
                Energy = Mathf.Max(0f, Energy - DrainPerSecond * seconds);
                if (Energy <= 0f) Running = false;
            }
            else
            {
                Energy = Mathf.Min(Max, Energy + (moving ? WalkRegenPerSecond : RestRegenPerSecond) * seconds);
            }
        }

        /// <summary>Toggle running; returns false if there's no energy to run.</summary>
        public bool Toggle()
        {
            if (!Running && Energy < 1f) return false;
            Running = !Running;
            return true;
        }
    }

    /// <summary>The player's run energy. R (or the HUD button) toggles walk/run.</summary>
    public class Stamina : MonoBehaviour
    {
        public StaminaModel Model { get; private set; }

        UnityEngine.AI.NavMeshAgent agent;
        RPG.Movement.Mover mover;
        bool wasRunning;

        private void Awake()
        {
            Model = new StaminaModel();
            agent = GetComponent<UnityEngine.AI.NavMeshAgent>();
            mover = GetComponent<RPG.Movement.Mover>();
            wasRunning = Model.Running;
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.R) && !RPG.Core.UiState.Blocked && !RPG.Core.UiState.Typing) Toggle();
            bool moving = (agent != null && agent.enabled && agent.velocity.magnitude > 0.4f) || (mover != null && mover.ManualSpeed > 0.4f);
            Model.Tick(Time.deltaTime, moving);
            if (wasRunning && !Model.Running) RPG.Core.GameLog.Add("You're out of breath and slow to a walk.", RPG.Core.GameLog.Warning);
            wasRunning = Model.Running;
        }

        public void Toggle()
        {
            if (!Model.Toggle()) RPG.Core.GameLog.Add("You're too tired to run.", RPG.Core.GameLog.Warning);
            wasRunning = Model.Running;
        }

        public float SpeedMultiplier { get { return Model.SpeedMultiplier; } }
    }
}
