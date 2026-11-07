using System.Collections.Generic;
using UnityEngine;
using SSRP.Core;

namespace SSRP.Input
{
    /// <summary>
    /// [C2] The single place where raw sensor data becomes game input (one per player slot):
    ///
    ///   IPlayerInputSource (raw 0-1023) -> normalise (calibration) -> filter chain -> deadzone
    ///   -> PlayerInputState (Up, Down in the configured range; Velocity = Up - Down; Sum = Up + Down)
    ///
    /// Stale-data safety: if the source reports nothing, or the newest sample is older than
    /// <see cref="staleTimeout"/>, the output is zero (never hold the last high value).
    /// Raises GameEvents.InputConnectionChanged(playerId, connected) whenever that changes.
    /// </summary>
    [DefaultExecutionOrder(-50)]
    public class PlayerInputPipeline : MonoBehaviour
    {
        private const float MaxAdc = 1023f;

        [SerializeField]
        private PlayerSlot slot;

        [SerializeField, Tooltip("Deadzone is read from here. Optional.")]
        private PaddleConfig paddleConfig;

        [SerializeField, Tooltip("Applied to the Up and Down channels independently, in order.")]
        private List<FilterStage> filterChain = new List<FilterStage> { new FilterStage() };

        [SerializeField, Min(0.01f), Tooltip("No new sample for longer than this = output zeros.")]
        private float staleTimeout = 0.25f;

        private static readonly List<PlayerInputPipeline> ActivePipelines = new List<PlayerInputPipeline>();

        private ISignalFilter[] upFilters = new ISignalFilter[0];
        private ISignalFilter[] downFilters = new ISignalFilter[0];
        private bool filtersDirty = true;
        private bool? lastConnected;

        /// <summary>All enabled pipelines (read by the HUD and debug overlay, no Find* needed).</summary>
        public static IReadOnlyList<PlayerInputPipeline> Active => ActivePipelines;

        public PlayerSlot Slot => slot;
        public int PlayerId => slot != null ? slot.PlayerId : 0;

        /// <summary>Final output used by the paddle and abilities.</summary>
        public PlayerInputState State { get; private set; }

        public RawFsrSample LastRawSample { get; private set; }

        /// <summary>Normalised (0-1) values before filtering - useful to see what the filter does.</summary>
        public float NormalisedUp { get; private set; }
        public float NormalisedDown { get; private set; }

        /// <summary>Seconds since the newest sample was taken (infinity if none).</summary>
        public double SampleAge { get; private set; } = double.PositiveInfinity;

        public bool IsConnected => lastConnected ?? false;

        public IReadOnlyList<FilterStage> FilterChain => filterChain;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => ActivePipelines.Clear();

        private void Awake()
        {
            if (slot == null)
                slot = GetComponent<PlayerSlot>();
        }

        private void OnEnable()
        {
            ActivePipelines.Add(this);
            lastConnected = null;
            filtersDirty = true;
        }

        private void OnDisable()
        {
            ActivePipelines.Remove(this);
        }

        private void OnValidate()
        {
            // Changing the filter type in the inspector during Play rebuilds the chain.
            filtersDirty = true;
        }

        public void Configure(PlayerSlot playerSlot, PaddleConfig config)
        {
            slot = playerSlot;
            paddleConfig = config;
        }

        /// <summary>Replace the filter chain from code (e.g. a settings screen or a test).</summary>
        public void SetFilterChain(IEnumerable<FilterStage> stages)
        {
            filterChain = new List<FilterStage>(stages);
            filtersDirty = true;
        }

        private void BuildFilters()
        {
            int n = filterChain != null ? filterChain.Count : 0;
            upFilters = new ISignalFilter[n];
            downFilters = new ISignalFilter[n];

            for (int i = 0; i < n; i++)
            {
                upFilters[i] = FilterFactory.Create(filterChain[i]);
                downFilters[i] = FilterFactory.Create(filterChain[i]);
            }

            filtersDirty = false;
        }

        private void ResetFilters()
        {
            for (int i = 0; i < upFilters.Length; i++)
            {
                upFilters[i].Reset();
                downFilters[i].Reset();
            }
        }

        private void Update()
        {
            Tick(Time.deltaTime);
        }

        /// <summary>Run one pipeline step. Public so tests can drive it deterministically.</summary>
        public void Tick(float dt)
        {
            if (filtersDirty)
                BuildFilters();

            bool got = false;
            RawFsrSample sample = default;

            if (slot != null)
                got = slot.TryGetLatest(out sample);

            SampleAge = got ? InputClock.Now - sample.Time : double.PositiveInfinity;

            bool connected = got && slot.IsConnected && SampleAge <= staleTimeout;
            PublishConnection(connected);

            if (!connected)
            {
                // Watchdog / stale data: zero output and forget filter history.
                State = default;
                NormalisedUp = 0f;
                NormalisedDown = 0f;
                ResetFilters();
                return;
            }

            LastRawSample = sample;

            float up = Normalise(sample.Up, UpChannel);
            float down = Normalise(sample.Down, DownChannel);
            NormalisedUp = up;
            NormalisedDown = down;

            if (dt > 0f)
            {
                for (int i = 0; i < upFilters.Length; i++)
                {
                    up = upFilters[i].Process(up, dt);
                    down = downFilters[i].Process(down, dt);
                }

                float deadzone = paddleConfig != null ? paddleConfig.deadzone : 0f;

                State = new PlayerInputState
                {
                    Up = ApplyDeadzone(Mathf.Clamp01(up), deadzone),
                    Down = ApplyDeadzone(Mathf.Clamp01(down), deadzone)
                };
            }
        }

        private void PublishConnection(bool connected)
        {
            if (lastConnected.HasValue && lastConnected.Value == connected)
                return;

            lastConnected = connected;

            if (slot != null)
                GameEvents.RaiseInputConnectionChanged(slot.PlayerId, connected);
        }

        // Player 1 uses channels 0 (Up) and 1 (Down); player 2 uses 2 and 3.
        private int UpChannel => (Mathf.Max(1, PlayerId) - 1) * 2;
        private int DownChannel => UpChannel + 1;

        /// <summary>
        /// Maps the calibrated firm-grip point to the configured upper limit. Values above that
        /// point continue toward 1 at the configured maximum force.
        /// </summary>
        private float Normalise(float raw, int channel)
        {
            GetRange(channel, out float baseline, out float max);
            float span = max - baseline;

            if (span <= 0.0001f)
                return 0f;

            float upperLimit = paddleConfig != null
                ? Mathf.Clamp01(paddleConfig.normalizationUpperLimit)
                : 0.75f;
            float forceMax = paddleConfig != null
                ? Mathf.Clamp(paddleConfig.normalizationForceMax, 0f, MaxAdc)
                : MaxAdc;

            if (raw <= baseline)
                return 0f;

            if (raw <= max)
                return Mathf.Lerp(0f, upperLimit, (raw - baseline) / span);

            // The configured force maximum cannot be below the calibrated firm-grip point.
            forceMax = Mathf.Max(forceMax, max);
            float forceHeadroom = forceMax - max;
            if (forceHeadroom <= 0.0001f)
                return raw >= forceMax ? 1f : upperLimit;

            float aboveFirmGrip = Mathf.Clamp01((raw - max) / forceHeadroom);
            return Mathf.Lerp(upperLimit, 1f, aboveFirmGrip);
        }

        /// <summary>
        /// Calibration ([A4]) applies to real sensors only (Serial / UDP). Keyboard and scripted
        /// sources already produce exact 0-1023 values, so they use the default range.
        /// </summary>
        private void GetRange(int channel, out float baseline, out float max)
        {
            baseline = 0f;
            max = MaxAdc;

            if (slot == null || !slot.InputSourceType.IsHardware())
                return;

            if (CalibrationStore.Current.TryGetChannel(channel, out ChannelCalibration c))
            {
                baseline = c.baseline;
                max = c.max;
            }
        }

        /// <summary>Values below the deadzone become 0; the rest is rescaled so 1 stays 1.</summary>
        public static float ApplyDeadzone(float value, float deadzone)
        {
            if (deadzone <= 0f)
                return value;

            if (value <= deadzone)
                return 0f;

            return (value - deadzone) / (1f - deadzone);
        }
    }
}
