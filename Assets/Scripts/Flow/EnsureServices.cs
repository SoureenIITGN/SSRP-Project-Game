using UnityEngine;

namespace SSRP.Flow
{
    /// <summary>Dev-scene helper: makes sure the persistent services (input sources, audio) exist.</summary>
    [DefaultExecutionOrder(-1000)]
    public class EnsureServices : MonoBehaviour
    {
        private void Awake()
        {
            GameServices.Ensure();
        }
    }
}
