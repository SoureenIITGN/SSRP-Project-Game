using UnityEngine;
using SSRP.Core;

namespace SSRP.Gameplay
{
    /// <summary>[B4] Plain wall tile: the ball bounces (physics does it), we only raise the event.</summary>
    [CreateAssetMenu(fileName = "SolidBehaviour", menuName = "SSRP/Tiles/Behaviours/Solid")]
    public class SolidBehaviour : TileBehaviour
    {
        public override void OnBallHit(in TileHitContext context)
        {
            GameEvents.RaiseBallHitTile();
        }
    }
}
