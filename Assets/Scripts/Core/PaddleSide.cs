namespace SSRP.Core
{
    /// <summary>Which side of the arena a player defends.</summary>
    public enum PaddleSide
    {
        Left,
        Right
    }

    public static class PaddleSideExtensions
    {
        /// <summary>-1 for Left, +1 for Right (the side's x direction).</summary>
        public static float Sign(this PaddleSide side) =>
            side == PaddleSide.Left ? -1f : 1f;

        public static PaddleSide Opposite(this PaddleSide side) =>
            side == PaddleSide.Left ? PaddleSide.Right : PaddleSide.Left;
    }
}
