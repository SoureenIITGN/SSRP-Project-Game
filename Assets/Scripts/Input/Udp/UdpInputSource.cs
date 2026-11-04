using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using UnityEngine;
using SSRP.Core;

namespace SSRP.Input
{
    /// <summary>
    /// [A6] Receives the same SSRP-FSR v1 lines as the serial port, but over UDP (for example from a
    /// Python script that filters the signal first). Background thread, same parser, same
    /// LatestFrameBuffer and the same 250 ms watchdog. Switching a player from Serial to UDP changes
    /// nothing else in the game. Test sender: python/udp_sender.py.
    /// </summary>
    public class UdpInputSource : MonoBehaviour, IPlayerInputSource
    {
        [SerializeField] private int port = 5005;
        [SerializeField, Tooltip("Start listening when enabled.")] private bool listenOnEnable = true;
        [SerializeField, Min(50)] private int watchdogMs = 250;

        private readonly LatestFrameBuffer buffer = new LatestFrameBuffer();
        private UdpClient client;
        private Thread thread;
        private volatile bool running;
        private volatile string lastError = "";

        public int Port => port;
        public bool IsListening => running && client != null;
        public bool IsConnected => IsListening && buffer.Age <= watchdogMs / 1000.0;
        public float PacketsPerSecond => buffer.PacketsPerSecond;
        public long PacketCount => buffer.PacketCount;
        public long ParseErrors => buffer.ParseErrors;
        public double LastPacketAge => buffer.Age;
        public string LastError => lastError;

        private void OnEnable()
        {
            if (listenOnEnable)
                StartListening(port);
        }

        private void OnDisable() => StopListening();

        private void OnApplicationQuit() => StopListening();

        public bool StartListening(int udpPort)
        {
            StopListening();
            port = udpPort;
            buffer.Clear();

            try
            {
                client = new UdpClient(new IPEndPoint(IPAddress.Any, port));
                client.Client.ReceiveTimeout = 100;
                running = true;
                thread = new Thread(ReceiveLoop) { IsBackground = true, Name = "SSRP UDP Reader" };
                thread.Start();
                return true;
            }
            catch (Exception e)
            {
                lastError = e.Message;
                Debug.LogWarning($"[UDP] Could not listen on port {port}: {e.Message}");
                StopListening();
                return false;
            }
        }

        public void StopListening()
        {
            running = false;

            UdpClient c = client;
            client = null;

            try { c?.Close(); }
            catch (Exception) { /* closing */ }

            Thread t = thread;
            thread = null;

            if (t != null && t.IsAlive && t != Thread.CurrentThread)
                t.Join(500);
        }

        private void ReceiveLoop()
        {
            UdpClient c = client;
            var remote = new IPEndPoint(IPAddress.Any, 0);

            while (running)
            {
                byte[] data;

                try
                {
                    data = c.Receive(ref remote);
                }
                catch (SocketException e)
                {
                    if (e.SocketErrorCode == SocketError.TimedOut)
                        continue;

                    if (running)
                        lastError = e.Message;

                    break;
                }
                catch (Exception e)
                {
                    if (running)
                        lastError = e.Message;

                    break;
                }

                // A datagram may hold one or several newline-separated lines.
                string text = Encoding.ASCII.GetString(data);
                foreach (string line in text.Split('\n'))
                {
                    switch (SerialLineParser.TryParse(line, out FsrFrame frame))
                    {
                        case ParseResult.Ok:
                            buffer.Write(frame);
                            break;
                        case ParseResult.Malformed:
                            buffer.CountParseError();
                            break;
                    }
                }
            }
        }

        private void Update() => buffer.UpdateRate();

        public bool TryGetLatest(int playerId, out RawFsrSample sample)
        {
            sample = default;
            return IsListening && buffer.TryGetPlayerSample(playerId, watchdogMs / 1000.0, out sample);
        }
    }
}
