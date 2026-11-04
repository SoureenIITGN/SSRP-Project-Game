
using UnityEngine;
using SSRP.Core;

namespace SSRP.Input
{
    /// <summary>
    /// Wraps ANY IPlayerInputSource and adds Gaussian noise and rare spikes, so filters
    /// can be tested visually without hardware (C1 optional task).
    /// </summary>
    public class NoiseInjector : MonoBehaviour, IPlayerInputSource
    {
        [Header("Input Source")]
        [SerializeField, Tooltip("Any component that implements IPlayerInputSource (Keyboard, Scripted, Serial...).")]
        //takes in an input source to add noise into
        private MonoBehaviour sourceComponent;

        [Header("Gaussian Noise")]
        [SerializeField]
        private bool enableGaussianNoise = true;

        [SerializeField, Min(0f)]
        private float gaussianStdDev = 10f;

        [Header("Rare Spikes")]
        [SerializeField]
        private bool enableSpikes = true;

        [SerializeField, Range(0f, 1f)]
        private float spikeProbability = 0.005f;

        [SerializeField, Min(0f)]
        private float spikeMagnitude = 200f;

        [Header("Debug")]
        [SerializeField]
        private bool showDebugGui;

        private IPlayerInputSource source;

        public bool IsConnected =>
            Source != null && Source.IsConnected;

        private IPlayerInputSource Source
        {
            get
            {
                if (source == null && sourceComponent != null)
                    source = sourceComponent as IPlayerInputSource;

                return source;
            }
        }

        /// <summary>Wrap a source from code (e.g. a test, or the input registry).</summary>
        public void SetSource(IPlayerInputSource inner)
        {
            source = inner;
            sourceComponent = inner as MonoBehaviour;
        }

        private void Awake()
        {
            if (sourceComponent != null && !(sourceComponent is IPlayerInputSource))
            {
                Debug.LogError(
                    "NoiseInjector: Assigned component does not implement IPlayerInputSource.",
                    this
                );
            }
        }

        private void OnValidate()
        {
            if (sourceComponent != null && !(sourceComponent is IPlayerInputSource))
            {
                Debug.LogWarning("NoiseInjector: the source must implement IPlayerInputSource.", this);
                sourceComponent = null;
            }

            source = null;
        }

        public bool TryGetLatest(
            int playerId, out RawFsrSample sample)
        {
            sample = default;

            IPlayerInputSource inner = Source;

            if (inner == null || ReferenceEquals(inner, this))
                return false;

            if (!inner.TryGetLatest(playerId, out sample))
                return false;

            float up = sample.Up;
            float down = sample.Down;

            if (enableGaussianNoise)
            {
                up += GenerateGaussianNoise();
                down += GenerateGaussianNoise();
            }

            if (enableSpikes)
            {
                up += GenerateSpike();
                down += GenerateSpike();
            }

            sample.Up = Mathf.Clamp(up, 0f, 1023f);
            sample.Down = Mathf.Clamp(down, 0f, 1023f);

            return true;
        }

        //Function for generating the gaussian noise
        private float GenerateGaussianNoise()
        {
            // Box-Muller transform:
            // Converts uniform random values into Gaussian values.

            float u1 = Mathf.Max(
                Random.value, 0.000001f);

            float u2 = Random.value;

            float standardNormal =
                Mathf.Sqrt(-2f * Mathf.Log(u1)) *
                Mathf.Cos(2f * Mathf.PI * u2);

            return standardNormal * gaussianStdDev;
        }

        //Function for generating random spikes to the input signal
        private float GenerateSpike()
        {
            if (Random.value < spikeProbability)
            {
                float direction =
                    Random.value < 0.5f ? -1f : 1f;

                return direction * spikeMagnitude;
            }

            return 0f;
        }

        private void OnGUI()
        {
            if (!showDebugGui)
                return;

            if (!TryGetLatest(1, out RawFsrSample sample))
            {
                GUI.Label(new Rect(10, 150, 400, 25), "No input source available");
                return;
            }

            GUI.Label(
                new Rect(10, 210, 500, 25),
                $"Noisy P1 Up: {sample.Up:F0} | Down: {sample.Down:F0}"
            );
        }
    }
}
