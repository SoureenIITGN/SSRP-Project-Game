using System.Collections.Generic;
using UnityEngine;
using SSRP.Arena;
using SSRP.Core;

namespace SSRP.Flow
{
    /// <summary>
    /// The one asset loaded from Resources (Assets/Resources/SSRP/GameCatalog.asset). It references
    /// the default MatchConfig, ball presets, arena registry and the persistent services prefab, so
    /// every scene can be played directly from the editor (Game, MainMenu or a dev scene).
    /// </summary>
    [CreateAssetMenu(fileName = "GameCatalog", menuName = "SSRP/Game Catalog")]
    public class GameCatalog : ScriptableObject
    {
        public const string ResourcePath = "SSRP/GameCatalog";

        public MatchConfig defaultMatchConfig;

        [Tooltip("Ball speed presets offered in Match Setup (BallConfig variants).")]
        public List<BallConfig> ballPresets = new List<BallConfig>();

        public ArenaRegistry arenaRegistry;

        [Tooltip("Prefab with GameServices: scene loader, input sources, serial manager, audio.")]
        public GameObject servicesPrefab;

        [Tooltip("Target-score choices offered in Match Setup.")]
        public List<int> targetScoreOptions = new List<int> { 5, 7, 11 };

        private static GameCatalog cached;

        public static GameCatalog Load()
        {
            if (cached == null)
            {
                cached = Resources.Load<GameCatalog>(ResourcePath);

                if (cached == null)
                    Debug.LogWarning("GameCatalog not found at Resources/" + ResourcePath +
                                     ". Run SSRP > Build Project in the editor.");
            }

            return cached;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => cached = null;

        public int IndexOfBallPreset(BallConfig config) => config != null ? ballPresets.IndexOf(config) : -1;
    }
}
