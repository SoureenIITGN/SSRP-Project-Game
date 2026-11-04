using UnityEngine;
using SSRP.Core;

namespace SSRP.Gameplay
{
    /// <summary>[B4] Bounce pad: the ball bounces off and gets a temporary speed boost via the modifier stack.</summary>
    [CreateAssetMenu(fileName = "BouncePadBehaviour", menuName = "SSRP/Tiles/Behaviours/Bounce Pad")]
    public class BouncePadBehaviour : TileBehaviour
    {
        public const string ModifierKey = "tile.bounce-pad";

        [Min(1f)] public float speedMultiplier = 1.5f;
        [Min(0.05f)] public float duration = 1.2f;

        public override void OnBallHit(in TileHitContext context)
        {
            GameEvents.RaiseBallHitTile();
            context.Modifiers.Add(ModifierKey, new SpeedMultiplierModifier(speedMultiplier), duration);
        }
    }
}
