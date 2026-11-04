namespace SSRP.Core
{
    /// <summary>
    /// Which input device feeds a player. Moved here from SSRP.Input (C1) so that
    /// MatchConfig (Core) can store a per-player choice without Core depending on Input.
    /// The numeric values are unchanged so existing scenes keep their selection.
    /// </summary>
    public enum InputSourceType
    {
        Keyboard = 0,
        Scripted = 1,
        Serial = 2,
        NoiseInjected = 3,
        Udp = 4
    }

    public static class InputSourceTypeExtensions
    {
        /// <summary>True for real sensor sources (calibration applies, they can disconnect).</summary>
        public static bool IsHardware(this InputSourceType type) =>
            type == InputSourceType.Serial || type == InputSourceType.Udp;

        public static string DisplayName(this InputSourceType type)
        {
            switch (type)
            {
                case InputSourceType.Keyboard: return "Keyboard";
                case InputSourceType.Scripted: return "Scripted (test)";
                case InputSourceType.Serial: return "Arduino (USB)";
                case InputSourceType.NoiseInjected: return "Keyboard + noise";
                case InputSourceType.Udp: return "UDP (Python)";
                default: return type.ToString();
            }
        }
    }
}
