using UnityEngine;
using SSRP.Core;

namespace SSRP.Arena
{
    /// <summary>
    /// [D4] Describes one arena as DATA. Adding an arena = a prefab + one of these assets + an entry
    /// in the ArenaRegistry. No script changes (see docs/EXTENDING.md).
    /// </summary>
    [CreateAssetMenu(fileName = "ArenaDefinition", menuName = "SSRP/Arena/Arena Definition")]
    public class ArenaDefinition : ScriptableObject
    {
        [Tooltip("Stable id stored in settings (lowercase, no spaces).")]
        public string id = "classic";

        public string displayName = "Classic";

        [TextArea] public string description = "";

        public Sprite thumbnail;

        [Tooltip("Prefab root must contain an ArenaAnchors component.")]
        public GameObject arenaPrefab;

        [Tooltip("Optional: replaces the match's BallConfig in this arena.")]
        public BallConfig ballConfigOverride;
    }
}
