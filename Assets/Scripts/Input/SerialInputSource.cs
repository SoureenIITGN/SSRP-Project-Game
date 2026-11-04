
using System;
using System.IO.Ports;
using System.Threading;
using UnityEngine;
using SSRP.Core;

namespace SSRP.Input
{
    /// <summary>
    /// Arduino FSR source ([A2]). A background thread reads lines from the serial port
    /// (ReadLine + ReadTimeout), parses them with <see cref="SerialLineParser"/> and publishes
    /// the newest frame into a thread-safe buffer. Unity APIs are only used on the main thread.
    ///
    /// Requires Project Settings > Player > Api Compatibility Level = .NET Framework
    /// (System.IO.Ports is not part of .NET Standard). Already set in ProjectSettings.
    ///
    /// Connection lifetime (open, reconnect, remember port) is handled by
    /// <see cref="SerialConnectionManager"/>; this class just opens/closes and reads.
    /// </summary>
    public class SerialInputSource : MonoBehaviour, IPlayerInputSource
    {
        [Header("Serial Settings")]
        [SerializeField, Tooltip("Optional. When set, its values override the two fields below.")]
        private SerialConfig config;

        [SerializeField]
        private string portName = "COM3";

        [SerializeField]
        private int baudRate = 115200;

        private readonly LatestFrameBuffer buffer = new LatestFrameBuffer();

        private SerialPort serialPort;
        private Thread readerThread;
        private volatile bool running;
        private volatile bool faulted;
        private volatile string lastError = "";
        private volatile string bootBanner = "";

        public string PortName => portName;
        public int BaudRate => config != null ? config.baudRate : baudRate;

        private double WatchdogSeconds => (config != null ? config.watchdogMs : 250) / 1000.0;
        private int ReadTimeoutMs => config != null ? config.readTimeoutMs : 50;

        /// <summary>The port is open and the reader thread is alive.</summary>
        public bool IsOpen => serialPort != null && running && !faulted;

        /// <summary>Open AND a valid line arrived within the watchdog window (250 ms).</summary>
        public bool IsConnected => IsOpen && buffer.Age <= WatchdogSeconds;

        public bool Faulted => faulted;
        public string LastError => lastError;
        public string BootBanner => bootBanner;
        public float PacketsPerSecond => buffer.PacketsPerSecond;
        public long PacketCount => buffer.PacketCount;
        public long ParseErrors => buffer.ParseErrors;

        /// <summary>Seconds since the last valid line (infinity if none).</summary>
        public double LastPacketAge => buffer.Age;

        public bool Connect(string port)
        {
            return Connect(port, BaudRate);
        }

        public bool Connect(string port, int baud)
        {
            Disconnect();

            portName = port;
            baudRate = baud;
            faulted = false;
            lastError = "";
            bootBanner = "";
            buffer.Clear();

            try
            {
                serialPort = new SerialPort(NormalisePortName(port), baud, Parity.None, 8, StopBits.One)
                {
                    ReadTimeout = ReadTimeoutMs,
                    NewLine = "\n",
                    DtrEnable = true,
                    RtsEnable = true
                };

                serialPort.Open();
                serialPort.DiscardInBuffer();

                running = true;
                readerThread = new Thread(ReadLoop)
                {
                    IsBackground = true,
                    Name = "SSRP Serial Reader"
                };
                readerThread.Start();

                Debug.Log($"[Serial] Opened {port} @ {baud}");
                return true;
            }
            catch (Exception e)
            {
                lastError = e.Message;
                Debug.LogWarning($"[Serial] Could not open {port}: {e.Message}");
                Disconnect();
                return false;
            }
        }

        /// <summary>Close the port and join the reader thread (safe to call repeatedly).</summary>
        public void Disconnect()
        {
            running = false;

            SerialPort port = serialPort;
            serialPort = null;

            if (port != null)
            {
                try
                {
                    if (port.IsOpen)
                        port.Close();
                }
                catch (Exception e)
                {
                    Debug.LogWarning("[Serial] Close warning: " + e.Message);
                }
            }

            Thread thread = readerThread;
            readerThread = null;

            if (thread != null && thread.IsAlive && thread != Thread.CurrentThread)
            {
                if (!thread.Join(500))
                    Debug.LogWarning("[Serial] Reader thread did not stop within 500 ms.");
            }

            if (port != null)
            {
                try { port.Dispose(); }
                catch (Exception) { /* already closed */ }
            }
        }

        private void ReadLoop()
        {
            SerialPort port = serialPort;

            while (running)
            {
                string line;

                try
                {
                    line = port.ReadLine();
                }
                catch (TimeoutException)
                {
                    continue;
                }
                catch (Exception e)
                {
                    // Unplugged cable, port closed by Disconnect(), driver error...
                    if (running)
                    {
                        lastError = e.Message;
                        faulted = true;
                    }

                    break;
                }

                switch (SerialLineParser.TryParse(line, out FsrFrame frame))
                {
                    case ParseResult.Ok:
                        buffer.Write(frame);
                        break;

                    case ParseResult.Comment:
                        if (line.Contains("SSRP-FSR"))
                            bootBanner = line.Trim();
                        break;

                    case ParseResult.Malformed:
                        buffer.CountParseError();
                        break;
                }
            }
        }

        private void Update()
        {
            buffer.UpdateRate();
        }

        public bool TryGetLatest(
            int playerId, out RawFsrSample sample)
        {
            sample = default;

            if (!IsOpen)
                return false;

            return buffer.TryGetPlayerSample(playerId, WatchdogSeconds, out sample);
        }

        /// <summary>Raw access to all 4 channels (used by calibration).</summary>
        public bool TryGetChannels(float[] destination, out double time)
        {
            time = -1;

            if (!IsOpen || destination == null || destination.Length < 4)
                return false;

            if (!buffer.TryRead(out FsrFrame frame, out time))
                return false;

            if (InputClock.Now - time > WatchdogSeconds)
                return false;

            for (int i = 0; i < 4; i++)
                destination[i] = frame[i];

            return true;
        }

        /// <summary>Windows needs the \\.\ prefix for COM10 and above.</summary>
        private static string NormalisePortName(string port)
        {
            if (Application.platform != RuntimePlatform.WindowsPlayer &&
                Application.platform != RuntimePlatform.WindowsEditor)
                return port;

            if (port.StartsWith("COM", StringComparison.OrdinalIgnoreCase) &&
                int.TryParse(port.Substring(3), out int n) && n >= 10)
                return @"\\.\" + port;

            return port;
        }

        // Clean shutdown: otherwise the COM port stays locked and the next Play press fails.
        private void OnDisable()
        {
            Disconnect();
        }

        private void OnApplicationQuit()
        {
            Disconnect();
        }

        private void OnDestroy()
        {
            Disconnect();
        }
    }
}
