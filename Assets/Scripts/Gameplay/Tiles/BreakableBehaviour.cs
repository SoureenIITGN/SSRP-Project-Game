using UnityEngine;
using SSRP.Core;

namespace SSRP.Gameplay
{
    /// <summary>
    /// [B4] Breakable tile. Each hit removes one hit point (GameplayTile.hitPoints to start).
    /// With one hit point left it swaps to <see cref="damagedTile"/> (crack sprite) if set;
    /// at zero it is removed. Raises BallHitTile on every hit and TileDestroyed(position) when removed.
    /// </summary>
    [CreateAssetMenu(fileName = "BreakableBehaviour", menuName = "SSRP/Tiles/Behaviours/Breakable")]
    public class BreakableBehaviour : TileBehaviour
    {
        [Tooltip("Optional tile shown once only one hit point remains (e.g. cracked sprite).")]
        public GameplayTile damagedTile;

        public override void OnBallHit(in TileHitContext context)
        {
            GameEvents.RaiseBallHitTile();

            TileHitResolver resolver = context.Resolver;
            int remaining = resolver.GetHitPoints(context.Cell, context.Tile) - 1;

            if (remaining <= 0)
            {
                Vector2 position = context.CellCentre;
                resolver.RemoveTile(context.Cell);
                GameEvents.RaiseTileDestroyed(position);
                return;
            }

            resolver.SetHitPoints(context.Cell, remaining);

            if (remaining == 1 && damagedTile != null && context.Tile != damagedTile)
                resolver.ReplaceTile(context.Cell, damagedTile);
        }
    }
}
