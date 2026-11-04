using UnityEngine;

namespace SSRP.Core
{
    public interface IPaddleInfo
    {
        int PlayerId { get; }
        float Height { get; }
        Vector2 Velocity { get; }
    }
}