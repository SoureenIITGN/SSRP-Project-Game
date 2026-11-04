using System.Collections.Generic;
using UnityEngine;
using SSRP.Core;

namespace SSRP.Gameplay
{
    /// <summary>What the match flow needs from a ball (lets the GameManager use a stub in tests).</summary>
    public interface IServeableBall
    {
        void Serve(Vector2 direction);
        void ResetToCenter();
        void Freeze();
        void Unfreeze();
    }

    /// <summary>
    /// [B2] Constant-speed Pong ball.
    ///
    ///  - Every FixedUpdate: velocity = direction * currentSpeed (elastic collisions in a physics
    ///    engine slowly gain/lose energy, so the speed is re-imposed every step).
    ///  - currentSpeed = baseSpeed * min(hitRamp^hits, cap) * product(modifiers)
    ///  - Anti-stall: |direction.x| is kept >= BallConfig.minimumHorizontalDirection.
    ///  - Raises BallHitWall; notifies every IBallHittable it touches.
    /// Paddle deflection is a separate component (PaddleHitResolver, [B3]).
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D))]
    [RequireComponent(typeof(CircleCollider2D))]
    [RequireComponent(typeof(BallModifierStack))]
    public class BallController : MonoBehaviour, IServeableBall
    {
        [SerializeField] private BallConfig config;

        private static readonly Dictionary<Collider2D, BallController> ByCollider =
            new Dictionary<Collider2D, BallController>();

        private readonly List<IBallHittable> hittables = new List<IBallHittable>(4);

        private Rigidbody2D rb;
        private CircleCollider2D circle;
        private BallModifierStack modifiers;
        private Vector2 spawnPosition;
        private Vector2 direction = Vector2.right;
        private bool frozen = true;
        private bool inPlay;
        private int hits;

        public BallConfig Config => config;
        public Rigidbody2D Body => rb;
        public BallModifierStack Modifiers => modifiers;
        public Vector2 Direction => direction;
        public bool IsFrozen => frozen;
        public bool InPlay => inPlay;
        public int Hits => hits;
        public Vector2 Position => rb != null ? rb.position : (Vector2)transform.position;

        /// <summary>World-space radius of the ball.</summary>
        public float Radius => circle != null
            ? circle.radius * Mathf.Max(transform.lossyScale.x, transform.lossyScale.y)
            : 0.2f;

        public float BaseSpeed => config != null ? config.speed : 8f;

        public float HitMultiplier
        {
            get
            {
                if (config == null)
                    return 1f;

                return Mathf.Min(Mathf.Pow(config.hitSpeedRamp, hits), config.maxSpeedMultiplier);
            }
        }

        public float CurrentSpeed => BaseSpeed * HitMultiplier * (modifiers != null ? modifiers.Product : 1f);

        private float MinHorizontal => config != null ? config.minimumHorizontalDirection : 0.25f;

        /// <summary>Find the ball that owns a collider without GetComponent (allocation-free).</summary>
        public static bool TryGet(Collider2D collider, out BallController ball)
        {
            ball = null;
            return collider != null && ByCollider.TryGetValue(collider, out ball) && ball != null;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => ByCollider.Clear();

        private void Awake()
        {
            rb = GetComponent<Rigidbody2D>();
            circle = GetComponent<CircleCollider2D>();
            modifiers = GetComponent<BallModifierStack>();

            rb.bodyType = RigidbodyType2D.Dynamic;
            rb.gravityScale = 0f;
            rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            rb.interpolation = RigidbodyInterpolation2D.Interpolate;
            rb.freezeRotation = true;
            rb.linearDamping = 0f;
            rb.angularDamping = 0f;

            spawnPosition = transform.position;
        }

        private void OnEnable()
        {
            if (circle != null)
                ByCollider[circle] = this;
        }

        private void OnDisable()
        {
            if (circle != null)
                ByCollider.Remove(circle);
        }

        public void Configure(BallConfig ballConfig)
        {
            config = ballConfig;
        }

        public void SetSpawnPosition(Vector2 position)
        {
            spawnPosition = position;
        }

        public void Serve(Vector2 serveDirection)
        {
            direction = ApplyAntiStall(serveDirection, MinHorizontal);
            frozen = false;
            inPlay = true;
            rb.linearVelocity = direction * CurrentSpeed;
        }

        public void ResetToCenter()
        {
            hits = 0;
            modifiers.Clear();
            frozen = true;
            inPlay = false;
            rb.linearVelocity = Vector2.zero;
            rb.position = spawnPosition;
            transform.position = spawnPosition;

            // A teleport would otherwise draw a streak across the arena.
            if (TryGetComponent(out TrailRenderer trail))
                trail.Clear();
        }

        public void Freeze()
        {
            frozen = true;
            rb.linearVelocity = Vector2.zero;
        }

        public void Unfreeze()
        {
            if (!frozen)
                return;

            frozen = false;
            rb.linearVelocity = direction * CurrentSpeed;
        }

        /// <summary>Change the travel direction (speed is re-imposed). Used by deflection and abilities.</summary>
        public void SetDirection(Vector2 newDirection)
        {
            direction = ApplyAntiStall(newDirection, MinHorizontal);

            if (!frozen)
                rb.linearVelocity = direction * CurrentSpeed;
        }

        /// <summary>Counts a paddle hit for the speed ramp.</summary>
        public void RegisterPaddleHit()
        {
            hits++;
        }

        /// <summary>Returns true exactly once per serve (guards goal zones against double-firing).</summary>
        public bool TryConsumeGoal()
        {
            if (!inPlay)
                return false;

            inPlay = false;
            return true;
        }

        private void FixedUpdate()
        {
            if (frozen)
            {
                rb.linearVelocity = Vector2.zero;
                return;
            }

            Vector2 v = rb.linearVelocity;

            if (v.sqrMagnitude > 1e-8f)
                direction = v.normalized;

            direction = ApplyAntiStall(direction, MinHorizontal);
            rb.linearVelocity = direction * CurrentSpeed;
        }

        private void OnCollisionEnter2D(Collision2D collision)
        {
            GameObject other = collision.gameObject;

            if (other.layer == PhysicsLayers.Wall)
                GameEvents.RaiseBallHitWall();

            if (collision.contactCount == 0)
                return;

            ContactPoint2D contact = collision.GetContact(0);
            var info = new BallHitInfo
            {
                Point = contact.point,
                Normal = contact.normal,
                Other = gameObject
            };

            hittables.Clear();
            other.GetComponents(hittables);

            for (int i = 0; i < hittables.Count; i++)
                hittables[i].OnBallHit(info);
        }

        /// <summary>
        /// Keeps |x| >= minX so the ball cannot bounce vertically forever. Preserves the signs.
        /// A zero vector becomes (1, 0).
        /// </summary>
        public static Vector2 ApplyAntiStall(Vector2 d, float minX)
        {
            if (d.sqrMagnitude < 1e-10f)
                return Vector2.right;

            d.Normalize();
            minX = Mathf.Clamp(minX, 0f, 0.99f);

            if (Mathf.Abs(d.x) >= minX)
                return d;

            float sx = d.x >= 0f ? 1f : -1f;
            float sy = d.y >= 0f ? 1f : -1f;
            return new Vector2(sx * minX, sy * Mathf.Sqrt(1f - minX * minX));
        }
    }
}
