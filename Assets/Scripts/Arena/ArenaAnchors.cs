using UnityEngine;
using SSRP.Core;
using SSRP.Gameplay;

namespace SSRP.Arena
{
    /// <summary>
    /// [D4] Lives on the root of every arena prefab and tells the game where things are:
    /// playable bounds (local space), paddle spawn points, ball spawn and the two goal zones.
    /// </summary>
    public class ArenaAnchors : MonoBehaviour
    {
        [Header("Playable area (local space; paddles are clamped to it)")]
        [SerializeField] private Vector2 boundsMin = new Vector2(-9f, -5f);
        [SerializeField] private Vector2 boundsMax = new Vector2(9f, 5f);

        [Header("Camera")]
        [SerializeField, Tooltip("Extra world units shown around the bounds (walls, goals).")]
        private float cameraMargin = 1.2f;

        [Header("Spawn points")]
        [SerializeField] private Transform leftPaddleSpawn;
        [SerializeField] private Transform rightPaddleSpawn;
        [SerializeField] private Transform ballSpawn;

        [Header("Goals")]
        [SerializeField] private GoalZone leftGoal;
        [SerializeField] private GoalZone rightGoal;

        public float CameraMargin => cameraMargin;

        public ArenaBounds Bounds
        {
            get
            {
                Vector2 a = transform.TransformPoint(boundsMin);
                Vector2 b = transform.TransformPoint(boundsMax);
                return new ArenaBounds(Vector2.Min(a, b), Vector2.Max(a, b));
            }
        }

        public Vector2 BallSpawn => ballSpawn != null ? (Vector2)ballSpawn.position : Centre;

        public Vector2 Centre
        {
            get
            {
                ArenaBounds b = Bounds;
                return (b.Min + b.Max) * 0.5f;
            }
        }

        public Vector2 GetPaddleSpawn(PaddleSide side)
        {
            Transform t = side == PaddleSide.Left ? leftPaddleSpawn : rightPaddleSpawn;

            if (t != null)
                return t.position;

            ArenaBounds b = Bounds;
            float x = side == PaddleSide.Left ? b.Min.x + 0.8f : b.Max.x - 0.8f;
            return new Vector2(x, Centre.y);
        }

        /// <summary>The goal behind <paramref name="side"/> (the ball entering it scores for the other side).</summary>
        public GoalZone GetGoal(PaddleSide side) => side == PaddleSide.Left ? leftGoal : rightGoal;

        private void OnDrawGizmos()
        {
            ArenaBounds b = Bounds;
            Gizmos.color = new Color(0.3f, 0.9f, 1f, 0.8f);
            Vector3 centre = (b.Min + b.Max) * 0.5f;
            Gizmos.DrawWireCube(centre, b.Max - b.Min);

            Gizmos.color = Color.yellow;
            if (leftPaddleSpawn != null) Gizmos.DrawWireSphere(leftPaddleSpawn.position, 0.3f);
            if (rightPaddleSpawn != null) Gizmos.DrawWireSphere(rightPaddleSpawn.position, 0.3f);
            Gizmos.color = Color.white;
            Gizmos.DrawWireSphere(BallSpawn, 0.25f);
        }
    }
}
