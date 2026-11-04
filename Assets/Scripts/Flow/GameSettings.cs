using UnityEngine;
using SSRP.Core;

namespace SSRP.Flow
{
    /// <summary>
    /// [D3] Player choices persisted with PlayerPrefs (they survive a restart). Match options are
    /// copied into the runtime MatchConfig; ScriptableObject assets are never modified.
    /// </summary>
    public static class GameSettings
    {
        private const string Prefix = "ssrp.settings.";
        private const string TargetScoreKey = Prefix + "targetScore";
        private const string BallPresetKey = Prefix + "ballPreset";
        private const string ArenaKey = Prefix + "arenaId";
        private const string InputKeyPrefix = Prefix + "input.p";
        private const string VolumeKey = Prefix + "volume";
        private const string ShakeKey = Prefix + "fx.shake";
        private const string HitStopKey = Prefix + "fx.hitStop";
        private const string ParticlesKey = Prefix + "fx.particles";

        public static float Volume
        {
            get => PlayerPrefs.GetFloat(VolumeKey, 0.8f);
            set
            {
                PlayerPrefs.SetFloat(VolumeKey, Mathf.Clamp01(value));
                AudioListener.volume = Mathf.Clamp01(value);
            }
        }

        public static bool ScreenShake
        {
            get => PlayerPrefs.GetInt(ShakeKey, 1) != 0;
            set => PlayerPrefs.SetInt(ShakeKey, value ? 1 : 0);
        }

        public static bool HitStop
        {
            get => PlayerPrefs.GetInt(HitStopKey, 1) != 0;
            set => PlayerPrefs.SetInt(HitStopKey, value ? 1 : 0);
        }

        public static bool Particles
        {
            get => PlayerPrefs.GetInt(ParticlesKey, 1) != 0;
            set => PlayerPrefs.SetInt(ParticlesKey, value ? 1 : 0);
        }

        /// <summary>Apply stored choices to a runtime MatchConfig.</summary>
        public static void ApplyTo(MatchConfig config, GameCatalog catalog)
        {
            if (config == null)
                return;

            if (PlayerPrefs.HasKey(TargetScoreKey))
                config.targetScore = Mathf.Max(1, PlayerPrefs.GetInt(TargetScoreKey));

            if (catalog != null && PlayerPrefs.HasKey(BallPresetKey))
            {
                int index = PlayerPrefs.GetInt(BallPresetKey);

                if (index >= 0 && index < catalog.ballPresets.Count && catalog.ballPresets[index] != null)
                    config.ballConfig = catalog.ballPresets[index];
            }

            if (PlayerPrefs.HasKey(ArenaKey))
                config.arenaId = PlayerPrefs.GetString(ArenaKey);

            foreach (PlayerDefinition player in config.players)
            {
                string key = InputKeyPrefix + player.playerId;

                if (PlayerPrefs.HasKey(key))
                    player.inputSource = (InputSourceType)PlayerPrefs.GetInt(key);
            }
        }

        /// <summary>Store the match options of a runtime MatchConfig.</summary>
        public static void SaveFrom(MatchConfig config, GameCatalog catalog)
        {
            if (config == null)
                return;

            PlayerPrefs.SetInt(TargetScoreKey, config.targetScore);

            if (catalog != null)
            {
                int index = catalog.IndexOfBallPreset(config.ballConfig);
                if (index >= 0)
                    PlayerPrefs.SetInt(BallPresetKey, index);
            }

            PlayerPrefs.SetString(ArenaKey, config.arenaId ?? "");

            foreach (PlayerDefinition player in config.players)
                PlayerPrefs.SetInt(InputKeyPrefix + player.playerId, (int)player.inputSource);

            PlayerPrefs.Save();
        }

        public static void Save() => PlayerPrefs.Save();
    }
}
