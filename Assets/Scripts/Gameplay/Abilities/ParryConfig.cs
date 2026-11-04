using UnityEngine;

namespace SSRP.Gameplay
{
    /// <summary>[C6] All parry numbers (normalised pressure 0-1, seconds).</summary>
    [CreateAssetMenu(fileName = "ParryConfig", menuName = "SSRP/Configs/Parry Config")]
    public class ParryConfig : ScriptableObject
    {
        [Range(0f, 1f), Tooltip("Both sensors must be at least this hard: min(Up, Down) >= threshold.")]
        public float pressThreshold = 0.6f;

        [Range(0f, 1f), Tooltip("And balanced: |Up - Down| <= tolerance.")]
        public float balanceTolerance = 0.2f;

        [Min(0f), Tooltip("For this long (rejects the brief balanced moment when switching Up -> Down).")]
        public float holdTime = 0.12f;

        [Min(0f)] public float cooldown = 0.8f;

        public Color flashColour = Color.white;

        [Min(0.01f)] public float flashDuration = 0.25f;
    }
}
