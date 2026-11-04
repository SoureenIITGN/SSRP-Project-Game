using UnityEngine;
using SSRP.Core;

namespace SSRP.Gameplay
{
    /// <summary>
    /// [B6] Extension proof: a Speed Zone tile. Paint it on a TRIGGER tilemap (the ball passes through);
    /// entering the zone adds a temporary speed-up modifier. New file only - no edits to
    /// BallController, TileHitResolver or Core.
    /// </summary>
    [CreateAssetMenu(fileName = "SpeedZoneBehaviour", menuName = "SSRP/Tiles/Behaviours/Speed Zone")]
    public class SpeedZoneBehaviour : TileBehaviour
    {
        public const string ModifierKey = "tile.speed-zone";

        [Min(1f)] public float speedMultiplier = 1.6f;
        [Min(0.05f)] public float duration = 1.5f;

        public override void OnBallHit(in TileHitContext context)
        {
            context.Modifiers.Add(ModifierKey, new SpeedMultiplierModifier(speedMultiplier), duration);
            GameEvents.RaiseBallHitTile();
        }
    }
}
