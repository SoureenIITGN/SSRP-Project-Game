using UnityEngine;

namespace SSRP.Core
{
    [CreateAssetMenu(fileName = "PaddleConfig", menuName = "SSRP/Configs/Paddle Config")]
    public class PaddleConfig : ScriptableObject
    {
        [Min(0f), Tooltip("Paddle speed (units/s) at the configured normalisation limit.")]
        public float maxSpeed = 8f;

        [Min(0f)]
        public float height = 2f;

        [Min(0.05f)]
        public float width = 0.3f;

        [Range(0f, 1f), Tooltip("Per-channel deadzone applied to normalised sensor values.")]
        public float deadzone = 0.05f;

        [Range(0f, 1f), Tooltip("Upper limit for each normalised sensor channel.")]
        public float normalizationUpperLimit = 0.75f;

        [Range(0f, 1023f), Tooltip("Raw ADC value that maps to normalised 1.0.")]
        public float normalizationForceMax = 1023f;

        [Min(0f), Tooltip("Optional acceleration smoothing (units/s^2). 0 = respond instantly.")]
        public float acceleration;
    }
}
