using UnityEngine;

namespace SSRP.Core
{
    [CreateAssetMenu(fileName = "BallConfig", menuName = "SSRP/Configs/Ball Config")]
    public class BallConfig : ScriptableObject
    {
        [Min(0f)]
        public float speed = 8f;

        [Range(0f, 90f)]
        public float maxBounceAngle = 60f;

        [Range(0f, 1f)]
        public float minimumHorizontalDirection = 0.25f;

        [Min(0f)]
        public float paddleVelocityInfluence;
    }
}
