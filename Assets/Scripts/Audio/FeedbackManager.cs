using System.Collections;
using UnityEngine;
using SSRP.Core;
using SSRP.Flow;

namespace SSRP.Audio
{
    /// <summary>
    /// [D6] Game feel, driven only by GameEvents: screen shake (goal, tile destroyed), a short
    /// hit-stop on paddle hits (~40 ms) and a particle burst where a tile is destroyed.
    /// Each effect can be switched off in Match Setup (GameSettings). Put it on the Game camera.
    /// </summary>
    public class FeedbackManager : MonoBehaviour
    {
        [Header("Screen shake")]
        [SerializeField] private Transform shakeTarget;
        [SerializeField, Min(0f)] private float goalShakeAmplitude = 0.35f;
        [SerializeField, Min(0f)] private float tileShakeAmplitude = 0.12f;
        [SerializeField, Min(0.01f)] private float shakeDuration = 0.25f;

        [Header("Hit-stop")]
        [SerializeField, Min(0f)] private float hitStopSeconds = 0.04f;

        [Header("Particles")]
        [SerializeField] private ParticleSystem tileBurstPrefab;

        private Coroutine shakeRoutine;
        private Coroutine hitStopRoutine;
        private Vector3 shakeBase;
        private MatchPhase phase = MatchPhase.Playing;

        private void Awake()
        {
            if (shakeTarget == null)
                shakeTarget = transform;
        }

        private void OnEnable()
        {
            GameEvents.GoalScored += OnGoal;
            GameEvents.TileDestroyed += OnTileDestroyed;
            GameEvents.BallHitPaddle += OnPaddleHit;
            GameEvents.MatchStateChanged += OnPhase;
        }

        private void OnDisable()
        {
            GameEvents.GoalScored -= OnGoal;
            GameEvents.TileDestroyed -= OnTileDestroyed;
            GameEvents.BallHitPaddle -= OnPaddleHit;
            GameEvents.MatchStateChanged -= OnPhase;
        }

        private void OnPhase(MatchPhase newPhase) => phase = newPhase;

        private void OnGoal(int _) => Shake(goalShakeAmplitude);

        private void OnTileDestroyed(Vector2 position)
        {
            Shake(tileShakeAmplitude);

            if (GameSettings.Particles && tileBurstPrefab != null)
            {
                ParticleSystem burst = Instantiate(tileBurstPrefab, position, Quaternion.identity);
                burst.Play();
                Destroy(burst.gameObject, burst.main.duration + burst.main.startLifetime.constantMax + 0.1f);
            }
        }

        private void OnPaddleHit()
        {
            if (!GameSettings.HitStop || hitStopSeconds <= 0f || hitStopRoutine != null)
                return;

            if (!Mathf.Approximately(Time.timeScale, 1f))
                return;

            hitStopRoutine = StartCoroutine(HitStop());
        }

        private IEnumerator HitStop()
        {
            Time.timeScale = 0f;
            yield return new WaitForSecondsRealtime(hitStopSeconds);

            // Do not undo a pause (or the end of the match) that happened during the hit-stop.
            if (phase != MatchPhase.Paused && Mathf.Approximately(Time.timeScale, 0f))
                Time.timeScale = 1f;

            hitStopRoutine = null;
        }

        private void Shake(float amplitude)
        {
            if (!GameSettings.ScreenShake || amplitude <= 0f || shakeTarget == null)
                return;

            if (shakeRoutine != null)
            {
                StopCoroutine(shakeRoutine);
                shakeTarget.localPosition = shakeBase;
            }

            shakeRoutine = StartCoroutine(ShakeRoutine(amplitude));
        }

        private IEnumerator ShakeRoutine(float amplitude)
        {
            shakeBase = shakeTarget.localPosition;
            float t = 0f;

            while (t < shakeDuration)
            {
                t += Time.unscaledDeltaTime;
                float strength = amplitude * (1f - t / shakeDuration);
                Vector2 offset = Random.insideUnitCircle * strength;
                shakeTarget.localPosition = shakeBase + new Vector3(offset.x, offset.y, 0f);
                yield return null;
            }

            shakeTarget.localPosition = shakeBase;
            shakeRoutine = null;
        }

        private void OnDestroy()
        {
            if (hitStopRoutine != null && phase != MatchPhase.Paused)
                Time.timeScale = 1f;
        }
    }
}
