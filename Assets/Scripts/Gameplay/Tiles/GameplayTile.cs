using UnityEngine;
using UnityEngine.Tilemaps;

namespace SSRP.Gameplay
{
    /// <summary>
    /// [B4] A tile asset with behaviour. Paint it with the Tile Palette like any Tile.
    /// The behaviour (a ScriptableObject) decides what happens when the ball hits it, so a new
    /// tile type = a new TileBehaviour class + assets; TileHitResolver never changes.
    /// </summary>
    [CreateAssetMenu(fileName = "GameplayTile", menuName = "SSRP/Tiles/Gameplay Tile")]
    public class GameplayTile : Tile
    {
        [Tooltip("What happens when the ball hits this tile. Empty = plain solid tile.")]
        public TileBehaviour behaviour;

        [Min(1), Tooltip("Hits needed to destroy it (used by BreakableBehaviour).")]
        public int hitPoints = 1;
    }
}
