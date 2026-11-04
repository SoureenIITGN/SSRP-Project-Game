using UnityEngine;
using SSRP.Core;

namespace SSRP.Flow
{
    /// <summary>
    /// [D1] Carries the runtime MatchConfig between scenes (Main Menu -> Game). It is always a
    /// copy (MatchConfig.CreateRuntimeCopy), so playing never modifies asset files.
    /// </summary>
    public static class MatchSession
    {
        private static MatchConfig config;

        public static MatchConfig Config
        {
            get
            {
                if (config == null)
                    config = CreateFromSettings();

                return config;
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => config = null;

        /// <summary>A fresh runtime config: catalog defaults + the player's saved choices.</summary>
        public static MatchConfig CreateFromSettings()
        {
            GameCatalog catalog = GameCatalog.Load();
            MatchConfig template = catalog != null ? catalog.defaultMatchConfig : null;

            MatchConfig runtime = template != null
                ? template.CreateRuntimeCopy()
                : ScriptableObject.CreateInstance<MatchConfig>();

            GameSettings.ApplyTo(runtime, catalog);
            return runtime;
        }
    }
}
