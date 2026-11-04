using System.Threading;

namespace SSRP.Input
{
    /// <summary>
    /// Thread-safe "latest sample" mailbox used by background reader threads (serial, UDP).
    /// The reader thread calls <see cref="Write"/>; the main thread calls <see cref="TryRead"/>.
    /// Only the newest frame is kept: the game needs the current pressure, not a backlog.
    /// </summary>
    public sealed class LatestFrameBuffer
    {
        private readonly object gate = new object();
        private FsrFrame latest;
        private double latestTime;
        private bool hasFrame;

        private long packetCount;
        private long parseErrors;

        // Main-thread rate meter.
        private long rateLastCount;
        private double rateLastTime = -1;

        public long PacketCount => Interlocked.Read(ref packetCount);

        public long ParseErrors => Interlocked.Read(ref parseErrors);

        /// <summary>Packets per second, updated about once a second by <see cref="UpdateRate"/>.</summary>
        public float PacketsPerSecond { get; private set; }

        /// <summary>Time (InputClock) of the newest frame, or -1 if none yet.</summary>
        public double LatestTime
        {
            get { lock (gate) { return hasFrame ? latestTime : -1; } }
        }

        public double Age
        {
            get
            {
                double t = LatestTime;
                return t < 0 ? double.PositiveInfinity : InputClock.Now - t;
            }
        }

        public void Write(in FsrFrame frame)
        {
            double now = InputClock.Now;

            lock (gate)
            {
                latest = frame;
                latestTime = now;
                hasFrame = true;
            }

            Interlocked.Increment(ref packetCount);
        }

        public void CountParseError() => Interlocked.Increment(ref parseErrors);

        public bool TryRead(out FsrFrame frame, out double time)
        {
            lock (gate)
            {
                frame = latest;
                time = latestTime;
                return hasFrame;
            }
        }

        public void Clear()
        {
            lock (gate)
            {
                hasFrame = false;
                latest = default;
            }

            Interlocked.Exchange(ref packetCount, 0);
            Interlocked.Exchange(ref parseErrors, 0);
            rateLastCount = 0;
            rateLastTime = -1;
            PacketsPerSecond = 0f;
        }

        /// <summary>Call from the main thread every frame.</summary>
        public void UpdateRate()
        {
            double now = InputClock.Now;

            if (rateLastTime < 0)
            {
                rateLastTime = now;
                rateLastCount = PacketCount;
                return;
            }

            double elapsed = now - rateLastTime;

            if (elapsed < 1.0)
                return;

            long count = PacketCount;
            PacketsPerSecond = (float)((count - rateLastCount) / elapsed);
            rateLastCount = count;
            rateLastTime = now;
        }

        /// <summary>
        /// Player 1 = channels 0 (Up) and 1 (Down); player 2 = channels 2 and 3.
        /// Returns false when there is no frame or it is older than the watchdog.
        /// </summary>
        public bool TryGetPlayerSample(int playerId, double watchdogSeconds, out SSRP.Core.RawFsrSample sample)
        {
            sample = default;

            if (playerId != 1 && playerId != 2)
                return false;

            if (!TryRead(out FsrFrame frame, out double time))
                return false;

            if (InputClock.Now - time > watchdogSeconds)
                return false;

            sample = playerId == 1
                ? new SSRP.Core.RawFsrSample { Up = frame.Ch0, Down = frame.Ch1, Time = time }
                : new SSRP.Core.RawFsrSample { Up = frame.Ch2, Down = frame.Ch3, Time = time };

            return true;
        }
    }
}
