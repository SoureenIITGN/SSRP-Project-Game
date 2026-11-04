using UnityEngine;
using SSRP.Core;

namespace SSRP.Gameplay
{
    /// <summary>
    /// [B3] Classic Pong control: WHERE the ball hits the paddle sets the outgoing angle.
    ///
    ///   offset = clamp((ball.y - paddle.y) / (Height / 2), -1, 1)
    ///   angle  = offset * maxBounceAngle          (default 60 degrees)
    ///   x direction points away from the paddle   (mirrored by side)
    ///   optional 'english': the paddle's vertical velocity bends the direction
    ///
    /// Finds paddles only through IPaddleInfo, so it never depends on PaddleController.
    /// Sits on the ball next to BallController.
    /// </summary>
    [RequireComponent(typeof(BallController))]
    public class PaddleHitResolver : MonoBehaviour
    {
        private BallController ball;

        private void Awake()
        {
            ball = GetComponent<BallController>();
        }

        private void OnCollisionEnter2D(Collision2D collision)
        {
            if (!collision.gameObject.TryGetComponent(out IPaddleInfo paddle))
                return;

            BallConfig config = ball.Config;
            float maxAngle = config != null ? config.maxBounceAngle : 60f;
            float influence = config != null ? config.paddleVelocityInfluence : 0f;

            Vector2 ballPosition = ball.Position;
            Vector2 paddlePosition = collision.transform.position;

            float offset = ComputeOffset(ballPosition.y, paddlePosition.y, paddle.Height);
            float sideSign = ballPosition.x >= paddlePosition.x ? 1f : -1f;

            ball.RegisterPaddleHit();

            Vector2 outgoing = ComputeBounceDirection(offset, maxAngle, sideSign,
                paddle.Velocity.y, influence, ball.CurrentSpeed);

            ball.SetDirection(outgoing);
            GameEvents.RaiseBallHitPaddle();
        }

        /// <summary>Hit offset from the paddle centre, -1 (bottom edge) to +1 (top edge).</summary>
        public static float ComputeOffset(float ballY, float paddleY, float paddleHeight)
        {
            float half = Mathf.Max(0.0001f, paddleHeight * 0.5f);
            return Mathf.Clamp((ballY - paddleY) / half, -1f, 1f);
        }

        /// <summary>
        /// Outgoing unit direction. <paramref name="sideSign"/> is +1 to send the ball right, -1 left.
        /// English adds paddleVelocityY * influence / speed to the y component before normalising.
        /// </summary>
        public static Vector2 ComputeBounceDirection(float offset, float maxBounceAngleDeg, float sideSign,
            float paddleVelocityY = 0f, float influence = 0f, float ballSpeed = 1f)
        {
            float angle = Mathf.Clamp(offset, -1f, 1f) * maxBounceAngleDeg * Mathf.Deg2Rad;
            var dir = new Vector2(Mathf.Cos(angle) * sideSign, Mathf.Sin(angle));

            if (influence > 0f && ballSpeed > 0.0001f)
                dir.y += paddleVelocityY * influence / ballSpeed;

            return dir.normalized;
        }
    }
}
