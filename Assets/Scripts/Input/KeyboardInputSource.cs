
using UnityEngine;
using UnityEngine.InputSystem;
using SSRP.Core;

namespace SSRP.Input
{
    //IPlayerInputSource is basically a contract(the interface defining the structure but not how to implement it)
    //this class KeyboardInputSource signs the contract(inherits the interface) and writes the code for implementing the structure)
    //Kind of like how rules are one-liners, they just tell what to do
    //we, the society, utilize them in our own way, we shape that rule according to us
    public class KeyboardInputSource : MonoBehaviour, IPlayerInputSource
    {
        [Header("Input Settings")]
        [SerializeField, Min(0.01f)]
        private float rampSeconds = 0.1f;

        private const float MAX_READING = 1023f;

        // Player 1 readings
        private float p1Up;
        private float p1Down;

        // Player 2 readings
        private float p2Up;
        private float p2Down;

        public bool IsConnected => true;

        private void Update()
        {
            float dt = Time.deltaTime;

            // Check whether a keyboard is available
            if (Keyboard.current == null)
                return;

            // Player 1: W = Up, S = Down
            p1Up = UpdateChannel(
                p1Up,
                Keyboard.current.wKey.isPressed,
                dt
            );

            p1Down = UpdateChannel(
                p1Down,
                Keyboard.current.sKey.isPressed,
                dt
            );

            // Player 2: Up Arrow = Up, Down Arrow = Down
            p2Up = UpdateChannel(
                p2Up,
                Keyboard.current.upArrowKey.isPressed,
                dt
            );

            p2Down = UpdateChannel(
                p2Down,
                Keyboard.current.downArrowKey.isPressed,
                dt
            );
        }

        private float UpdateChannel(
            float currentValue, bool isPressed, float dt)
        {
            float targetValue = isPressed ? MAX_READING : 0f;

            float rampRate = MAX_READING / rampSeconds;

            //It moves a value towards a target by a maximum permitted amount.
            return Mathf.MoveTowards(
                currentValue,
                targetValue,
                rampRate * dt
            );
        }

        public bool TryGetLatest(
            int playerId, out RawFsrSample sample)
        {
            sample = default;

            if (playerId == 1)
            {
                sample = new RawFsrSample
                {
                    Up = p1Up,
                    Down = p1Down,
                    Time = Time.timeAsDouble
                };

                return true;
            }

            if (playerId == 2)
            {
                sample = new RawFsrSample
                {
                    Up = p2Up,
                    Down = p2Down,
                    Time = Time.timeAsDouble
                };

                return true;
            }

            return false;
        }

        private void OnGUI()
            {
        bool newInput =
            Keyboard.current != null &&
            Keyboard.current.wKey.isPressed;

        GUI.Label(new Rect(10, 10, 400, 25),
            $"Input System W: {newInput}");

        GUI.Label(new Rect(10, 50, 400, 25),
            $"P1 Up: {p1Up:F0} | Down: {p1Down:F0}");

        GUI.Label(new Rect(10, 80, 400, 25),
            $"P2 Up: {p2Up:F0} | Down: {p2Down:F0}");

        RawFsrSample sample;

        bool success = TryGetLatest(1, out sample);

        GUI.Label(new Rect(10, 170, 500, 25),
            $"Interface: {success} | Up: {sample.Up:F0} | Down: {sample.Down:F0}");
        }
    }
}
