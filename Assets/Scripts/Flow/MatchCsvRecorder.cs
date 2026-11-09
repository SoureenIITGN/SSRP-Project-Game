using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;
using SSRP.Core;
using SSRP.Gameplay;
using SSRP.Input;

namespace SSRP.Flow
{
    /// <summary>
    /// Records one match as raw input values and paddle speeds. A new match replaces the
    /// previous CSV when recording starts.
    /// </summary>
    [DefaultExecutionOrder(-40)]
    public class MatchCsvRecorder : MonoBehaviour
    {
        private const string FileName = "ssrp_match_data.csv";

        private readonly List<Sample> samples = new List<Sample>(4096);
        private bool recording;

        public static string FilePath =>
            Path.Combine(Application.persistentDataPath, FileName);

        private struct Sample
        {
            public float up1;
            public float down1;
            public float up2;
            public float down2;
            public float speed1;
            public float speed2;
        }

        private void OnEnable()
        {
            GameEvents.MatchStateChanged += OnMatchStateChanged;
        }

        private void OnDisable()
        {
            GameEvents.MatchStateChanged -= OnMatchStateChanged;
            recording = false;
        }

        private void FixedUpdate()
        {
            if (!recording)
                return;

            GameManager manager = GameManager.Instance;
            if (manager == null || manager.Phase != MatchPhase.Playing ||
                manager.Players == null || manager.Ball == null)
                return;

            ReadRawInput(manager.Players.GetSlot(1), out float up1, out float down1);
            ReadRawInput(manager.Players.GetSlot(2), out float up2, out float down2);
            PaddleController paddle1 = manager.Players.GetPaddle(1);
            PaddleController paddle2 = manager.Players.GetPaddle(2);

            samples.Add(new Sample
            {
                up1 = up1,
                down1 = down1,
                up2 = up2,
                down2 = down2,
                speed1 = paddle1 != null ? paddle1.Velocity.magnitude : 0f,
                speed2 = paddle2 != null ? paddle2.Velocity.magnitude : 0f
            });
        }

        private void OnMatchStateChanged(MatchPhase phase)
        {
            if (phase == MatchPhase.Playing && !recording)
            {
                samples.Clear();
                recording = true;
                return;
            }

            if (phase == MatchPhase.MatchOver && recording)
            {
                recording = false;
                WriteCsv();
            }
        }

        private static void ReadRawInput(PlayerSlot slot, out float up, out float down)
        {
            up = 0f;
            down = 0f;

            if (slot != null && slot.TryGetLatest(out RawFsrSample sample))
            {
                up = Mathf.Clamp(sample.Up, 0f, 1023f);
                down = Mathf.Clamp(sample.Down, 0f, 1023f);
            }
        }

        private void WriteCsv()
        {
            try
            {
                var csv = new StringBuilder(samples.Count * 32);
                csv.AppendLine("Up1,Down1,Speed1,Up2,Down2,Speed2");

                for (int i = 0; i < samples.Count; i++)
                {
                    Sample sample = samples[i];
                    csv.Append(sample.up1.ToString("0.###", CultureInfo.InvariantCulture)).Append(',');
                    csv.Append(sample.down1.ToString("0.###", CultureInfo.InvariantCulture)).Append(',');
                    csv.Append(sample.speed1.ToString("0.###", CultureInfo.InvariantCulture)).Append(',');
                    csv.Append(sample.up2.ToString("0.###", CultureInfo.InvariantCulture)).Append(',');
                    csv.Append(sample.down2.ToString("0.###", CultureInfo.InvariantCulture)).Append(',');
                    csv.AppendLine(sample.speed2.ToString("0.###", CultureInfo.InvariantCulture));
                }

                File.WriteAllText(FilePath, csv.ToString(), Encoding.UTF8);
                Debug.Log($"[MatchData] Saved {samples.Count} samples to {FilePath}", this);
            }
            catch (Exception e)
            {
                Debug.LogError($"[MatchData] Could not save {FilePath}: {e.Message}", this);
            }
        }
    }
}
