using UnityEngine;

namespace SSRP.Gameplay
{
    /// <summary>
    /// Dev-scene helper: serves the ball at a random angle on Start and re-serves with Space.
    /// Used in Dev_PhysicsSandbox and Dev_TileArena (no GameManager there).
    /// </summary>
    public class SandboxServer : MonoBehaviour
    {
        [SerializeField] private BallController ball;
        [SerializeField, Range(0f, 80f)] private float angleRange = 60f;

        private void Start()
        {
            ServeRandom();
        }

        private void Update()
        {
            var keyboard = UnityEngine.InputSystem.Keyboard.current;

            if (keyboard != null && keyboard.spaceKey.wasPressedThisFrame)
                ServeRandom();
        }

        public void ServeRandom()
        {
            if (ball == null)
                return;

            ball.ResetToCenter();
            float angle = Random.Range(-angleRange, angleRange) * Mathf.Deg2Rad;
            float side = Random.value < 0.5f ? -1f : 1f;
            ball.Serve(new Vector2(side * Mathf.Cos(angle), Mathf.Sin(angle)));
        }
    }
}
