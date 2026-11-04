using UnityEngine;

namespace SSRP.Gameplay
{
    /// <summary>
    /// [B4] Base class for tile behaviours. Behaviours are ScriptableObject ASSETS shared by every
    /// tile that uses them, so they must not store per-tile state; per-cell state (such as remaining
    /// hit points) lives in the TileHitResolver and is reached through the context.
    /// </summary>
    public abstract class TileBehaviour : ScriptableObject
    {
        public abstract void OnBallHit(in TileHitContext context);
    }
}
