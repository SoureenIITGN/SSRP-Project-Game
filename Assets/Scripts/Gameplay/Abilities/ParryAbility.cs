using UnityEngine;
using SSRP.Core;

namespace SSRP.Gameplay
{
    /// <summary>
    /// [C6] Parry as an add-on IPaddleAbility (no edits to Core, PaddleController or the input pipeline).
    ///
    /// Trigger: min(Up, Down) >= pressThreshold AND |Up - Down| <= balanceTolerance for holdTime, then a
    /// cooldown (and, optionally, both sensors must be released before the next parry).
    /// Raises GameEvents.ParryTriggered(playerId) and flashes the paddle white.
    ///
    /// SUCCESSFUL parry: the ball touches this paddle within <c>activeWindow</c> after the parry fired, or the
    /// parry fires within <c>lateGrace</c> after the ball touched it. Then the ball catches fire
    /// (BallFireEffect: orange colour + fiery trail), gets a speed boost through the modifier stack, the
    /// paddle flashes orange and GameEvents.ParrySucceeded(playerId) is raised (sound, hit-stop, banner).
    /// The paddle receives ball contacts through IBallHittable (BallController notifies it).
    /// </summary>
    public class ParryAbility : MonoBehaviour, IPaddleAbility, IBallHittable
    {
        [SerializeField] private ParryConfig config;
        [SerializeField, Tooltip("Renderer to flash (defaults to the first SpriteRenderer).")]
        private SpriteRenderer flashRenderer;

        private IPaddleInfo paddle;
        private float held;
        private float cooldownLeft;
        private bool waitingForRelease;
        private float lastTriggerTime = -99f;
        private float succeededTriggerTime = -98f;
        private float lastContactTime = -99f;
        private BallController lastContactBall;

        private float flashLeft;
        private float flashLength = 0.2f;
        private Color flashFrom = Color.white;
        private Color restColour;
        private bool flashing;

        public bool IsOnCooldown => cooldownLeft > 0f;
        public int TriggerCount { get; private set; }
        public int SuccessCount { get; private set; }

        /// <summary>True while a parry that just fired can still meet the ball.</summary>
        public bool IsWindowOpen => config != null && Time.time - lastTriggerTime <= config.activeWindow
                                    && succeededTriggerTime != lastTriggerTime;

        private int PlayerId => paddle != null ? paddle.PlayerId : 0;

        private void Awake()
        {
            paddle = GetComponent<IPaddleInfo>();

            if (flashRenderer == null)
                flashRenderer = GetComponentInChildren<SpriteRenderer>();
        }

        public void SetConfig(ParryConfig parryConfig) => config = parryConfig;

        public void Tick(PlayerInputState input, float dt)
        {
            UpdateFlash(dt);

            if (config == null || dt <= 0f)
                return;

            if (cooldownLeft > 0f)
                cooldownLeft -= dt;

            float weaker = Mathf.Min(input.Up, input.Down);

            if (waitingForRelease)
            {
                if (weaker < config.pressThreshold * 0.7f)
                    waitingForRelease = false;

                held = 0f;
                return;
            }

            if (cooldownLeft > 0f)
            {
                held = 0f;
                return;
            }

            bool firm = weaker >= config.pressThreshold;
            bool balanced = Mathf.Abs(input.Up - input.Down) <= config.balanceTolerance;

            held = firm && balanced ? held + dt : 0f;

            if (held + 1e-5f < config.holdTime || !firm || !balanced)
                return;

            Trigger();
        }

        private void Trigger()
        {
            held = 0f;
            cooldownLeft = config.cooldown;
            waitingForRelease = config.requireRelease;
            lastTriggerTime = Time.time;
            TriggerCount++;

            GameEvents.RaiseParryTriggered(PlayerId);
            StartFlash(config.flashColour, config.flashDuration);

            // Late parry: the ball touched the paddle a moment before the press.
            if (lastContactBall != null && Time.time - lastContactTime <= config.lateGrace)
                Succeed(lastContactBall);
        }

        /// <summary>Called by BallController whenever the ball touches this paddle.</summary>
        public void OnBallHit(BallHitInfo info)
        {
            if (info.Other == null || !info.Other.TryGetComponent(out BallController ball))
                return;

            lastContactTime = Time.time;
            lastContactBall = ball;

            if (IsWindowOpen)
                Succeed(ball);
        }

        private void Succeed(BallController ball)
        {
            if (config == null || ball == null || succeededTriggerTime == lastTriggerTime)
                return;

            succeededTriggerTime = lastTriggerTime;   // one success per parry
            lastContactBall = null;
            SuccessCount++;

            ball.Modifiers.Add(BallFireEffect.ModifierKey, new SpeedMultiplierModifier(config.speedMultiplier),
                config.fireMaxDuration);

            if (!ball.TryGetComponent(out BallFireEffect fire))
                fire = ball.gameObject.AddComponent<BallFireEffect>();

            fire.Ignite(PlayerId);
            StartFlash(config.successFlashColour, config.successFlashDuration);
            GameEvents.RaiseParrySucceeded(PlayerId);
        }

        private void StartFlash(Color colour, float duration)
        {
            if (flashRenderer == null)
                return;

            if (!flashing)
                restColour = flashRenderer.color;

            flashing = true;
            flashFrom = colour;
            flashLength = Mathf.Max(0.01f, duration);
            flashLeft = flashLength;
            flashRenderer.color = colour;
        }

        private void UpdateFlash(float dt)
        {
            if (!flashing || flashRenderer == null)
                return;

            flashLeft -= dt;
            float t = Mathf.Clamp01(1f - flashLeft / flashLength);
            flashRenderer.color = Color.Lerp(flashFrom, restColour, t);

            if (flashLeft <= 0f)
            {
                flashRenderer.color = restColour;
                flashing = false;
            }
        }
    }
}
