using UnityEngine;
using SSRP.Core;

namespace SSRP.Gameplay
{
    /// <summary>
    /// [C6] Extension proof: parry as an add-on IPaddleAbility. Zero edits to Core, PaddleController
    /// or the input pipeline - just add this component to the paddle prefab.
    ///
    /// Trigger: min(Up, Down) >= pressThreshold AND |Up - Down| <= balanceTolerance, held for holdTime,
    /// then a cooldown. Moving from 'up' to 'down' passes briefly through a balanced state, which the
    /// hold time filters out. Balanced pressure also gives Velocity ~ 0, so the paddle braces in place.
    ///
    /// Raises GameEvents.ParryTriggered(playerId) and flashes the paddle. (Ball-reflect effect: future work.)
    /// </summary>
    public class ParryAbility : MonoBehaviour, IPaddleAbility
    {
        [SerializeField] private ParryConfig config;
        [SerializeField, Tooltip("Renderer to flash (defaults to the first SpriteRenderer).")]
        private SpriteRenderer flashRenderer;

        private IPaddleInfo paddle;
        private float held;
        private float cooldownLeft;
        private float flashLeft;
        private Color restColour;
        private bool flashing;

        public bool IsOnCooldown => cooldownLeft > 0f;
        public int TriggerCount { get; private set; }

        private void Awake()
        {
            paddle = GetComponent<IPaddleInfo>();

            if (flashRenderer == null)
                flashRenderer = GetComponentInChildren<SpriteRenderer>();
        }

        public void Tick(PlayerInputState input, float dt)
        {
            UpdateFlash(dt);

            if (config == null || dt <= 0f)
                return;

            if (cooldownLeft > 0f)
            {
                cooldownLeft -= dt;
                held = 0f;
                return;
            }

            bool firm = Mathf.Min(input.Up, input.Down) >= config.pressThreshold;
            bool balanced = Mathf.Abs(input.Up - input.Down) <= config.balanceTolerance;

            held = firm && balanced ? held + dt : 0f;

            if (held < config.holdTime)
                return;

            held = 0f;
            cooldownLeft = config.cooldown;
            TriggerCount++;
            GameEvents.RaiseParryTriggered(paddle != null ? paddle.PlayerId : 0);
            StartFlash();
        }

        private void StartFlash()
        {
            if (flashRenderer == null)
                return;

            if (!flashing)
                restColour = flashRenderer.color;

            flashing = true;
            flashLeft = config.flashDuration;
            flashRenderer.color = config.flashColour;
        }

        private void UpdateFlash(float dt)
        {
            if (!flashing || flashRenderer == null)
                return;

            flashLeft -= dt;
            float t = Mathf.Clamp01(1f - flashLeft / Mathf.Max(0.01f, config.flashDuration));
            flashRenderer.color = Color.Lerp(config.flashColour, restColour, t);

            if (flashLeft <= 0f)
            {
                flashRenderer.color = restColour;
                flashing = false;
            }
        }
    }
}
