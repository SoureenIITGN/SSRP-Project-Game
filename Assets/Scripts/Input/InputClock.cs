using System.Diagnostics;

namespace SSRP.Input
{
    /// <summary>
    /// One monotonic clock (seconds) shared by every input source, the pipeline and the
    /// background serial/UDP threads. Unity's Time API may only be used on the main thread
    /// and stops when Time.timeScale = 0, so sample timestamps use this clock instead.
    /// RawFsrSample.Time is always expressed on this clock.
    /// </summary>
    public static class InputClock
    {
        private static readonly Stopwatch Watch = Stopwatch.StartNew();

        public static double Now => Watch.Elapsed.TotalSeconds;
    }
}
