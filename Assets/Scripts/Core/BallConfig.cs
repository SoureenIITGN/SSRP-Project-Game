using UnityEngine;

namespace SSRP.Core
{
    [CreateAssetMenu(fileName = "BallConfig", menuName = "SSRP/Configs/Ball Config")]
    public class BallConfig : ScriptableObject
    {
        [Tooltip("Name shown in Match Setup (e.g. Slow / Normal / Fast).")]
        public string presetName = "Normal";

        [Min(0f), Tooltip("Base speed in units per second.")]
        public float speed = 8f;

        [Range(0f, 90f), Tooltip("Outgoing angle when the ball hits the very edge of a paddle.")]
        public float maxBounceAngle = 60f;

        [Range(0f, 1f), Tooltip("Anti-stall: the direction's |x| never drops below this.")]
        public float minimumHorizontalDirection = 0.25f;

        [Min(0f), Tooltip("'English': how much paddle velocity bends the outgoing direction.")]
        public float paddleVelocityInfluence;

        [Min(1f), Tooltip("Speed multiplier applied per paddle hit (speed = base * ramp^hits).")]
        public float hitSpeedRamp = 1.04f;

        [Min(1f), Tooltip("Cap for ramp^hits.")]
        public float maxSpeedMultiplier = 1.8f;

        [Range(0f, 89f), Tooltip("Serve direction is random within +/- this angle (degrees).")]
        public float serveAngleRange = 25f;
    }
}
