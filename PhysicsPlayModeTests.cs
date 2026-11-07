using UnityEngine;

namespace SSRP.Audio
{
    /// <summary>[D6] Maps game events to sound clips. All clips are listed in docs/CREDITS.md.</summary>
    [CreateAssetMenu(fileName = "AudioConfig", menuName = "SSRP/Configs/Audio Config")]
    public class AudioConfig : ScriptableObject
    {
        public AudioClip paddleHit;
        public AudioClip wallHit;
        public AudioClip tileHit;
        public AudioClip tileBreak;
        public AudioClip goal;
        [Tooltip("Any parry press (quiet swish).")]
        public AudioClip parry;
        [Tooltip("Parry that met the ball (big clang).")]
        public AudioClip parrySuccess;
        public AudioClip countdownTick;
        public AudioClip countdownGo;
        public AudioClip matchOver;

        [Range(0f, 1f)] public float effectsVolume = 0.8f;

        [Range(0f, 0.3f), Tooltip("Random pitch variation so repeated hits do not sound identical.")]
        public float pitchVariation = 0.06f;
    }
}
