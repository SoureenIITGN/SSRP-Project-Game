using System;
using UnityEngine;
using SSRP.Core;

namespace SSRP.Input
{
    /// <summary>Filters selectable from the inspector. Add a value + a case in FilterFactory to add one.</summary>
    public enum FilterType
    {
        PassThrough,
        Exponential
    }

    /// <summary>One stage of a filter chain as configured in the inspector.</summary>
    [Serializable]
    public class FilterStage
    {
        public FilterType type = FilterType.PassThrough;

        [Min(0f), Tooltip("Exponential: time constant tau in seconds (63% of a step after tau).")]
        public float timeConstant = 0.05f;
    }

    public static class FilterFactory
    {
        public static ISignalFilter Create(FilterStage stage)
        {
            if (stage == null)
                return new PassThroughFilter();

            switch (stage.type)
            {
                case FilterType.Exponential:
                    return new ExponentialFilter(stage.timeConstant);

                default:
                    return new PassThroughFilter();
            }
        }
    }

    /// <summary>Default filter: output = input.</summary>
    public sealed class PassThroughFilter : ISignalFilter
    {
        public float Process(float x, float dt) => x;

        public void Reset() { }
    }

    /// <summary>
    /// First-order IIR low-pass (exponential moving average): y += alpha * (x - y).
    /// alpha = 1 - exp(-dt / tau) makes the response independent of the frame rate:
    /// a unit step reaches 1 - e^-1 = 63.2% after one time constant.
    /// The output starts at 0 (sensors at rest). Proof that the filter hook works;
    /// real filter design is part of the analysis work.
    /// </summary>
    public sealed class ExponentialFilter : ISignalFilter
    {
        private readonly float timeConstant;
        private float y;

        public ExponentialFilter(float timeConstantSeconds)
        {
            timeConstant = Mathf.Max(0f, timeConstantSeconds);
        }

        public float TimeConstant => timeConstant;

        public float Process(float x, float dt)
        {
            if (timeConstant <= 0f)
            {
                y = x;
                return y;
            }

            if (dt <= 0f)
                return y;

            float alpha = 1f - Mathf.Exp(-dt / timeConstant);
            y += alpha * (x - y);
            return y;
        }

        public void Reset()
        {
            y = 0f;
        }
    }
}
