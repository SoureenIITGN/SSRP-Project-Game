using UnityEngine;
using SSRP.Core;

namespace SSRP.Gameplay
{
    /// <summary>
    /// [B6] Extension proof: a Slow-Mo power-up tile. Paint it on a TRIGGER tilemap; when the ball passes
    /// through, the pickup disappears and the ball gets a SlowMoModifier for a few seconds.
    /// New file only - no edits to BallController, TileHitResolver or Core.
    /// </summary>
    [CreateAssetMenu(fileName = "SlowMoPickupBehaviour", menuName = "SSRP/Tiles/Behaviours/Slow-Mo Pickup")]
    public class SlowMoPickupBehaviour : TileBehaviour
    {
        [Range(0.05f, 1f)] public float slowFactor = 0.55f;
        [Min(0.1f)] public float duration = 3f;

        public override void OnBallHit(in TileHitContext context)
        {
            context.Modifiers.Add(SlowMoModifier.Key, new SlowMoModifier(slowFactor), duration);

            Vector2 position = context.CellCentre;
            context.Resolver.RemoveTile(context.Cell);
            GameEvents.RaiseTileDestroyed(position);
        }
    }
}
