using UnityEngine;
using SSRP.Audio;
using SSRP.Core;
using SSRP.Flow;
using SSRP.Gameplay;
using SSRP.Input;
using SSRP.UI;

namespace SSRP.EditorTools
{
    /// <summary>Paddle, ball, services, UI and particle prefabs.</summary>
    internal static class PrefabFactory
    {
        public static void CreateAll(BuildContext c)
        {
            c.PaddlePrefab = BuildPaddle(c);
            c.BallPrefab = BuildBall(c);
            c.ServicesPrefab = BuildServices(c);
            c.HudPrefab = BuildUiRoot<PlayerHud>("PlayerHud", BuildUtil.PrefabsUi);
            c.FlowUiPrefab = BuildUiRoot<FlowUi>("FlowUi", BuildUtil.PrefabsUi);
            c.SerialPanelPrefab = BuildUiRoot<SerialConnectionPanel>("SerialConnectionPanel", BuildUtil.PrefabsUi);
            c.CalibrationPanelPrefab = BuildUiRoot<CalibrationPanel>("CalibrationPanel", BuildUtil.PrefabsUi);
            c.TileBurstPrefab = BuildTileBurst(c);
            c.Log("Prefabs: Paddle, Ball, SSRP_Services, PlayerHud, FlowUi, SerialConnectionPanel, CalibrationPanel, TileBurst.");
        }

        private static PaddleController BuildPaddle(BuildContext c)
        {
            var go = new GameObject("Paddle");
            BuildUtil.AssignLayer(go, PhysicsLayers.PaddleName);
            SpriteRenderer body = BuildUtil.AddSprite(go, c.Square, c.SpriteMaterial, Color.white, 5);

            Rigidbody2D rb = go.AddComponent<Rigidbody2D>();
            rb.bodyType = RigidbodyType2D.Kinematic;
            rb.interpolation = RigidbodyInterpolation2D.Interpolate;
            rb.gravityScale = 0f;
            rb.freezeRotation = true;

            BoxCollider2D box = go.AddComponent<BoxCollider2D>();
            box.size = Vector2.one;
            box.sharedMaterial = c.Bouncy;

            PlayerSlot slot = go.AddComponent<PlayerSlot>();
            PlayerInputPipeline pipeline = go.AddComponent<PlayerInputPipeline>();
            BuildUtil.Set(pipeline, "slot", slot);
            BuildUtil.Set(pipeline, "paddleConfig", c.PaddleConfig);

            PaddleController paddle = go.AddComponent<PaddleController>();
            BuildUtil.Set(paddle, "config", c.PaddleConfig);
            BuildUtil.Set(paddle, "pipeline", pipeline);
            BuildUtil.Set(paddle, "slot", slot);
            BuildUtil.Set(paddle, "body", body);

            ParryAbility parry = go.AddComponent<ParryAbility>();
            BuildUtil.Set(parry, "config", c.ParryConfig);
            BuildUtil.Set(parry, "flashRenderer", body);

            go.transform.localScale = new Vector3(c.PaddleConfig.width, c.PaddleConfig.height, 1f);

            return BuildUtil.SavePrefab(go, $"{BuildUtil.PrefabsGameplay}/Paddle.prefab").GetComponent<PaddleController>();
        }

        private static BallController BuildBall(BuildContext c)
        {
            var go = new GameObject("Ball");
            BuildUtil.AssignLayer(go, PhysicsLayers.BallName);
            go.transform.localScale = Vector3.one * 0.35f;
            BuildUtil.AddSprite(go, c.Circle, c.SpriteMaterial, Color.white, 10);

            Rigidbody2D rb = go.AddComponent<Rigidbody2D>();
            rb.bodyType = RigidbodyType2D.Dynamic;
            rb.gravityScale = 0f;
            rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            rb.interpolation = RigidbodyInterpolation2D.Interpolate;
            rb.freezeRotation = true;

            CircleCollider2D circle = go.AddComponent<CircleCollider2D>();
            circle.radius = 0.5f;
            circle.sharedMaterial = c.Bouncy;

            go.AddComponent<BallModifierStack>();
            BallController ball = go.AddComponent<BallController>();
            BuildUtil.Set(ball, "config", c.BallNormal);
            go.AddComponent<PaddleHitResolver>();

            TrailRenderer trail = go.AddComponent<TrailRenderer>();
            go.AddComponent<BallFireEffect>();   // (Awake reads the trail at runtime, order does not matter)
            trail.sharedMaterial = c.ParticleMaterial;
            trail.time = 0.12f;
            trail.startWidth = 0.3f;
            trail.endWidth = 0f;
            trail.startColor = new Color(1f, 1f, 1f, 0.5f);
            trail.endColor = new Color(1f, 1f, 1f, 0f);
            trail.sortingOrder = 9;

            return BuildUtil.SavePrefab(go, $"{BuildUtil.PrefabsGameplay}/Ball.prefab").GetComponent<BallController>();
        }

        private static GameObject BuildServices(BuildContext c)
        {
            var root = new GameObject("SSRP_Services");
            root.AddComponent<GameServices>();
            root.AddComponent<SceneLoader>();

            GameObject input = BuildUtil.Child(root, "Input", Vector3.zero);
            KeyboardInputSource keyboard = input.AddComponent<KeyboardInputSource>();
            ScriptedInputSource scripted = input.AddComponent<ScriptedInputSource>();
            SerialInputSource serial = input.AddComponent<SerialInputSource>();
            BuildUtil.Set(serial, "config", c.SerialConfig);

            SerialConnectionManager manager = input.AddComponent<SerialConnectionManager>();
            BuildUtil.Set(manager, "source", serial);
            BuildUtil.Set(manager, "config", c.SerialConfig);

            UdpInputSource udp = input.AddComponent<UdpInputSource>();

            NoiseInjector noise = input.AddComponent<NoiseInjector>();
            BuildUtil.Set(noise, "sourceComponent", keyboard);

            InputSourceRegistry registry = input.AddComponent<InputSourceRegistry>();
            BuildUtil.Set(registry, "keyboard", keyboard);
            BuildUtil.Set(registry, "scripted", scripted);
            BuildUtil.Set(registry, "serial", serial);
            BuildUtil.Set(registry, "noiseInjected", noise);
            BuildUtil.Set(registry, "udp", udp);

            GameObject audio = BuildUtil.Child(root, "Audio", Vector3.zero);
            AudioManager audioManager = audio.AddComponent<AudioManager>();
            BuildUtil.Set(audioManager, "config", c.AudioConfig);

            return BuildUtil.SavePrefab(root, $"{BuildUtil.PrefabsCore}/SSRP_Services.prefab");
        }

        private static GameObject BuildUiRoot<T>(string name, string folder) where T : Component
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = LayerMask.NameToLayer("UI") >= 0 ? LayerMask.NameToLayer("UI") : 0;
            go.AddComponent<T>();
            return BuildUtil.SavePrefab(go, $"{folder}/{name}.prefab");
        }

        private static ParticleSystem BuildTileBurst(BuildContext c)
        {
            var go = new GameObject("TileBurst");
            ParticleSystem ps = go.AddComponent<ParticleSystem>();

            ParticleSystem.MainModule main = ps.main;
            main.duration = 0.5f;
            main.loop = false;
            main.playOnAwake = false;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.35f, 0.7f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(2f, 6f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.08f, 0.2f);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.6f, 0.25f), new Color(1f, 0.9f, 0.5f));
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.gravityModifier = 0f;

            ParticleSystem.EmissionModule emission = ps.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)28) });

            ParticleSystem.ShapeModule shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = 0.2f;

            ParticleSystem.ColorOverLifetimeModule col = ps.colorOverLifetime;
            col.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) });
            col.color = new ParticleSystem.MinMaxGradient(gradient);

            ParticleSystemRenderer renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = c.ParticleMaterial;
            renderer.sortingOrder = 20;

            return BuildUtil.SavePrefab(go, $"{BuildUtil.PrefabsAudio}/TileBurst.prefab").GetComponent<ParticleSystem>();
        }
    }
}
