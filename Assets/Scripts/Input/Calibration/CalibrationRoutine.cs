using System.Collections.Generic;

namespace SSRP.Input
{
    /// <summary>
    /// [A4] Guided calibration as a plain C# state machine (no Unity calls, easy to test).
    ///
    /// Full:  Rest (all released, 2 s) -> Press P1 Up -> Press P1 Down -> Press P2 Up -> Press P2 Down (3 s each)
    /// Quick re-zero: Rest only (updates baselines, keeps the existing max values).
    ///
    /// baseline = median of the rest samples; max = 95th percentile of the press samples,
    /// so single spikes cannot corrupt either value. The first <see cref="SettleSeconds"/> of
    /// each step are ignored while the player gets into position.
    /// </summary>
    public class CalibrationRoutine
    {
        public const float RestSeconds = 2f;
        public const float PressSeconds = 3f;
        public const float SettleSeconds = 0.5f;
        public const float MaxPercentile = 95f;
        public const float DefaultMinSpan = 150f;
        public const float DefaultDeadSpan = 30f;

        public enum StepKind { Rest, Press, Done }

        private readonly List<float>[] restSamples = new List<float>[CalibrationData.ChannelCount];
        private readonly List<float>[] pressSamples = new List<float>[CalibrationData.ChannelCount];
        private readonly CalibrationData previous;
        private readonly bool quick;
        private readonly float minSpan;
        private readonly float deadSpan;

        private int stepIndex;          // 0 = rest, 1..4 = press channel (stepIndex-1)
        private float stepTime;

        public CalibrationRoutine(bool quickRezero, CalibrationData existing,
            float minimumSpan = DefaultMinSpan, float deadChannelSpan = DefaultDeadSpan)
        {
            quick = quickRezero;
            previous = existing != null ? existing.Clone() : CalibrationData.CreateDefault();
            minSpan = minimumSpan;
            deadSpan = deadChannelSpan;

            for (int i = 0; i < CalibrationData.ChannelCount; i++)
            {
                restSamples[i] = new List<float>(256);
                pressSamples[i] = new List<float>(256);
            }
        }

        public bool IsQuick => quick;
        public bool IsDone => CurrentKind == StepKind.Done;
        public bool Succeeded { get; private set; }
        public string Message { get; private set; } = "";
        public CalibrationData Result { get; private set; }

        private int StepCount => quick ? 1 : 1 + CalibrationData.ChannelCount;

        public StepKind CurrentKind =>
            stepIndex >= StepCount ? StepKind.Done : (stepIndex == 0 ? StepKind.Rest : StepKind.Press);

        /// <summary>Channel being pressed in the current Press step (-1 otherwise).</summary>
        public int CurrentChannel => CurrentKind == StepKind.Press ? stepIndex - 1 : -1;

        public float StepDuration => CurrentKind == StepKind.Rest ? RestSeconds : PressSeconds;

        public float StepProgress => IsDone ? 1f : stepTime / StepDuration;

        public float OverallProgress => IsDone ? 1f : (stepIndex + StepProgress) / StepCount;

        public string Prompt
        {
            get
            {
                switch (CurrentKind)
                {
                    case StepKind.Rest:
                        return "Release ALL sensors and keep your hands off...";
                    case StepKind.Press:
                        return $"Press {CalibrationData.ChannelNames[CurrentChannel]} FIRMLY and hold";
                    default:
                        return Succeeded ? "Calibration saved." : "Calibration rejected.";
                }
            }
        }

        /// <summary>Feed one sample of all 4 channels (raw counts) and the elapsed time.</summary>
        public void Tick(float dt, float[] channels, bool hasSample)
        {
            if (IsDone)
                return;

            stepTime += dt;

            if (hasSample && channels != null && stepTime >= SettleSeconds)
            {
                if (CurrentKind == StepKind.Rest)
                {
                    for (int i = 0; i < CalibrationData.ChannelCount; i++)
                        restSamples[i].Add(channels[i]);
                }
                else
                {
                    pressSamples[CurrentChannel].Add(channels[CurrentChannel]);
                }
            }

            if (stepTime >= StepDuration)
            {
                stepIndex++;
                stepTime = 0f;

                if (IsDone)
                    Finish();
            }
        }

        private void Finish()
        {
            CalibrationData data = previous.Clone();

            for (int i = 0; i < CalibrationData.ChannelCount; i++)
            {
                if (restSamples[i].Count == 0)
                {
                    Succeeded = false;
                    Message = "No data received from the sensors. Is the Arduino connected?";
                    return;
                }

                data.channels[i].baseline = CalibrationMath.Median(restSamples[i]);

                if (!quick)
                {
                    data.channels[i].max = pressSamples[i].Count > 0
                        ? CalibrationMath.Percentile(pressSamples[i], MaxPercentile)
                        : data.channels[i].baseline;
                }
            }

            Succeeded = CalibrationMath.Validate(data, minSpan, deadSpan, out string message);
            Message = message;

            if (Succeeded)
                Result = data;
        }
    }
}
