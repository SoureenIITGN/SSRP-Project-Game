using System;
using System.IO.Ports;
using UnityEngine;

namespace SSRP.Input
{
    /// <summary>
    /// [A3] Owns the Arduino connection lifetime: list ports, connect/disconnect, remember the
    /// last port (PlayerPrefs), and auto-reconnect every ~2 s after an unplug.
    ///
    /// Watchdog: SerialInputSource stops returning samples 250 ms after the last valid line.
    /// Each PlayerInputPipeline then outputs zeros and raises
    /// GameEvents.InputConnectionChanged(playerId, false) for the players using the Arduino,
    /// so the paddles freeze and the GameManager shows the pause screen.
    /// </summary>
    public class SerialConnectionManager : MonoBehaviour
    {
        public enum ConnectionStatus
        {
            Disconnected,
            Connected,
            NoData,
            Reconnecting
        }

        public const string LastPortPrefKey = "ssrp.serial.lastPort";

        public static SerialConnectionManager Instance { get; private set; }

        [SerializeField] private SerialInputSource source;
        [SerializeField] private SerialConfig config;
        [SerializeField, Tooltip("Reconnect to the remembered port when the game starts (if it is present).")]
        private bool autoConnectOnStart = true;

        private bool wantConnection;
        private string desiredPort;
        private float nextAttemptTime;
        private ConnectionStatus status = ConnectionStatus.Disconnected;

        public event Action<ConnectionStatus> StatusChanged;

        public ConnectionStatus Status => status;
        public SerialInputSource Source => source;
        public bool WantsConnection => wantConnection;
        public string DesiredPort => desiredPort;

        public string LastPort =>
            PlayerPrefs.GetString(LastPortPrefKey, config != null ? config.portName : "COM3");

        private float ReconnectInterval => config != null ? config.reconnectIntervalSeconds : 2f;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => Instance = null;

        private void Awake()
        {
            if (Instance == null)
                Instance = this;

            if (source == null)
                source = GetComponent<SerialInputSource>();
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        private void Start()
        {
            if (!autoConnectOnStart || !PlayerPrefs.HasKey(LastPortPrefKey))
                return;

            string port = LastPort;

            if (PortExists(port))
                Connect(port);
        }

        /// <summary>Available serial ports, sorted. Never throws.</summary>
        public static string[] GetPortNames()
        {
            try
            {
                string[] names = SerialPort.GetPortNames();
                Array.Sort(names, StringComparer.OrdinalIgnoreCase);
                return names;
            }
            catch (Exception e)
            {
                Debug.LogWarning("[Serial] Could not list ports: " + e.Message);
                return Array.Empty<string>();
            }
        }

        private static bool PortExists(string port)
        {
            if (string.IsNullOrEmpty(port))
                return false;

            string[] ports = GetPortNames();

            // Some platforms return an empty list even when the device exists; try anyway.
            if (ports.Length == 0)
                return true;

            return Array.IndexOf(ports, port) >= 0;
        }

        public bool Connect(string port)
        {
            if (source == null || string.IsNullOrEmpty(port))
                return false;

            desiredPort = port;
            wantConnection = true;
            PlayerPrefs.SetString(LastPortPrefKey, port);
            PlayerPrefs.Save();

            bool ok = source.Connect(port);
            nextAttemptTime = Time.unscaledTime + ReconnectInterval;
            RefreshStatus();
            return ok;
        }

        public void Disconnect()
        {
            wantConnection = false;

            if (source != null)
                source.Disconnect();

            RefreshStatus();
        }

        private void Update()
        {
            if (source == null)
                return;

            // Auto-reconnect after an unplug (the reader thread faulted, so the port is closed).
            if (wantConnection && !source.IsOpen && Time.unscaledTime >= nextAttemptTime)
            {
                nextAttemptTime = Time.unscaledTime + ReconnectInterval;

                if (PortExists(desiredPort))
                    source.Connect(desiredPort);
            }

            RefreshStatus();
        }

        private void RefreshStatus()
        {
            ConnectionStatus next;

            if (!wantConnection)
                next = ConnectionStatus.Disconnected;
            else if (source != null && source.IsConnected)
                next = ConnectionStatus.Connected;
            else if (source != null && source.IsOpen)
                next = ConnectionStatus.NoData;
            else
                next = ConnectionStatus.Reconnecting;

            if (next == status)
                return;

            status = next;
            StatusChanged?.Invoke(status);
        }
    }
}
