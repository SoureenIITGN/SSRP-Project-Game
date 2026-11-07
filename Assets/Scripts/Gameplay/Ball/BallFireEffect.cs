using UnityEngine;
using SSRP.Core;

namespace SSRP.Gameplay
{
    /// <summary>
    /// "Ball on fire" after a successful parry: flickering flaming-orange ball, a long orange-to-red trail
    /// and a stream of embers. Purely visual - the speed boost is the BallModifierStack entry
    /// <see cref="ModifierKey"/> that ParryAbility adds, and the fire lasts exactly as long as that entry:
    ///  - the OTHER player's paddle touching the ball removes it (the parried shot was returned),
    ///  - ResetToCenter (goal) clears all modifiers,
    ///  - otherwise it expires after ParryConfig.fireMaxDuration.
    /// Added to the ball prefab by the builder, or at runtime by ParryAbility if missing.
    /// </summary>
    [RequireComponent(typeof(BallController))]
    public class BallFireEffect : MonoBehaviour
    {
        public const string ModifierKey = "parry.fire";

        [SerializeField] private Color fireColour = new Color(1f, 0.42f, 0.05f);
        [SerializeField] private Color hotColour = new Color(1f, 0.82f, 0.3f);
        [SerializeField, Min(0f)] private float flickerSpeed = 18f;
        [SerializeField, Min(0f)] private float trailTime = 0.35f;
        [SerializeField, Min(0f)] private float trailWidth = 0.5f;

        private BallController ball;
        private SpriteRenderer sprite;
        private TrailRenderer trail;
        private ParticleSystem embers;

        private Color baseColour = Color.white;
        private float baseTrailTime;
        private float baseStartWidth;
        private float baseEndWidth;
        private Gradient baseGradient;
        private Gradient fireGradient;

        private bool burning;
        private float ignitedAt;
        private int ownerId;

        public bool IsBurning => burning;
        public int OwnerId => ownerId;

        private void Awake()
        {
            ball = GetComponent<BallController>();
            sprite = GetComponent<SpriteRenderer>();
            trail = GetComponent<TrailRenderer>();

            if (sprite != null)
                baseColour = sprite.color;

            if (trail != null)
            {
                baseTrailTime = trail.time;
                baseStartWidth = trail.startWidth;
                baseEndWidth = trail.endWidth;
                baseGradient = trail.colorGradient;
            }

            fireGradient = new Gradient();
            fireGradient.SetKeys(
                new[]
                {
                    new GradientColorKey(new Color(1f, 0.9f, 0.45f), 0f),
                    new GradientColorKey(new Color(1f, 0.45f, 0.05f), 0.3f),
                    new GradientColorKey(new Color(0.8f, 0.1f, 0.02f), 1f)
                },
                new[]
                {
                    new GradientAlphaKey(1f, 0f),
                    new GradientAlphaKey(0.85f, 0.4f),
                    new GradientAlphaKey(0f, 1f)
                });

            embers = CreateEmbers();
        }

        /// <summary>Set the ball on fire (refreshes if already burning). <paramref name="byPlayerId"/> = who parried.</summary>
        public void Ignite(int byPlayerId)
        {
            ownerId = byPlayerId;
            ignitedAt = Time.time;

            if (burning)
                return;

            burning = true;

            if (trail != null)
            {
                trail.time = trailTime;
                trail.startWidth = trailWidth;
                trail.endWidth = 0.05f;
                trail.colorGradient = fireGradient;
            }

            if (embers != null)
                embers.Play();
        }

        public void Extinguish()
        {
            if (!burning)
                return;

            burning = false;

            if (sprite != null)
                sprite.color = baseColour;

            if (trail != null)
            {
                trail.time = baseTrailTime;
                trail.startWidth = baseStartWidth;
                trail.endWidth = baseEndWidth;
                trail.colorGradient = baseGradient;
            }

            if (embers != null)
                embers.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        }

        private void Update()
        {
            if (!burning)
                return;

            // The fire lives exactly as long as the speed modifier (goal reset / expiry / return remove it).
            if (!ball.Modifiers.Contains(ModifierKey))
            {
                Extinguish();
                return;
            }

            if (sprite != null)
            {
                float t = Mathf.PerlinNoise(Time.time * flickerSpeed, 0.37f);
                sprite.color = Color.Lerp(fireColour, hotColour, t);
            }
        }

        private void OnCollisionEnter2D(Collision2D collision)
        {
            // Ignore the very collision that lit the fire (same physics step).
            if (!burning || Time.time - ignitedAt < 0.05f)
                return;

            if (collision.gameObject.TryGetComponent(out IPaddleInfo paddle) && paddle.PlayerId != ownerId)
            {
                ball.Modifiers.Remove(ModifierKey);
                Extinguish();
            }
        }

        private ParticleSystem CreateEmbers()
        {
            var go = new GameObject("FireEmbers");
            go.transform.SetParent(transform, false);
            go.SetActive(false);

            ParticleSystem ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            ParticleSystem.MainModule main = ps.main;
            main.playOnAwake = false;
            main.loop = true;
            main.duration = 1f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.25f, 0.5f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.2f, 1f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.15f, 0.35f);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.85f, 0.3f), new Color(1f, 0.35f, 0.05f));
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.scalingMode = ParticleSystemScalingMode.Shape;   // sizes in world units despite the ball's scale
            main.maxParticles = 300;
            main.gravityModifier = 0f;

            ParticleSystem.EmissionModule emission = ps.emission;
            emission.rateOverTime = 30f;
            emission.rateOverDistance = 18f;

            ParticleSystem.ShapeModule shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = 0.3f;

            ParticleSystem.ColorOverLifetimeModule col = ps.colorOverLifetime;
            col.enabled = true;
            col.color = new ParticleSystem.MinMaxGradient(fireGradient);

            ParticleSystem.SizeOverLifetimeModule size = ps.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0.1f));

            ParticleSystemRenderer renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.sortingOrder = 8;

            Material material = trail != null ? trail.sharedMaterial : null;
            if (material == null && sprite != null)
                material = sprite.sharedMaterial;
            if (material != null)
                renderer.sharedMaterial = material;

            go.SetActive(true);
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            return ps;
        }
    }
}
