using UnityEngine;

namespace SSRP.Gameplay
{
    /// <summary>
    /// [C6] All parry numbers (normalised pressure 0-1, seconds).
    /// Asset: Assets/ScriptableObjects/Gameplay/ParryConfig.asset - edit it in the Inspector (also during Play).
    /// </summary>
    [CreateAssetMenu(fileName = "ParryConfig", menuName = "SSRP/Configs/Parry Config")]
    public class ParryConfig : ScriptableObject
    {
        [Header("Trigger (input)")]
        [Range(0f, 1f), Tooltip("Both sensors must be at least this hard: min(Up, Down) >= threshold.")]
        public float pressThreshold = 0.55f;

        [Range(0f, 1f), Tooltip("And roughly balanced: |Up - Down| <= tolerance. Larger = easier to trigger " +
                                "when the two presses are not perfectly simultaneous.")]
        public float balanceTolerance = 0.35f;

        [Min(0f), Tooltip("How long both must be held before the parry fires (adds input lag). " +
                          "A switch Up -> Down never reaches the threshold on both sensors, so this can stay tiny.")]
        public float holdTime = 0.02f;

        [Min(0f), Tooltip("Seconds after a parry before the next one can fire.")]
        public float cooldown = 0.8f;

        [Tooltip("Both sensors must be released (below 70% of the threshold) before another parry can fire, " +
                 "so holding both does not auto-repeat.")]
        public bool requireRelease = true;

        [Header("Successful parry (the ball meets the parry)")]
        [Min(0f), Tooltip("A parry counts if the ball touches the paddle within this many seconds AFTER the parry fires.")]
        public float activeWindow = 0.3f;

        [Min(0f), Tooltip("Forgiveness: a parry pressed this many seconds AFTER the ball touched the paddle still counts.")]
        public float lateGrace = 0.1f;

        [Min(1f), Tooltip("Ball speed multiplier while it is on fire.")]
        public float speedMultiplier = 1.5f;

        [Min(0.1f), Tooltip("Safety limit: the fire ends after this long even if the other paddle never touches the ball.")]
        public float fireMaxDuration = 6f;

        [Header("Paddle flash")]
        public Color flashColour = Color.white;

        [Min(0.01f)] public float flashDuration = 0.2f;

        public Color successFlashColour = new Color(1f, 0.5f, 0.1f);

        [Min(0.01f)] public float successFlashDuration = 0.4f;

        [HideInInspector, Tooltip("Used by SSRP > Build Project to upgrade older assets once.")]
        public int dataVersion;

        public const int CurrentDataVersion = 2;

        /// <summary>Reset every value to the defaults above (used once to upgrade older assets).</summary>
        public void ApplyDefaults()
        {
            pressThreshold = 0.55f;
            balanceTolerance = 0.35f;
            holdTime = 0.02f;
            cooldown = 0.8f;
            requireRelease = true;
            activeWindow = 0.3f;
            lateGrace = 0.1f;
            speedMultiplier = 1.5f;
            fireMaxDuration = 6f;
            flashColour = Color.white;
            flashDuration = 0.2f;
            successFlashColour = new Color(1f, 0.5f, 0.1f);
            successFlashDuration = 0.4f;
            dataVersion = CurrentDataVersion;
        }
    }
}
