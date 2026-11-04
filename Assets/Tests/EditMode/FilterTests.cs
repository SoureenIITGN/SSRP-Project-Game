using NUnit.Framework;
using SSRP.Input;

namespace SSRP.Tests
{
    public class FilterTests
    {
        [Test]
        public void ExponentialFilter_UnitStep_Reaches63PercentAfterOneTimeConstant()
        {
            const float tau = 0.1f;
            const float dt = 0.001f;
            var filter = new ExponentialFilter(tau);

            float y = 0f;
            int steps = UnityEngine.Mathf.RoundToInt(tau / dt);

            for (int i = 0; i < steps; i++)
                y = filter.Process(1f, dt);

            Assert.That(y, Is.EqualTo(0.632f).Within(0.01f));
        }

        [Test]
        public void ExponentialFilter_ResultDoesNotDependOnFrameRate()
        {
            var fast = new ExponentialFilter(0.05f);
            var slow = new ExponentialFilter(0.05f);
            float a = 0f, b = 0f;

            for (int i = 0; i < 100; i++) a = fast.Process(1f, 0.001f);
            for (int i = 0; i < 10; i++) b = slow.Process(1f, 0.01f);

            Assert.That(a, Is.EqualTo(b).Within(1e-3f));
        }

        [Test]
        public void ExponentialFilter_Reset_ReturnsToZero()
        {
            var filter = new ExponentialFilter(0.05f);
            filter.Process(1f, 0.1f);
            filter.Reset();
            Assert.That(filter.Process(0f, 0.01f), Is.EqualTo(0f));
        }

        [Test]
        public void PassThrough_ReturnsInput()
        {
            var filter = new PassThroughFilter();
            Assert.That(filter.Process(0.37f, 0.02f), Is.EqualTo(0.37f));
        }

        [Test]
        public void Factory_CreatesSelectedType()
        {
            Assert.That(FilterFactory.Create(new FilterStage { type = FilterType.Exponential }),
                Is.TypeOf<ExponentialFilter>());
            Assert.That(FilterFactory.Create(new FilterStage { type = FilterType.PassThrough }),
                Is.TypeOf<PassThroughFilter>());
        }

        [Test]
        public void Deadzone_ZeroesSmallValues_AndKeepsFullScale()
        {
            Assert.That(PlayerInputPipeline.ApplyDeadzone(0.04f, 0.05f), Is.EqualTo(0f));
            Assert.That(PlayerInputPipeline.ApplyDeadzone(1f, 0.05f), Is.EqualTo(1f).Within(1e-6f));
            Assert.That(PlayerInputPipeline.ApplyDeadzone(0.5f, 0f), Is.EqualTo(0.5f));
        }
    }
}
