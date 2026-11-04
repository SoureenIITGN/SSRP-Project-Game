
namespace SSRP.Core
{
    /// <summary>
    /// One raw reading from the player's two force-sensitive resistors.
    /// Up and Down are raw ADC counts, normally from 0 to 1023.
    /// Time is the sample timestamp in seconds.
    /// </summary>
    public struct RawFsrSample
    {
        public float Up;
        public float Down;
        public double Time;
    }
}
