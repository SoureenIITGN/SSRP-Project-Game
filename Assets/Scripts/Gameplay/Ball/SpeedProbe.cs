using UnityEngine;

namespace SSRP.Gameplay
{
    /// <summary>
    /// [B1/B2] Measures speed error every physics step and shows it on screen.
    /// Done-when: error stays within +/-1% of the expected speed.
    /// </summary>
    public class SpeedProbe : MonoBehaviour
    {
        [SerializeField] private BallController ball;

        private float worstError;
        private float lastError;
        private float elapsed;
        private float escapeDistance;

        public float WorstError => worstError;

        private void FixedUpdate()
        {
            if (ball == null || ball.IsFrozen)
                return;

            float expected = ball.CurrentSpeed;

            if (expected <= 0f)
                return;

            lastError = Mathf.Abs(ball.Body.linearVelocity.magnitude - expected) / expected;
            worstError = Mathf.Max(worstError, lastError);
            elapsed += Time.fixedDeltaTime;
            escapeDistance = Mathf.Max(escapeDistance, ball.Position.magnitude);
        }

        private void OnGUI()
        {
            GUI.Label(new Rect(10, 10, 700, 25),
                $"t = {elapsed:0}s   speed error now {lastError * 100f:0.000}%   worst {worstError * 100f:0.000}%   " +
                $"max distance from centre {escapeDistance:0.00}   (Space = re-serve)");
        }
    }
}
