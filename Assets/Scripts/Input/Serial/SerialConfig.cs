using UnityEngine;

namespace SSRP.Input
{
    [CreateAssetMenu(fileName = "SerialConfig", menuName = "SSRP/Configs/Serial Config")]
    public class SerialConfig : ScriptableObject
    {
        [Tooltip("Default port when nothing was remembered (Windows: COM3, macOS: /dev/cu.usbmodem..., Linux: /dev/ttyACM0).")]
        public string portName = "COM3";

        public int baudRate = 115200;

        [Min(10), Tooltip("ReadLine timeout of the background thread (ms).")]
        public int readTimeoutMs = 50;

        [Min(50), Tooltip("Watchdog: no valid line for this long = disconnected, inputs go to zero.")]
        public int watchdogMs = 250;

        [Min(0.5f), Tooltip("Auto-reconnect attempt interval after an unplug (s).")]
        public float reconnectIntervalSeconds = 2f;
    }
}
