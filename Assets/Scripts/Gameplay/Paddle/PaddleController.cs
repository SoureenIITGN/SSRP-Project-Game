using UnityEngine;
using SSRP.Core;
using SSRP.Input;

namespace SSRP.Gameplay
{
    /// <summary>
    /// [C3] Velocity-controlled paddle. The player's pressure differential sets the SPEED,
    /// not the position (a first-order integrator), so filter delay shows up as overshoot.
    ///
    ///   velocity = maxSpeed * state.Velocity     (optionally smoothed by PaddleConfig.acceleration)
    ///
    /// Kinematic Rigidbody2D moved with MovePosition in FixedUpdate, clamped inside ArenaBounds.
    /// Abilities (parry, dash...) are separate IPaddleAbility components ticked every frame,
    /// so they never require edits to this class. No references to Ball or Arena classes.
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D))]
    [RequireComponent(typeof(BoxCollider2D))]
    public class PaddleController : MonoBehaviour, IPaddleInfo
    {
        [SerializeField] private PaddleConfig config;
        [SerializeField] private PlayerInputPipeline pipeline;
        [SerializeField] private PlayerSlot slot;
        [SerializeField, Tooltip("Renderer tinted with the player's colour (abilities may flash it).")]
        private SpriteRenderer body;

        private Rigidbody2D rb;
        private IPaddleAbility[] abilities = new IPaddleAbility[0];
        private ArenaBounds bounds;
        private bool hasBounds;
        private float currentSpeed;
        private Vector2 velocity;

        public int PlayerId => slot != null ? slot.PlayerId : 0;

        public float Height => config != null ? config.height : transform.lossyScale.y;

        public Vector2 Velocity => velocity;

        public PaddleConfig Config => config;
        public PlayerInputPipeline Pipeline => pipeline;
        public PlayerSlot Slot => slot;
        public SpriteRenderer Body => body;

        /// <summary>Colour the paddle returns to after an ability flash.</summary>
        public Color BaseColour { get; private set; } = Color.white;

        public PlayerInputState InputState => pipeline != null ? pipeline.State : default;

        private void Awake()
        {
            rb = GetComponent<Rigidbody2D>();
            rb.bodyType = RigidbodyType2D.Kinematic;
            rb.interpolation = RigidbodyInterpolation2D.Interpolate;
            rb.gravityScale = 0f;
            rb.freezeRotation = true;

            if (slot == null) slot = GetComponent<PlayerSlot>();
            if (pipeline == null) pipeline = GetComponent<PlayerInputPipeline>();
            if (body == null) body = GetComponentInChildren<SpriteRenderer>();

            if (body != null)
                BaseColour = body.color;

            RefreshAbilities();
            ApplyConfig();
        }

        /// <summary>Collect IPaddleAbility components (done once; call again if you add one at runtime).</summary>
        public void RefreshAbilities()
        {
            abilities = GetComponents<IPaddleAbility>();
        }

        public void Configure(PaddleConfig paddleConfig)
        {
            config = paddleConfig;

            if (pipeline != null)
                pipeline.Configure(slot, paddleConfig);

            ApplyConfig();
        }

        public void SetBounds(ArenaBounds arenaBounds)
        {
            bounds = arenaBounds;
            hasBounds = true;
        }

        public void SetColour(Color colour)
        {
            BaseColour = colour;

            if (body != null)
                body.color = colour;
        }

        /// <summary>Teleport (used when spawning or resetting).</summary>
        public void SetPosition(Vector2 position)
        {
            transform.position = position;

            if (rb != null)
                rb.position = position;

            currentSpeed = 0f;
            velocity = Vector2.zero;
        }

        private void ApplyConfig()
        {
            if (config == null)
                return;

            // The paddle sprite and collider are 1x1 units; scale to the configured size.
            transform.localScale = new Vector3(config.width, config.height, 1f);
        }

        private void Update()
        {
            PlayerInputState state = InputState;
            float dt = Time.deltaTime;

            for (int i = 0; i < abilities.Length; i++)
                abilities[i].Tick(state, dt);
        }

        private void FixedUpdate()
        {
            float dt = Time.fixedDeltaTime;
            float maxSpeed = config != null ? config.maxSpeed : 8f;
            float target = maxSpeed * InputState.Velocity;

            if (config != null && config.acceleration > 0f)
                currentSpeed = Mathf.MoveTowards(currentSpeed, target, config.acceleration * dt);
            else
                currentSpeed = target;

            Vector2 position = rb.position;
            float y = position.y + currentSpeed * dt;

            if (hasBounds)
            {
                float half = Height * 0.5f;
                float minY = bounds.Min.y + half;
                float maxY = bounds.Max.y - half;
                y = minY <= maxY ? Mathf.Clamp(y, minY, maxY) : (bounds.Min.y + bounds.Max.y) * 0.5f;

                // Hitting the clamp means the paddle is not really moving.
                if ((y <= minY && currentSpeed < 0f) || (y >= maxY && currentSpeed > 0f))
                    currentSpeed = 0f;
            }

            velocity = new Vector2(0f, (y - position.y) / dt);
            rb.MovePosition(new Vector2(position.x, y));
        }
    }
}
