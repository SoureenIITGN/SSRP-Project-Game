using UnityEngine;

namespace SSRP.Core
{
    public struct ArenaBounds
    {
        public Vector2 Min;
        public Vector2 Max;

        public ArenaBounds(Vector2 min, Vector2 max)
        {
            Min = min;
            Max = max;
        }
    }
}