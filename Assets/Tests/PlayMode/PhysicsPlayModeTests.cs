using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.Tilemaps;
using SSRP.Core;
using SSRP.Gameplay;
using SSRP.Input;

namespace SSRP.Tests
{
    /// <summary>
    /// [B5] PlayMode tests for ball, goals, tiles, paddle and parry. Every test builds its own objects,
    /// so no scene or generated asset is needed.
    /// </summary>
    public class PhysicsPlayModeTests
    {
        private readonly List<Object> created = new List<Object>();
        private PhysicsMaterial2D bouncy;

        [SetUp]
        public void SetUp()
        {
            Time.timeScale = 1f;
            bouncy = new PhysicsMaterial2D("TestBouncy") { bounciness = 1f, friction = 0f };
            created.Add(bouncy);
        }

        [TearDown]
        public void TearDown()
        {
            foreach (Object o in created)
            {
                if (o != null)
                    Object.Destroy(o);
            }

            created.Clear();
        }

        private T Track<T>(T o) where T : Object
        {
            created.Add(o);
            return o;
        }

        private BallConfig MakeConfig(float speed = 8f, float ramp = 1f)
        {
            BallConfig c = Track(ScriptableObject.CreateInstance<BallConfig>());
            c.speed = speed;
            c.hitSpeedRamp = ramp;
            c.maxSpeedMultiplier = 2f;
            c.minimumHorizontalDirection = 0.25f;
            return c;
        }

        private BallController MakeBall(BallConfig config, Vector2 position)
        {
            var go = Track(new GameObject("TestBall"));
            go.transform.position = position;
            go.transform.localScale = Vector3.one * 0.35f;
            go.AddComponent<Rigidbody2D>();
            var circle = go.AddComponent<CircleCollider2D>();
            circle.radius = 0.5f;
            circle.sharedMaterial = bouncy;
            go.AddComponent<BallModifierStack>();
            BallController ball = go.AddComponent<BallController>();
            ball.Configure(config);
            ball.SetSpawnPosition(position);
            return ball;
        }

        private void MakeWall(Vector2 centre, Vector2 size)
        {
            var go = Track(new GameObject("TestWall"));
            go.transform.position = centre;
            var box = go.AddComponent<BoxCollider2D>();
            box.size = size;
            box.sharedMaterial = bouncy;
        }

        private void MakeBox(float halfWidth, float halfHeight)
        {
            MakeWall(new Vector2(0, halfHeight + 0.5f), new Vector2(halfWidth * 2 + 2, 1));
            MakeWall(new Vector2(0, -halfHeight - 0.5f), new Vector2(halfWidth * 2 + 2, 1));
            MakeWall(new Vector2(halfWidth + 0.5f, 0), new Vector2(1, halfHeight * 2 + 2));
            MakeWall(new Vector2(-halfWidth - 0.5f, 0), new Vector2(1, halfHeight * 2 + 2));
        }

        [UnityTest]
        public IEnumerator Ball_SpeedStaysWithinOnePercent()
        {
            MakeBox(6f, 4f);
            BallController ball = MakeBall(MakeConfig(10f), Vector2.zero);
            yield return new WaitForFixedUpdate();
            ball.Serve(new Vector2(1f, 0.7f));

            float worst = 0f;
            for (int i = 0; i < 500; i++)
            {
                yield return new WaitForFixedUpdate();
                float speed = ball.Body.linearVelocity.magnitude;
                worst = Mathf.Max(worst, Mathf.Abs(speed - ball.CurrentSpeed) / ball.CurrentSpeed);
            }

            Assert.That(worst, Is.LessThanOrEqualTo(0.01f), $"worst speed error {worst:P3}");
            Assert.That(ball.Position.magnitude, Is.LessThan(8f), "ball escaped the box");
        }

        [UnityTest]
        public IEnumerator Ball_NeverStuckOnVerticalAxis()
        {
            MakeBox(6f, 4f);
            BallController ball = MakeBall(MakeConfig(), Vector2.zero);
            yield return new WaitForFixedUpdate();
            ball.Serve(Vector2.up);   // worst case: straight up

            float startX = ball.Position.x;
            for (int i = 0; i < 100; i++)
            {
                yield return new WaitForFixedUpdate();
                Assert.That(Mathf.Abs(ball.Direction.x), Is.GreaterThanOrEqualTo(0.25f - 1e-4f));
            }

            Assert.That(Mathf.Abs(ball.Position.x - startX), Is.GreaterThan(0.5f));
        }

        [UnityTest]
        public IEnumerator Goal_FiresExactlyOnce()
        {
            int goals = 0;
            int scorer = -1;
            System.Action<int> handler = id => { goals++; scorer = id; };
            GameEvents.GoalScored += handler;

            try
            {
                var goalGo = Track(new GameObject("TestGoal"));
                goalGo.transform.position = new Vector2(3f, 0f);
                var box = goalGo.AddComponent<BoxCollider2D>();
                box.size = new Vector2(1f, 10f);
                box.isTrigger = true;
                GoalZone goal = goalGo.AddComponent<GoalZone>();
                goal.ScoringPlayerId = 7;

                BallController ball = MakeBall(MakeConfig(10f), Vector2.zero);
                yield return new WaitForFixedUpdate();
                ball.Serve(Vector2.right);

                for (int i = 0; i < 150; i++)
                    yield return new WaitForFixedUpdate();

                Assert.That(goals, Is.EqualTo(1));
                Assert.That(scorer, Is.EqualTo(7));
            }
            finally
            {
                GameEvents.GoalScored -= handler;
            }
        }

        [UnityTest]
        public IEnumerator BreakableTile_RemovedAfterHitPoints()
        {
            var gridGo = Track(new GameObject("TestGrid"));
            gridGo.AddComponent<Grid>();
            var mapGo = new GameObject("Interactive");
            mapGo.transform.SetParent(gridGo.transform, false);
            Tilemap map = mapGo.AddComponent<Tilemap>();
            mapGo.AddComponent<TilemapRenderer>();
            TileHitResolver resolver = mapGo.AddComponent<TileHitResolver>();

            BreakableBehaviour breakable = Track(ScriptableObject.CreateInstance<BreakableBehaviour>());
            GameplayTile tile = Track(ScriptableObject.CreateInstance<GameplayTile>());
            tile.behaviour = breakable;
            tile.hitPoints = 3;

            var cell = new Vector3Int(2, 0, 0);
            map.SetTile(cell, tile);
            BallController ball = MakeBall(MakeConfig(), new Vector2(-3f, 0f));
            yield return null;

            int destroyed = 0;
            System.Action<Vector2> onDestroyed = _ => destroyed++;
            GameEvents.TileDestroyed += onDestroyed;

            try
            {
                Vector2 point = map.GetCellCenterWorld(cell) + Vector3.left * 0.45f;

                for (int hit = 1; hit <= 3; hit++)
                {
                    Assert.That(map.GetTile(cell), Is.Not.Null, $"tile gone before hit {hit}");
                    Assert.That(resolver.ProcessHit(ball, point, Vector2.right, Vector2.left), Is.True);
                }

                Assert.That(map.GetTile(cell), Is.Null);
                Assert.That(destroyed, Is.EqualTo(1));
            }
            finally
            {
                GameEvents.TileDestroyed -= onDestroyed;
            }
        }

        [UnityTest]
        public IEnumerator BouncePad_AddsTemporaryModifier()
        {
            BallController ball = MakeBall(MakeConfig(8f), Vector2.zero);
            yield return null;

            float before = ball.CurrentSpeed;
            ball.Modifiers.Add(BouncePadBehaviour.ModifierKey, new SpeedMultiplierModifier(1.5f), 0.2f);
            Assert.That(ball.CurrentSpeed, Is.EqualTo(before * 1.5f).Within(1e-3f));

            yield return new WaitForSeconds(0.35f);
            Assert.That(ball.CurrentSpeed, Is.EqualTo(before).Within(1e-3f));
        }

        [UnityTest]
        public IEnumerator Paddle_MovesWithScriptedInput()
        {
            var registryGo = Track(new GameObject("TestRegistry"));
            ScriptedInputSource scripted = registryGo.AddComponent<ScriptedInputSource>();
            scripted.Pattern = ScriptedPattern.Constant;
            scripted.SetConstant(1023f, 0f);
            InputSourceRegistry registry = registryGo.AddComponent<InputSourceRegistry>();
            registry.Register(InputSourceType.Scripted, scripted);

            var paddleGo = Track(new GameObject("TestPaddle"));
            PlayerSlot slot = paddleGo.AddComponent<PlayerSlot>();
            slot.SetInputSource(InputSourceType.Scripted);
            paddleGo.AddComponent<PlayerInputPipeline>();
            PaddleController paddle = paddleGo.AddComponent<PaddleController>();
            paddle.SetBounds(new ArenaBounds(new Vector2(-9, -5), new Vector2(9, 5)));

            float startY = paddle.transform.position.y;
            yield return new WaitForSeconds(0.3f);

            Assert.That(paddle.transform.position.y, Is.GreaterThan(startY + 0.5f));
            Assert.That(paddle.Velocity.y, Is.GreaterThan(0f));

            // Clamped at the top bound (half height defaults to the transform scale).
            yield return new WaitForSeconds(2f);
            Assert.That(paddle.transform.position.y, Is.LessThanOrEqualTo(5f));
        }

        private ParryAbility MakeParry()
        {
            ParryConfig config = Track(ScriptableObject.CreateInstance<ParryConfig>());
            var go = Track(new GameObject("TestParry"));
            go.AddComponent<SpriteRenderer>();
            ParryAbility parry = go.AddComponent<ParryAbility>();
            parry.SetConfig(config);
            return parry;
        }

        [UnityTest]
        public IEnumerator Parry_TriggersOnBalancedFirmPress_NotOnSwitch()
        {
            ParryAbility parry = MakeParry();
            yield return null;

            // Switching Up -> Down (keyboard ramp) crosses at ~0.5/0.5, below the 0.55 threshold: no parry.
            parry.Tick(new PlayerInputState { Up = 1f, Down = 0f }, 0.02f);
            parry.Tick(new PlayerInputState { Up = 0.5f, Down = 0.5f }, 0.02f);
            parry.Tick(new PlayerInputState { Up = 0f, Down = 1f }, 0.02f);
            Assert.That(parry.TriggerCount, Is.EqualTo(0));

            // Both firmly: fires on the first frame (hold 0.02 s), holding does not repeat.
            for (int i = 0; i < 60; i++)
                parry.Tick(new PlayerInputState { Up = 1f, Down = 1f }, 0.02f);

            Assert.That(parry.TriggerCount, Is.EqualTo(1));

            // Release, wait out the cooldown, press again: second parry.
            for (int i = 0; i < 5; i++)
                parry.Tick(new PlayerInputState { Up = 0f, Down = 0f }, 0.02f);
            parry.Tick(new PlayerInputState { Up = 1f, Down = 0.8f }, 0.02f);
            Assert.That(parry.TriggerCount, Is.EqualTo(2));
        }

        [UnityTest]
        public IEnumerator Parry_SuccessSetsBallOnFire()
        {
            ParryAbility parry = MakeParry();
            BallController ball = MakeBall(MakeConfig(8f), Vector2.zero);
            yield return null;

            int successes = 0;
            System.Action<int> handler = _ => successes++;
            GameEvents.ParrySucceeded += handler;

            try
            {
                // Ball touching a paddle that did not parry: nothing.
                parry.OnBallHit(new BallHitInfo { Other = ball.gameObject });
                Assert.That(parry.SuccessCount, Is.EqualTo(0));
                yield return new WaitForSeconds(0.2f);   // leave the late-parry grace window

                parry.Tick(new PlayerInputState { Up = 1f, Down = 1f }, 0.02f);   // parry
                parry.OnBallHit(new BallHitInfo { Other = ball.gameObject });     // ball arrives in the window

                Assert.That(parry.SuccessCount, Is.EqualTo(1));
                Assert.That(successes, Is.EqualTo(1));
                Assert.That(ball.Modifiers.Contains(BallFireEffect.ModifierKey), Is.True);
                Assert.That(ball.GetComponent<BallFireEffect>().IsBurning, Is.True);
                Assert.That(ball.CurrentSpeed, Is.EqualTo(8f * 1.5f).Within(1e-3f));

                // Goal reset clears the modifier, which puts the fire out.
                ball.ResetToCenter();
                yield return null;
                Assert.That(ball.GetComponent<BallFireEffect>().IsBurning, Is.False);
            }
            finally
            {
                GameEvents.ParrySucceeded -= handler;
            }
        }
    }
}
