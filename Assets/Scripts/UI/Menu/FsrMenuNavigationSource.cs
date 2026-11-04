using UnityEngine;
using SSRP.Core;
using SSRP.Input;

namespace SSRP.UI
{
    /// <summary>
    /// [D2 stretch] Navigate menus with the FSRs through the normal pipeline (reads PlayerInputState):
    /// press UP / DOWN to move the highlight (repeats while held), press BOTH firmly to select.
    /// Only active when the player uses a non-keyboard source (keyboard W/S already navigates).
    /// </summary>
    [RequireComponent(typeof(PlayerSlot))]
    [RequireComponent(typeof(PlayerInputPipeline))]
    public class FsrMenuNavigationSource : MonoBehaviour, IMenuNavigationSource
    {
        [SerializeField, Range(0.1f, 1f)] private float moveThreshold = 0.5f;
        [SerializeField, Min(0.1f)] private float repeatDelay = 0.45f;
        [SerializeField, Range(0.1f, 1f)] private float selectThreshold = 0.6f;
        [SerializeField, Min(0f)] private float selectHold = 0.35f;
        [SerializeField, Min(0f)] private float selectCooldown = 0.8f;

        private PlayerSlot slot;
        private PlayerInputPipeline pipeline;
        private int pendingStep;
        private bool pendingSubmit;
        private int heldDirection;
        private float repeatTimer;
        private float bothHeld;
        private float cooldown;

        public bool IsActive
        {
            get
            {
                if (slot == null)
                    return false;

                InputSourceType t = slot.InputSourceType;
                return t != InputSourceType.Keyboard && t != InputSourceType.NoiseInjected;
            }
        }

        private void Awake()
        {
            slot = GetComponent<PlayerSlot>();
            pipeline = GetComponent<PlayerInputPipeline>();
        }

        public void SetInputSource(InputSourceType type)
        {
            if (slot == null)
                slot = GetComponent<PlayerSlot>();

            slot.SetInputSource(type);
        }

        private void Update()
        {
            float dt = Time.unscaledDeltaTime;
            PlayerInputState s = pipeline.State;

            if (cooldown > 0f)
                cooldown -= dt;

            // Both pressed = select (balanced pressure gives Velocity ~ 0 but a high minimum).
            if (Mathf.Min(s.Up, s.Down) >= selectThreshold)
            {
                bothHeld += dt;
                heldDirection = 0;

                if (bothHeld >= selectHold && cooldown <= 0f)
                {
                    pendingSubmit = true;
                    cooldown = selectCooldown;
                    bothHeld = 0f;
                }

                return;
            }

            bothHeld = 0f;
            int direction = s.Velocity > moveThreshold ? 1 : (s.Velocity < -moveThreshold ? -1 : 0);

            if (direction == 0)
            {
                heldDirection = 0;
                return;
            }

            if (direction != heldDirection)
            {
                heldDirection = direction;
                repeatTimer = repeatDelay;
                pendingStep = direction;
                return;
            }

            repeatTimer -= dt;

            if (repeatTimer <= 0f)
            {
                repeatTimer = repeatDelay;
                pendingStep = direction;
            }
        }

        public bool TryConsume(out int verticalStep, out bool submit)
        {
            verticalStep = pendingStep;
            submit = pendingSubmit;
            pendingStep = 0;
            pendingSubmit = false;
            return verticalStep != 0 || submit;
        }
    }
}
