using UnityEngine;
using SSRP.Core;

namespace SSRP.Audio
{
    /// <summary>
    /// [D6] Plays a sound for every game event. It only listens to GameEvents, so any new feature
    /// gets sound by raising an event. Lives on the persistent services object. Master volume is
    /// AudioListener.volume (set from Match Setup).
    /// </summary>
    public class AudioManager : MonoBehaviour
    {
        [SerializeField] private AudioConfig config;
        [SerializeField, Range(1, 16)] private int voices = 8;

        private AudioSource[] sources = new AudioSource[0];
        private int nextSource;

        public AudioConfig Config => config;

        private void Awake()
        {
            sources = new AudioSource[voices];

            for (int i = 0; i < voices; i++)
            {
                AudioSource s = gameObject.AddComponent<AudioSource>();
                s.playOnAwake = false;
                s.spatialBlend = 0f;
                sources[i] = s;
            }
        }

        private void OnEnable()
        {
            GameEvents.BallHitPaddle += OnPaddleHit;
            GameEvents.BallHitWall += OnWallHit;
            GameEvents.BallHitTile += OnTileHit;
            GameEvents.TileDestroyed += OnTileDestroyed;
            GameEvents.GoalScored += OnGoal;
            GameEvents.ParryTriggered += OnParry;
            GameEvents.ParrySucceeded += OnParrySucceeded;
            GameEvents.CountdownTick += OnCountdown;
            GameEvents.MatchStateChanged += OnPhase;
        }

        private void OnDisable()
        {
            GameEvents.BallHitPaddle -= OnPaddleHit;
            GameEvents.BallHitWall -= OnWallHit;
            GameEvents.BallHitTile -= OnTileHit;
            GameEvents.TileDestroyed -= OnTileDestroyed;
            GameEvents.GoalScored -= OnGoal;
            GameEvents.ParryTriggered -= OnParry;
            GameEvents.ParrySucceeded -= OnParrySucceeded;
            GameEvents.CountdownTick -= OnCountdown;
            GameEvents.MatchStateChanged -= OnPhase;
        }

        public void SetConfig(AudioConfig audioConfig) => config = audioConfig;

        private void OnPaddleHit() => Play(config != null ? config.paddleHit : null);
        private void OnWallHit() => Play(config != null ? config.wallHit : null, 0.7f);
        private void OnTileHit() => Play(config != null ? config.tileHit : null, 0.8f);
        private void OnTileDestroyed(Vector2 _) => Play(config != null ? config.tileBreak : null);
        private void OnGoal(int _) => Play(config != null ? config.goal : null);
        private void OnParry(int _) => Play(config != null ? config.parry : null, 0.45f);
        private void OnParrySucceeded(int _) => Play(config != null ? config.parrySuccess : null, 1f, false);

        private void OnCountdown(int secondsLeft)
        {
            if (config == null)
                return;

            Play(secondsLeft > 0 ? config.countdownTick : config.countdownGo, 1f, false);
        }

        private void OnPhase(MatchPhase phase)
        {
            if (phase == MatchPhase.MatchOver && config != null)
                Play(config.matchOver, 1f, false);
        }

        /// <summary>Play any clip through the pool (also usable by UI for clicks).</summary>
        public void Play(AudioClip clip, float volume = 1f, bool varyPitch = true)
        {
            if (clip == null || sources.Length == 0)
                return;

            AudioSource source = sources[nextSource];
            nextSource = (nextSource + 1) % sources.Length;

            float variation = config != null ? config.pitchVariation : 0f;
            source.pitch = varyPitch ? 1f + Random.Range(-variation, variation) : 1f;
            source.PlayOneShot(clip, volume * (config != null ? config.effectsVolume : 1f));
        }
    }
}
