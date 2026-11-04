using UnityEngine;

namespace SSRP.Core
{
    [CreateAssetMenu(fileName = "PaddleConfig", menuName = "SSRP/Configs/Paddle Config")]
    public class PaddleConfig : ScriptableObject
    {
        [Min(0f)]
        public float maxSpeed = 8f;

        [Min(0f)]
        public float height = 2f;

        [Range(0f, 1f)]
        public float deadzone = 0.05f;
    }
}
