
using UnityEngine;
using SSRP.Core;

namespace SSRP.Input
{
    //Gives two options of input signal sources to select from
    public enum ScriptedPattern
    {
        SineSweep,
        Step
    }

    //We use MonoBehaviour as it directly integrated the c# script to Unity, so it can work as a component
    public class ScriptedInputSource : MonoBehaviour, IPlayerInputSource
    {
        [Header("Pattern Selection")]
        [SerializeField]
        private ScriptedPattern pattern = ScriptedPattern.SineSweep;

        [Header("Sine Sweep Settings")]
        [SerializeField, Min(0.01f)]
        private float sweepDuration = 10f;

        //the starting frequency
        [SerializeField, Min(0.01f)]
        private float minFrequency = 0.2f;

        //Ending frequency
        [SerializeField, Min(0.01f)]
        private float maxFrequency = 2f;

        //Sweep duration of the sine wave
        [Header("Step Settings")]
        [SerializeField, Min(0.01f)]
        private float stepDuration = 1f;

        private const float MAX_READING = 1023f;
        private const float CENTER = MAX_READING / 2f;

        public bool IsConnected => true;

        public bool TryGetLatest(
            int playerId, out RawFsrSample sample)
        {
            sample = default;

            if (playerId != 1 && playerId != 2)
                return false;

            float up;
            float down;

            if (pattern == ScriptedPattern.SineSweep)
            {
                GenerateSineSweep(out up, out down);
            }
            else
            {
                GenerateStep(playerId, out up, out down);
            }

            sample = new RawFsrSample
            {
                Up = up,
                Down = down,
                Time = Time.timeAsDouble
            };

            return true;
        }

        private void GenerateSineSweep(
            out float up, out float down)
        {
            float t = Time.time % sweepDuration;

            // Frequency increases gradually over the sweep.
            float progress = t / sweepDuration;

            //float frequency = Mathf.Lerp(
            //    minFrequency,
            //    maxFrequency,
            //    progress
            //);

            // Approximate phase for a continuously increasing frequency.
            float phase = 2f * Mathf.PI *
                (minFrequency * t +
                0.5f * (maxFrequency - minFrequency)
                * t * t / sweepDuration);

            float signal = Mathf.Sin(phase);

            up = Mathf.Clamp(
                CENTER + CENTER * signal, 0f, MAX_READING);

            down = Mathf.Clamp(
                CENTER - CENTER * signal, 0f, MAX_READING);
        }

        //Generates four distinct states for player 1: (0, 0), (1023, 0), (1023, 1023), (0, 1023)
        //Each pair represents the UP and DOWNN readings resembling a square wave input
        //Player 2 is given a time advance so it is distinguishable from player 1
        //This is just a test to see if the code works properly or not
        
        private void GenerateStep(
            int playerId, out float up, out float down)
        {
            int step = Mathf.FloorToInt(
                Time.time / stepDuration);

            // Offset Player 2 so its pattern is distinguishable.
            step += playerId - 1;

            switch (step % 4)
            {
                case 0:
                    up = 0f;
                    down = 0f;
                    break;

                case 1:
                    up = MAX_READING;
                    down = 0f;
                    break;

                case 2:
                    up = MAX_READING;
                    down = MAX_READING;
                    break;

                default:
                    up = 0f;
                    down = MAX_READING;
                    break;
            }
        }

        //This code is just for testing the script on the screen
        //Can be removed when we connect the actual Arduino Interface and make the actual
        private void OnGUI()
            {
            TryGetLatest(1, out RawFsrSample p1);
            TryGetLatest(2, out RawFsrSample p2);

            GUI.Label(new Rect(10, 140, 400, 25),
                $"P1: {p1.Up:F0} | {p1.Down:F0}");

            GUI.Label(new Rect(10, 160, 400, 25),
                $"P2: {p2.Up:F0} | {p2.Down:F0}");
            }   
    }
}
