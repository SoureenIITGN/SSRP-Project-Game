
using UnityEngine;
using SSRP.Core;
using System;
using System.IO.Ports;
using System.Globalization;
using System.Text;

namespace SSRP.Input
{
    public class SerialInputSource : MonoBehaviour, IPlayerInputSource
    {
        [Header("Serial Settings")]
        [SerializeField]
        private string portName = "COM3";

        [SerializeField]
        private int baudRate = 115200;

        private SerialPort serialPort;
        private bool connectionAttempted;

        //Sometimes, the data does not necessarily arrive as one single line, it can be broken down too.
        //However, the previous code utilized readExisting, which only reads the current data in the buffer
        //to solve that challenge, we use a StringBuilder to accumulate incoming characters until a complete newline-terminated line is available.
        private readonly StringBuilder receiveBuffer =
            new StringBuilder();

        private RawFsrSample[] latestSamples =
            new RawFsrSample[3];

        private bool[] hasSample = new bool[3];

        public bool IsConnected =>
            serialPort != null && serialPort.IsOpen;

        private bool TryOpenSerialPort()
        {
            if (connectionAttempted)
                return IsConnected;

            connectionAttempted = true;

            try
            {
                serialPort = new SerialPort(portName, baudRate);
                serialPort.ReadTimeout = 50;
                serialPort.NewLine = "\n";

                serialPort.Open();

                Debug.Log("Serial connected: " + portName);
                return true;
            }
            catch (Exception e)
            {
                Debug.LogError(
                    "Could not open serial port: " + e.Message);

                CloseSerialPort();
                return false;
            }
        }

        private void Update()
        {
            if (!IsConnected)
                return;

            try
            {
                //Reads the incoming serial input.
                string incoming = serialPort.ReadExisting();

                if (!string.IsNullOrEmpty(incoming))
                {
                    receiveBuffer.Append(incoming);
                    ProcessReceivedLines();
                }
            }
            catch (Exception e)
            {
                Debug.LogError("Serial read error: " + e.Message);
                CloseSerialPort();
            }
        }

        private void ProcessReceivedLines()
        {
            while (true)
            {
                string data = receiveBuffer.ToString();
                int newlineIndex = data.IndexOf('\n');

                if (newlineIndex < 0)
                    break;

                string line = data.Substring(0, newlineIndex).Trim();

                receiveBuffer.Remove(0, newlineIndex + 1);

                ParseLine(line);
            }
        }

        private void ParseLine(string line)
        {
            string[] values = line.Split(',');

            if (values.Length != 3)
                return;

            bool validPlayer = int.TryParse(
                values[0], out int playerId);

            bool validUp = float.TryParse(
                values[1],
                NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out float up);

            bool validDown = float.TryParse(
                values[2],
                NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out float down);

            if (!validPlayer || !validUp || !validDown)
                return;

            if (playerId < 1 || playerId > 2)
                return;

            latestSamples[playerId] = new RawFsrSample
            {
                Up = Mathf.Clamp(up, 0f, 1023f),
                Down = Mathf.Clamp(down, 0f, 1023f),
                Time = Time.timeAsDouble
            };

            hasSample[playerId] = true;
        }

        public bool TryGetLatest(
            int playerId, out RawFsrSample sample)
        {
            sample = default;

            if (playerId < 1 || playerId > 2)
                return false;

            if (!TryOpenSerialPort())
                return false;

            if (!IsConnected || !hasSample[playerId])
                return false;

            sample = latestSamples[playerId];
            return true;
        }

        private void OnDisable()
        {
            CloseSerialPort();
            connectionAttempted = false;
        }

        private void OnDestroy()
        {
            CloseSerialPort();
        }

        private void CloseSerialPort()
        {
            if (serialPort != null)
            {
                try
                {
                    if (serialPort.IsOpen)
                        serialPort.Close();

                    serialPort.Dispose();
                }
                catch (Exception e)
                {
                    Debug.LogWarning(
                        "Serial port close warning: " + e.Message);
                }

                serialPort = null;
            }
        }
    }
}
