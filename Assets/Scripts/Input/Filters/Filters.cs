using System;
using UnityEngine;
using SSRP.Core;

namespace SSRP.Input
{
    /// <summary>Filters selectable from the inspector. Add a value + a case in FilterFactory to add one.</summary>
    public enum FilterType
    {
        PassThrough,
        Exponential,
        MovingAverage,
        Median,
        Butterworth
    }

    /// <summary>One stage of a filter chain as configured in the inspector.</summary>
    [Serializable]
    public class FilterStage
    {
        public FilterType type = FilterType.PassThrough;

        [Min(0f), Tooltip("Exponential: time constant tau in seconds (63% of a step after tau).")]
        public float timeConstant = 0.05f;

        [Range(1, 101), Tooltip("Moving Average / Median: number of samples in the window.")]
        public int windowSize = 5;

        [Min(0.01f), Tooltip("Butterworth: cutoff frequency in Hz.")]
        public float cutoffFrequency = 5f;

        [Range(1, 4), Tooltip("Butterworth: filter order. Higher order gives a steeper roll-off and more delay.")]
        public int order = 2;
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

                case FilterType.MovingAverage:
                    return new MovingAverageFilter(stage.windowSize);

                case FilterType.Median:
                    return new MedianFilter(stage.windowSize);

                case FilterType.Butterworth:
                    return new ButterworthFilter(stage.cutoffFrequency, stage.order);

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

    public sealed class MovingAverageFilter : ISignalFilter
    {
        private readonly float[] samples;
        private int count;
        private int next;
        private float sum;

        public MovingAverageFilter(int windowSize)
        {
            samples = new float[Mathf.Clamp(windowSize, 1, 101)];
        }

        public float Process(float x, float dt)
        {
            if (count < samples.Length)
                count++;
            else
                sum -= samples[next];

            samples[next] = x;
            sum += x;
            next = (next + 1) % samples.Length;
            return sum / count;
        }

        public void Reset()
        {
            Array.Clear(samples, 0, samples.Length);
            count = 0;
            next = 0;
            sum = 0f;
        }
    }

    public sealed class MedianFilter : ISignalFilter
    {
        private readonly float[] samples;
        private readonly float[] sorted;
        private int count;
        private int next;

        public MedianFilter(int windowSize)
        {
            samples = new float[Mathf.Clamp(windowSize, 1, 101)];
            sorted = new float[samples.Length];
        }

        public float Process(float x, float dt)
        {
            samples[next] = x;
            next = (next + 1) % samples.Length;
            if (count < samples.Length)
                count++;

            Array.Copy(samples, sorted, count);
            Array.Sort(sorted, 0, count);
            int middle = count / 2;
            return count % 2 == 0
                ? (sorted[middle - 1] + sorted[middle]) * 0.5f
                : sorted[middle];
        }

        public void Reset()
        {
            Array.Clear(samples, 0, samples.Length);
            Array.Clear(sorted, 0, sorted.Length);
            count = 0;
            next = 0;
        }
    }

    /// <summary>
    /// Digital Butterworth low-pass made from cascaded first- and second-order sections.
    /// Cutoff is recalculated from dt so the setting remains time-based.
    /// </summary>
    public sealed class ButterworthFilter : ISignalFilter
    {
        private readonly float cutoffFrequency;
        private readonly int order;
        private readonly float[] firstOrderState = new float[1];
        private readonly Biquad[] sections;
        private float lastDt = -1f;
        private float output;

        public ButterworthFilter(float cutoffFrequencyHz, int filterOrder)
        {
            cutoffFrequency = Mathf.Max(0.01f, cutoffFrequencyHz);
            order = Mathf.Clamp(filterOrder, 1, 4);
            int biquadCount = order / 2;
            sections = new Biquad[biquadCount];
            for (int i = 0; i < sections.Length; i++)
                sections[i] = new Biquad();
        }

        public float Process(float x, float dt)
        {
            if (dt <= 0f)
                return output;

            if (!Mathf.Approximately(lastDt, dt))
                Configure(dt);

            float value = x;
            if ((order & 1) != 0)
            {
                float alpha = 1f - Mathf.Exp(-2f * Mathf.PI * cutoffFrequency * dt);
                firstOrderState[0] += alpha * (value - firstOrderState[0]);
                value = firstOrderState[0];
            }

            for (int i = 0; i < sections.Length; i++)
                value = sections[i].Process(value);

            output = value;
            return output;
        }

        public void Reset()
        {
            firstOrderState[0] = 0f;
            output = 0f;
            lastDt = -1f;
            for (int i = 0; i < sections.Length; i++)
                sections[i].Reset();
        }

        private void Configure(float dt)
        {
            lastDt = dt;
            float frequency = Mathf.Clamp(cutoffFrequency * dt, 0.00001f, 0.49f);
            float omega = 2f * Mathf.PI * frequency;
            float cosine = Mathf.Cos(omega);
            float sine = Mathf.Sin(omega);

            for (int i = 0; i < sections.Length; i++)
            {
                int sectionNumber = i + 1;
                float angle = Mathf.PI * (2f * sectionNumber - 1f) / (2f * order);
                float q = 1f / (2f * Mathf.Sin(angle));
                sections[i].Configure(cosine, sine, q);
            }
        }

        private sealed class Biquad
        {
            private float b0;
            private float b1;
            private float b2;
            private float a1;
            private float a2;
            private float z1;
            private float z2;

            public void Configure(float cosine, float sine, float q)
            {
                float alpha = sine / (2f * q);
                float a0 = 1f + alpha;
                b0 = ((1f - cosine) * 0.5f) / a0;
                b1 = (1f - cosine) / a0;
                b2 = b0;
                a1 = -2f * cosine / a0;
                a2 = (1f - alpha) / a0;
            }

            public float Process(float input)
            {
                float output = b0 * input + z1;
                z1 = b1 * input - a1 * output + z2;
                z2 = b2 * input - a2 * output;
                return output;
            }

            public void Reset()
            {
                z1 = 0f;
                z2 = 0f;
            }
        }
    }
}
