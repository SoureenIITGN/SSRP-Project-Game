using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using SSRP.Arena;
using SSRP.Core;
using SSRP.Flow;
using SSRP.Gameplay;
using SSRP.Input;

namespace SSRP.Tests
{
    public class SerialParserTests
    {
        [Test]
        public void ParsesValidLine()
        {
            Assert.That(SerialLineParser.TryParse("1234567,512,3,0,1023\r\n", out FsrFrame f), Is.EqualTo(ParseResult.Ok));
            Assert.That(f.TimestampMicros, Is.EqualTo(1234567u));
            Assert.That(f.Ch0, Is.EqualTo(512f));
            Assert.That(f.Ch1, Is.EqualTo(3f));
            Assert.That(f.Ch2, Is.EqualTo(0f));
            Assert.That(f.Ch3, Is.EqualTo(1023f));
        }

        [TestCase("# SSRP-FSR v1 rate=200Hz", ParseResult.Comment)]
        [TestCase("", ParseResult.Empty)]
        [TestCase("   ", ParseResult.Empty)]
        [TestCase("12,34,56", ParseResult.Malformed)]          // old 3-field format / partial line
        [TestCase("1,2,3,4,5,6", ParseResult.Malformed)]
        [TestCase("1,2,3,4,1024", ParseResult.Malformed)]      // out of ADC range
        [TestCase("1,2,-3,4,5", ParseResult.Malformed)]
        [TestCase("1,2,x,4,5", ParseResult.Malformed)]
        [TestCase("45,512,3", ParseResult.Malformed)]
        public void ClassifiesOtherLines(string line, ParseResult expected)
        {
            Assert.That(SerialLineParser.TryParse(line, out _), Is.EqualTo(expected));
        }
    }

    public class CalibrationTests
    {
        [Test]
        public void MedianAndPercentile_IgnoreSpikes()
        {
            var rest = new List<float> { 20, 21, 19, 22, 20, 900, 21 };   // one spike
            Assert.That(CalibrationMath.Median(rest), Is.EqualTo(21f));

            var press = new List<float>();
            for (int i = 0; i < 100; i++) press.Add(800 + i);              // 800..899
            press.Add(1023);                                               // spike
            Assert.That(CalibrationMath.Percentile(press, 95f), Is.InRange(890f, 900f));
        }

        [Test]
        public void Validate_RejectsSmallSpan()
        {
            CalibrationData data = CalibrationData.CreateDefault();
            data.channels[1] = new ChannelCalibration { baseline = 100, max = 140 };
            Assert.That(CalibrationMath.Validate(data, 150f, 30f, out string message), Is.False);
            StringAssert.Contains("P1 Down", message);
        }

        [Test]
        public void Routine_ProducesBaselineAndMax()
        {
            var routine = new CalibrationRoutine(false, CalibrationData.CreateDefault());
            var ch = new float[4];
            const float dt = 0.02f;

            int guard = 0;
            while (!routine.IsDone && guard++ < 10000)
            {
                for (int i = 0; i < 4; i++)
                    ch[i] = routine.CurrentChannel == i ? 900f : 30f;

                routine.Tick(dt, ch, true);
            }

            Assert.That(routine.Succeeded, Is.True, routine.Message);
            for (int i = 0; i < 4; i++)
            {
                Assert.That(routine.Result.channels[i].baseline, Is.EqualTo(30f).Within(0.5f));
                Assert.That(routine.Result.channels[i].max, Is.EqualTo(900f).Within(0.5f));
            }
        }

        [Test]
        public void Routine_RejectsWhenNoData()
        {
            var routine = new CalibrationRoutine(true, CalibrationData.CreateDefault());
            for (int i = 0; i < 200 && !routine.IsDone; i++)
                routine.Tick(0.02f, null, false);

            Assert.That(routine.IsDone, Is.True);
            Assert.That(routine.Succeeded, Is.False);
        }
    }

    public class DeflectionTests
    {
        [Test]
        public void EdgeHitsAreSteeperThanCentreHits()
        {
            float previous = -1f;

            foreach (float offset in new[] { 0f, 0.25f, 0.5f, 0.75f, 1f })
            {
                Vector2 d = PaddleHitResolver.ComputeBounceDirection(offset, 60f, 1f);
                float angle = Mathf.Abs(Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg);
                Assert.That(angle, Is.GreaterThan(previous));
                previous = angle;
            }

            Assert.That(previous, Is.EqualTo(60f).Within(0.01f));
        }

        [Test]
        public void DirectionIsMirroredBySide()
        {
            Vector2 right = PaddleHitResolver.ComputeBounceDirection(0.3f, 60f, 1f);
            Vector2 left = PaddleHitResolver.ComputeBounceDirection(0.3f, 60f, -1f);
            Assert.That(right.x, Is.GreaterThan(0f));
            Assert.That(left.x, Is.LessThan(0f));
            Assert.That(left.y, Is.EqualTo(right.y).Within(1e-5f));
        }

        [Test]
        public void OffsetIsClamped()
        {
            Assert.That(PaddleHitResolver.ComputeOffset(10f, 0f, 2f), Is.EqualTo(1f));
            Assert.That(PaddleHitResolver.ComputeOffset(-10f, 0f, 2f), Is.EqualTo(-1f));
            Assert.That(PaddleHitResolver.ComputeOffset(0.5f, 0f, 2f), Is.EqualTo(0.5f));
        }

        [Test]
        public void AntiStall_KeepsMinimumHorizontalComponent()
        {
            Vector2 d = BallController.ApplyAntiStall(new Vector2(0f, 1f), 0.25f);
            Assert.That(Mathf.Abs(d.x), Is.GreaterThanOrEqualTo(0.25f - 1e-5f));
            Assert.That(d.magnitude, Is.EqualTo(1f).Within(1e-5f));
            Assert.That(d.y, Is.GreaterThan(0f));
        }

        [Test]
        public void Serve_IsTowardSideWithinRange()
        {
            for (int i = 0; i <= 10; i++)
            {
                Vector2 d = ServeMath.DirectionToward(PaddleSide.Left, 25f, i / 10f);
                Assert.That(d.x, Is.LessThan(0f));
                float angle = Mathf.Abs(Mathf.Atan2(d.y, -d.x) * Mathf.Rad2Deg);
                Assert.That(angle, Is.LessThanOrEqualTo(25.001f));
            }
        }
    }

    public class FlowTests
    {
        [Test]
        public void MatchState_FirstToTargetWins()
        {
            var state = new MatchState();
            state.Reset(new[] { 1, 2 });

            for (int i = 0; i < 4; i++) state.AddPoint(1);
            for (int i = 0; i < 4; i++) state.AddPoint(2);
            Assert.That(state.HasWinner(5, out _), Is.False);

            state.AddPoint(2);
            Assert.That(state.HasWinner(5, out int winner), Is.True);
            Assert.That(winner, Is.EqualTo(2));
            Assert.That(state.GetScore(1), Is.EqualTo(4));
        }

        [Test]
        public void RuntimeCopy_DoesNotTouchAsset()
        {
            var asset = ScriptableObject.CreateInstance<MatchConfig>();
            asset.targetScore = 5;
            MatchConfig copy = asset.CreateRuntimeCopy();
            copy.targetScore = 11;
            copy.players[0].inputSource = InputSourceType.Serial;

            Assert.That(asset.targetScore, Is.EqualTo(5));
            Assert.That(asset.players[0].inputSource, Is.EqualTo(InputSourceType.Keyboard));

            Object.DestroyImmediate(copy);
            Object.DestroyImmediate(asset);
        }

        [Test]
        public void CameraFit_CoversBoundsForAnyAspect()
        {
            var bounds = new ArenaBounds(new Vector2(-9, -5), new Vector2(9, 5));

            foreach (float aspect in new[] { 16f / 9f, 16f / 10f, 4f / 3f, 21f / 9f })
            {
                float size = ArenaCameraFitter.ComputeOrthographicSize(bounds, 1f, aspect);
                Assert.That(size, Is.GreaterThanOrEqualTo(6f));               // half height + margin
                Assert.That(size * aspect, Is.GreaterThanOrEqualTo(10f - 1e-4f)); // half width + margin
            }
        }
    }
}
