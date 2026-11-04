using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace SSRP.Input
{
    [Serializable]
    public struct ChannelCalibration
    {
        public float baseline;
        public float max;

        public float Span => max - baseline;
    }

    /// <summary>
    /// [A4] Per-channel baseline (rest) and max (firm press), in raw ADC counts.
    /// Channel order matches the protocol: 0 = P1 Up, 1 = P1 Down, 2 = P2 Up, 3 = P2 Down.
    /// </summary>
    [Serializable]
    public class CalibrationData
    {
        public const int ChannelCount = 4;

        public static readonly string[] ChannelNames =
        {
            "P1 Up (A0)", "P1 Down (A1)", "P2 Up (A2)", "P2 Down (A3)"
        };

        public ChannelCalibration[] channels = CreateDefaultChannels();
        public string createdUtc = "";
        public bool isDefault = true;

        public static CalibrationData CreateDefault() => new CalibrationData();

        private static ChannelCalibration[] CreateDefaultChannels()
        {
            var result = new ChannelCalibration[ChannelCount];
            for (int i = 0; i < ChannelCount; i++)
                result[i] = new ChannelCalibration { baseline = 0f, max = 1023f };
            return result;
        }

        public bool TryGetChannel(int channel, out ChannelCalibration value)
        {
            value = default;

            if (channels == null || channel < 0 || channel >= channels.Length)
                return false;

            value = channels[channel];
            return value.Span > 0f;
        }

        public CalibrationData Clone()
        {
            var copy = new CalibrationData
            {
                channels = (ChannelCalibration[])channels.Clone(),
                createdUtc = createdUtc,
                isDefault = isDefault
            };
            return copy;
        }
    }

    /// <summary>Saves/loads calibration as JSON in Application.persistentDataPath.</summary>
    public static class CalibrationStore
    {
        public const string FileName = "ssrp_calibration.json";

        private static CalibrationData current;

        public static event Action<CalibrationData> Changed;

        public static string FilePath => Path.Combine(Application.persistentDataPath, FileName);

        /// <summary>The active calibration (defaults 0-1023 if none was saved).</summary>
        public static CalibrationData Current
        {
            get
            {
                if (current == null)
                    current = LoadFromDisk() ?? CalibrationData.CreateDefault();

                return current;
            }
        }

        public static bool HasSavedCalibration => !Current.isDefault;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            current = null;
            Changed = null;
        }

        public static void Save(CalibrationData data)
        {
            data.isDefault = false;
            data.createdUtc = DateTime.UtcNow.ToString("o");
            current = data;

            try
            {
                File.WriteAllText(FilePath, JsonUtility.ToJson(data, true));
            }
            catch (Exception e)
            {
                Debug.LogWarning("[Calibration] Could not save: " + e.Message);
            }

            Changed?.Invoke(current);
        }

        public static void ResetToDefaults()
        {
            current = CalibrationData.CreateDefault();

            try
            {
                if (File.Exists(FilePath))
                    File.Delete(FilePath);
            }
            catch (Exception e)
            {
                Debug.LogWarning("[Calibration] Could not delete file: " + e.Message);
            }

            Changed?.Invoke(current);
        }

        private static CalibrationData LoadFromDisk()
        {
            try
            {
                if (!File.Exists(FilePath))
                    return null;

                CalibrationData data = JsonUtility.FromJson<CalibrationData>(File.ReadAllText(FilePath));

                if (data == null || data.channels == null || data.channels.Length != CalibrationData.ChannelCount)
                    return null;

                data.isDefault = false;
                return data;
            }
            catch (Exception e)
            {
                Debug.LogWarning("[Calibration] Could not load, using defaults: " + e.Message);
                return null;
            }
        }
    }

    /// <summary>Robust statistics used by calibration (median / percentile ignore spikes).</summary>
    public static class CalibrationMath
    {
        public static float Median(List<float> values) => Percentile(values, 50f);

        /// <summary>Linear-interpolated percentile (0-100). Sorts a copy; returns 0 for empty input.</summary>
        public static float Percentile(List<float> values, float percent)
        {
            if (values == null || values.Count == 0)
                return 0f;

            var sorted = new List<float>(values);
            sorted.Sort();

            float rank = Mathf.Clamp(percent, 0f, 100f) / 100f * (sorted.Count - 1);
            int lo = Mathf.FloorToInt(rank);
            int hi = Mathf.Min(lo + 1, sorted.Count - 1);
            return Mathf.Lerp(sorted[lo], sorted[hi], rank - lo);
        }

        /// <summary>
        /// Rejects the calibration if any channel's span is below <paramref name="minSpan"/>.
        /// A span below <paramref name="deadSpan"/> is reported as "not responding" (wiring problem).
        /// </summary>
        public static bool Validate(CalibrationData data, float minSpan, float deadSpan, out string message)
        {
            var problems = new List<string>();

            for (int i = 0; i < CalibrationData.ChannelCount; i++)
            {
                ChannelCalibration c = data.channels[i];
                float span = c.Span;

                if (span < deadSpan)
                    problems.Add($"{CalibrationData.ChannelNames[i]} is not responding (moved {span:0} counts) - check its wiring.");
                else if (span < minSpan)
                    problems.Add($"{CalibrationData.ChannelNames[i]} only moved {span:0} counts (need {minSpan:0}) - press harder.");
            }

            if (problems.Count == 0)
            {
                message = "Calibration OK.";
                return true;
            }

            message = string.Join("\n", problems);
            return false;
        }
    }
}
