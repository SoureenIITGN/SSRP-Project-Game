using UnityEngine;

namespace SSRP.Gameplay
{
    /// <summary>
    /// [B1] Layer names and indices (defined in ProjectSettings/TagManager.asset).
    /// Ball=6, Paddle=7, Wall=8, Tile=9, Goal=10. See docs/physics.md for the collision matrix.
    /// </summary>
    public static class PhysicsLayers
    {
        public const string BallName = "Ball";
        public const string PaddleName = "Paddle";
        public const string WallName = "Wall";
        public const string TileName = "Tile";
        public const string GoalName = "Goal";

        public static int Ball => LayerMask.NameToLayer(BallName);
        public static int Paddle => LayerMask.NameToLayer(PaddleName);
        public static int Wall => LayerMask.NameToLayer(WallName);
        public static int Tile => LayerMask.NameToLayer(TileName);
        public static int Goal => LayerMask.NameToLayer(GoalName);

        /// <summary>Assign a layer by name, falling back to Default if the layer is missing.</summary>
        public static void Assign(GameObject go, string layerName)
        {
            int layer = LayerMask.NameToLayer(layerName);
            go.layer = layer >= 0 ? layer : 0;
        }
    }
}
