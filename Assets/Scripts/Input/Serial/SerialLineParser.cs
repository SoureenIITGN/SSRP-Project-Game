using System.Globalization;

namespace SSRP.Input
{
    /// <summary>One parsed protocol line: the Arduino timestamp and the 4 raw channels.</summary>
    public struct FsrFrame
    {
        public uint TimestampMicros;
        public float Ch0;
        public float Ch1;
        public float Ch2;
        public float Ch3;

        public float this[int channel]
        {
            get
            {
                switch (channel)
                {
                    case 0: return Ch0;
                    case 1: return Ch1;
                    case 2: return Ch2;
                    default: return Ch3;
                }
            }
        }
    }

    public enum ParseResult
    {
        Ok,
        Empty,
        Comment,
        Malformed
    }

    /// <summary>
    /// Parser for the SSRP-FSR v1 line format (docs/serial-protocol.md). Pure C#, thread-safe,
    /// shared by SerialInputSource and UdpInputSource and covered by EditMode tests.
    /// </summary>
    public static class SerialLineParser
    {
        public const int MaxAdc = 1023;

        public static ParseResult TryParse(string line, out FsrFrame frame)
        {
            frame = default;

            if (line == null)
                return ParseResult.Empty;

            line = line.Trim();

            if (line.Length == 0)
                return ParseResult.Empty;

            if (line[0] == '#')
                return ParseResult.Comment;

            string[] parts = line.Split(',');

            if (parts.Length != 5)
                return ParseResult.Malformed;

            if (!uint.TryParse(parts[0], NumberStyles.None, CultureInfo.InvariantCulture, out uint t))
                return ParseResult.Malformed;

            if (!TryParseChannel(parts[1], out frame.Ch0) ||
                !TryParseChannel(parts[2], out frame.Ch1) ||
                !TryParseChannel(parts[3], out frame.Ch2) ||
                !TryParseChannel(parts[4], out frame.Ch3))
            {
                frame = default;
                return ParseResult.Malformed;
            }

            frame.TimestampMicros = t;
            return ParseResult.Ok;
        }

        private static bool TryParseChannel(string text, out float value)
        {
            value = 0f;

            if (!int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out int v))
                return false;

            if (v < 0 || v > MaxAdc)
                return false;

            value = v;
            return true;
        }
    }
}
