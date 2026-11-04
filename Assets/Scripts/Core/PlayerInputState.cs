using UnityEngine;

namespace SSRP.Core
{
    public struct PlayerInputState
    {
        public float Up;
        public float Down;

        public float Velocity =>
            Mathf.Clamp(Up - Down, -1f, 1f);

        public float Sum => Up + Down;
    }
}