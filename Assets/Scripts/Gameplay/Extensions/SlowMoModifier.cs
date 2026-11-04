using UnityEngine;
using SSRP.Core;

namespace SSRP.Gameplay
{
    /// <summary>[B6] Ball modifier: slows the ball down (multiplier &lt; 1). Added by the Slow-Mo pickup tile.</summary>
    public sealed class SlowMoModifier : IBallModifier
    {
        public const string Key = "powerup.slow-mo";

        public SlowMoModifier(float factor = 0.55f)
        {
            SpeedMultiplier = Mathf.Clamp(factor, 0.05f, 1f);
        }

        public float SpeedMultiplier { get; }
    }
}
