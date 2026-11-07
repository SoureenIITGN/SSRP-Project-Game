using UnityEngine;
using UnityEngine.InputSystem;
using SSRP.Arena;
using SSRP.Core;
using SSRP.Gameplay;
using SSRP.Input;

namespace SSRP.Flow
{
    public enum PauseReason
    {
        None,
        UserRequest,
        InputDisconnected
    }

    /// <summary>
    /// [D1] Match flow state machine for the Game scene.
    ///
    ///   (Calibration) -> Countdown -> Serving -> Playing -> PointScored -> Countdown ... -> MatchOver
    ///                     Paused can interrupt Countdown / Serving / Playing / PointScored.
    ///
    /// Every transition raises GameEvents.MatchStateChanged(phase); the HUD, flow UI, audio and
    /// effects only listen. This class never references UI classes.
    /// It is the composition root of the Game scene: it loads the arena, spawns the ball and the
    /// paddles from the runtime MatchConfig and wires the goal zones to the right players.
    /// </summary>
    [RequireComponent(typeof(MatchCsvRecorder))]
    public class GameManager : MonoBehaviour
    {
        [Header("Scene references")]
        [SerializeField] private ArenaLoader arenaLoader;
        [SerializeField] private PlayerSpawner playerSpawner;
        [SerializeField] private BallController ballPrefab;
        [SerializeField] private Transform ballRoot;
        [SerializeField] private ArenaCameraFitter cameraFitter;

        [Header("Development")]
        [SerializeField, Tooltip("Optional: use this config instead of the menu's (for testing the Game scene).")]
        private MatchConfig configOverride;

        [SerializeField, Min(0f), Tooltip("Tiles within this distance of the ball spawn are removed when the arena loads.")]
        private float ballSpawnClearRadius = 0.75f;

        [SerializeField, Tooltip("Ask for calibration when a player uses sensors and none is saved.")]
        private bool requireCalibrationForHardware = true;

        private readonly MatchState matchState = new MatchState();
        private ArenaAnchors anchors;
        private BallConfig ballConfig;
        private PaddleSide nextServeSide = PaddleSide.Left;
        private float timer;
        private int countdownValue;

        public static GameManager Instance { get; private set; }

        public MatchPhase Phase { get; private set; } = MatchPhase.Menu;
        public MatchPhase PhaseBeforePause { get; private set; } = MatchPhase.Playing;
        public PauseReason PauseReason { get; private set; } = PauseReason.None;
        public string PauseDetail { get; private set; } = "";
        public MatchConfig Config { get; private set; }
        public MatchState Score => matchState;
        public BallController Ball { get; private set; }
        public int WinnerId { get; private set; }
        public ArenaDefinition Arena => arenaLoader != null ? arenaLoader.Current : null;
        public PlayerSpawner Players => playerSpawner;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => Instance = null;

        private void Awake()
        {
            Instance = this;
            if (GetComponent<MatchCsvRecorder>() == null)
                gameObject.AddComponent<MatchCsvRecorder>();

            GameServices.Ensure();
            Config = configOverride != null ? configOverride.CreateRuntimeCopy() : MatchSession.Config;
        }

        private void OnEnable()
        {
            GameEvents.GoalScored += OnGoalScored;
            GameEvents.InputConnectionChanged += OnInputConnectionChanged;
        }

        private void OnDisable()
        {
            GameEvents.GoalScored -= OnGoalScored;
            GameEvents.InputConnectionChanged -= OnInputConnectionChanged;
        }

        private void OnDestroy()
        {
            Time.timeScale = 1f;

            if (Instance == this)
                Instance = null;
        }

        private void Start()
        {
            SetupMatch();
        }

        // ------------------------------------------------------------------ setup

        private void SetupMatch()
        {
            GameCatalog catalog = GameCatalog.Load();
            ArenaRegistry registry = catalog != null ? catalog.arenaRegistry : null;
            ArenaDefinition arena = registry != null ? registry.Get(Config.arenaId) : null;

            anchors = arenaLoader != null ? arenaLoader.Load(arena) : null;

            if (anchors == null)
            {
                Debug.LogError("GameManager: could not load an arena. Run SSRP > Build Project.", this);
                return;
            }

            ArenaBounds bounds = anchors.Bounds;

            // A ball that spawns inside tiles gets stuck: keep the spawn area clear in every arena.
            int cleared = arenaLoader.ClearTilesAround(anchors.BallSpawn, ballSpawnClearRadius);
            if (cleared > 0)
                Debug.LogWarning($"GameManager: removed {cleared} tile(s) covering the ball spawn in arena " +
                                 $"'{arena.displayName}'. Move them away from the spawn point in the prefab.", this);

            if (cameraFitter != null)
                cameraFitter.Fit(bounds, anchors.CameraMargin);

            ballConfig = arena.ballConfigOverride != null ? arena.ballConfigOverride : Config.ballConfig;

            Ball = Instantiate(ballPrefab, anchors.BallSpawn, Quaternion.identity, ballRoot);
            Ball.name = "Ball";
            Ball.Configure(ballConfig);
            Ball.SetSpawnPosition(anchors.BallSpawn);
            Ball.ResetToCenter();

            playerSpawner.Spawn(Config, bounds, anchors.GetPaddleSpawn);
            AssignGoals();
            ResetScores();

            nextServeSide = Random.value < 0.5f ? PaddleSide.Left : PaddleSide.Right;

            if (NeedsCalibration())
                Enter(MatchPhase.Calibration);
            else
                Enter(MatchPhase.Countdown);
        }

        /// <summary>The goal behind a side scores for the player on the opposite side.</summary>
        private void AssignGoals()
        {
            foreach (PaddleSide side in new[] { PaddleSide.Left, PaddleSide.Right })
            {
                GoalZone goal = anchors.GetGoal(side);

                if (goal == null)
                    continue;

                PlayerDefinition scorer = Config.GetPlayerOnSide(side.Opposite());
                goal.ScoringPlayerId = scorer != null ? scorer.playerId : 0;
            }
        }

        private void ResetScores()
        {
            var ids = new int[Config.players.Count];
            for (int i = 0; i < ids.Length; i++)
                ids[i] = Config.players[i].playerId;

            matchState.Reset(ids);
            WinnerId = 0;

            foreach (int id in ids)
            {
                PlayerSlot slot = playerSpawner.GetSlot(id);
                if (slot != null)
                    slot.Score = 0;

                GameEvents.RaiseScoreChanged(id, 0);
            }
        }

        private bool NeedsCalibration()
        {
            if (!requireCalibrationForHardware || CalibrationStore.HasSavedCalibration)
                return false;

            foreach (PlayerDefinition player in Config.players)
            {
                if (player.inputSource.IsHardware())
                    return true;
            }

            return false;
        }

        // ------------------------------------------------------------------ state machine

        private void Enter(MatchPhase phase)
        {
            Phase = phase;

            switch (phase)
            {
                case MatchPhase.Calibration:
                    Ball.Freeze();
                    break;

                case MatchPhase.Countdown:
                    Ball.ResetToCenter();
                    countdownValue = Mathf.CeilToInt(Config.countdownSeconds);
                    timer = 1f;
                    break;

                case MatchPhase.PointScored:
                    Ball.ResetToCenter();
                    timer = Config.pointScoredPause;
                    break;

                case MatchPhase.MatchOver:
                    Ball.ResetToCenter();
                    break;
            }

            GameEvents.RaiseMatchStateChanged(phase);

            // Follow-up transitions after listeners have seen the state.
            if (phase == MatchPhase.Countdown)
            {
                if (countdownValue <= 0)
                {
                    GameEvents.RaiseCountdownTick(0);
                    Enter(MatchPhase.Serving);
                }
                else
                {
                    GameEvents.RaiseCountdownTick(countdownValue);
                }
            }
            else if (phase == MatchPhase.Serving)
            {
                float range = ballConfig != null ? ballConfig.serveAngleRange : 25f;
                Ball.Serve(ServeMath.DirectionToward(nextServeSide, range, Random.value));
                Enter(MatchPhase.Playing);
            }
        }

        private void Update()
        {
            HandlePauseKeys();

            float dt = Time.unscaledDeltaTime;

            switch (Phase)
            {
                case MatchPhase.Countdown:
                    timer -= dt;
                    if (timer <= 0f)
                    {
                        countdownValue--;

                        if (countdownValue > 0)
                        {
                            timer += 1f;
                            GameEvents.RaiseCountdownTick(countdownValue);
                        }
                        else
                        {
                            GameEvents.RaiseCountdownTick(0);
                            Enter(MatchPhase.Serving);
                        }
                    }
                    break;

                case MatchPhase.PointScored:
                    timer -= dt;
                    if (timer <= 0f)
                    {
                        if (matchState.HasWinner(Config.targetScore, out int winner))
                        {
                            WinnerId = winner;
                            Enter(MatchPhase.MatchOver);
                        }
                        else
                        {
                            Enter(MatchPhase.Countdown);
                        }
                    }
                    break;
            }
        }

        private void HandlePauseKeys()
        {
            Keyboard keyboard = Keyboard.current;

            if (keyboard == null)
                return;

            if (keyboard.escapeKey.wasPressedThisFrame || keyboard.pKey.wasPressedThisFrame)
                TogglePause();
        }

        private static bool IsPausable(MatchPhase phase) =>
            phase == MatchPhase.Countdown || phase == MatchPhase.Serving ||
            phase == MatchPhase.Playing || phase == MatchPhase.PointScored;

        // ------------------------------------------------------------------ events

        private void OnGoalScored(int scoringPlayerId)
        {
            if (Phase != MatchPhase.Playing)
                return;

            int score = matchState.AddPoint(scoringPlayerId);

            PlayerSlot slot = playerSpawner.GetSlot(scoringPlayerId);
            if (slot != null)
                slot.Score = score;

            GameEvents.RaiseScoreChanged(scoringPlayerId, score);

            // Serve toward the player who conceded (the side opposite the scorer).
            PlayerDefinition scorer = Config.GetPlayer(scoringPlayerId);
            if (scorer != null)
                nextServeSide = scorer.side.Opposite();

            Enter(MatchPhase.PointScored);
        }

        private void OnInputConnectionChanged(int playerId, bool connected)
        {
            if (connected || !IsPausable(Phase))
                return;

            PlayerDefinition player = Config != null ? Config.GetPlayer(playerId) : null;

            if (player == null || !player.inputSource.IsHardware())
                return;

            Pause(PauseReason.InputDisconnected, $"{player.displayName}: sensors disconnected (no data for 250 ms).");
        }

        // ------------------------------------------------------------------ public API (used by UI)

        public void Pause(PauseReason reason, string detail = "")
        {
            if (!IsPausable(Phase))
                return;

            PhaseBeforePause = Phase;
            PauseReason = reason;
            PauseDetail = detail;
            Phase = MatchPhase.Paused;
            Time.timeScale = 0f;
            GameEvents.RaiseMatchStateChanged(MatchPhase.Paused);
        }

        public void Resume()
        {
            if (Phase != MatchPhase.Paused)
                return;

            Time.timeScale = 1f;
            PauseReason = PauseReason.None;
            PauseDetail = "";
            Phase = PhaseBeforePause;
            GameEvents.RaiseMatchStateChanged(Phase);
        }

        public void TogglePause()
        {
            if (Phase == MatchPhase.Paused)
                Resume();
            else
                Pause(PauseReason.UserRequest);
        }

        public void Rematch()
        {
            Time.timeScale = 1f;
            ResetScores();
            playerSpawner.ResetPositions(anchors.GetPaddleSpawn);
            nextServeSide = Random.value < 0.5f ? PaddleSide.Left : PaddleSide.Right;
            Enter(MatchPhase.Countdown);
        }

        public void ReturnToMenu()
        {
            Time.timeScale = 1f;
            Phase = MatchPhase.Menu;
            GameEvents.RaiseMatchStateChanged(MatchPhase.Menu);
            SceneLoader.Load(SceneNames.MainMenu);
        }

        /// <summary>Called by the calibration UI when it closes (calibrated or skipped).</summary>
        public void CompleteCalibration()
        {
            if (Phase == MatchPhase.Calibration)
                Enter(MatchPhase.Countdown);
        }

        public PlayerDefinition GetPlayer(int playerId) => Config != null ? Config.GetPlayer(playerId) : null;
    }
}
