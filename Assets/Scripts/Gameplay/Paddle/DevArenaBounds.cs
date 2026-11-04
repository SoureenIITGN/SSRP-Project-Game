using UnityEngine;
using SSRP.Core;

namespace SSRP.Gameplay
{
    /// <summary>Dev-scene helper: gives paddles placed by hand an ArenaBounds (no GameManager in dev scenes).</summary>
    public class DevArenaBounds : MonoBehaviour
    {
        [SerializeField] private Vector2 min = new Vector2(-9f, -5f);
        [SerializeField] private Vector2 max = new Vector2(9f, 5f);
        [SerializeField] private PaddleController[] paddles = new PaddleController[0];

        private void Start()
        {
            var bounds = new ArenaBounds(min, max);

            foreach (PaddleController paddle in paddles)
            {
                if (paddle == null)
                    continue;

                paddle.SetBounds(bounds);

                if (paddle.Slot != null)
                    paddle.SetColour(paddle.Slot.Colour);
            }
        }
    }
}
