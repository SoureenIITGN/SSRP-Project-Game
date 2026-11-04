using UnityEngine;
using SSRP.Core;

namespace SSRP.Gameplay
{
    /// <summary>
    /// [B3] Trigger at the left/right edge. When the ball enters, raises GoalScored(scoringPlayerId)
    /// exactly once per serve (BallController.TryConsumeGoal guards against double-firing,
    /// e.g. when the ball touches the trigger with two contacts or bounces inside it).
    /// The GameManager assigns <see cref="ScoringPlayerId"/> from the MatchConfig sides.
    /// </summary>
    [RequireComponent(typeof(BoxCollider2D))]
    public class GoalZone : MonoBehaviour
    {
        [SerializeField, Tooltip("The side this goal sits behind (the defender's side).")]
        private PaddleSide defendedSide = PaddleSide.Left;

        [SerializeField, Tooltip("Player who scores when the ball enters this zone.")]
        private int scoringPlayerId = 2;

        public PaddleSide DefendedSide => defendedSide;

        public int ScoringPlayerId
        {
            get => scoringPlayerId;
            set => scoringPlayerId = value;
        }

        private void Reset()
        {
            GetComponent<BoxCollider2D>().isTrigger = true;
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (!BallController.TryGet(other, out BallController ball))
                return;

            if (!ball.TryConsumeGoal())
                return;

            GameEvents.RaiseGoalScored(scoringPlayerId);
        }
    }

    /// <summary>[B3] Serve direction: toward the player who conceded, random angle within +/- range.</summary>
    public static class ServeMath
    {
        /// <param name="random01">A random number in [0, 1] (passed in so tests are deterministic).</param>
        public static Vector2 DirectionToward(PaddleSide side, float angleRangeDeg, float random01)
        {
            float angle = Mathf.Lerp(-angleRangeDeg, angleRangeDeg, Mathf.Clamp01(random01)) * Mathf.Deg2Rad;
            return new Vector2(side.Sign() * Mathf.Cos(angle), Mathf.Sin(angle));
        }
    }
}
