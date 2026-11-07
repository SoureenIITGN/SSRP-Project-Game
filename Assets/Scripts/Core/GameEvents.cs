using System;
using UnityEngine;

namespace SSRP.Core
{
    /// <summary>
    /// Static event hub. Systems raise events here; listeners subscribe in OnEnable
    /// and unsubscribe in OnDisable. Nobody needs a reference to whoever raised it.
    ///
    /// Core change (reviewed with the team, see docs/ARCHITECTURE.md section 5):
    ///  - MatchStateChanged now carries the MatchPhase that was entered.
    ///  - TileDestroyed now carries the world position (for particles).
    ///  - New: ScoreChanged(playerId, score) raised by the GameManager after it updates the score.
    ///  - New: CountdownTick(secondsLeft) raised during the pre-serve countdown (3, 2, 1, then 0 = GO).
    ///  - New: ParrySucceeded(playerId) when a parry meets the ball (ParryTriggered = any parry press).
    /// </summary>
    public static class GameEvents
    {
        public static event Action<int> GoalScored;
        public static event Action<int, int> ScoreChanged;
        public static event Action BallHitPaddle;
        public static event Action BallHitWall;
        public static event Action BallHitTile;
        public static event Action<Vector2> TileDestroyed;
        public static event Action<MatchPhase> MatchStateChanged;
        public static event Action<int> CountdownTick;
        public static event Action<int> ParryTriggered;
        public static event Action<int> ParrySucceeded;
        public static event Action<int, bool> InputConnectionChanged;

        public static void RaiseGoalScored(int playerId) =>
            GoalScored?.Invoke(playerId);

        public static void RaiseScoreChanged(int playerId, int score) =>
            ScoreChanged?.Invoke(playerId, score);

        public static void RaiseBallHitPaddle() =>
            BallHitPaddle?.Invoke();

        public static void RaiseBallHitWall() =>
            BallHitWall?.Invoke();

        public static void RaiseBallHitTile() =>
            BallHitTile?.Invoke();

        public static void RaiseTileDestroyed(Vector2 worldPosition) =>
            TileDestroyed?.Invoke(worldPosition);

        public static void RaiseMatchStateChanged(MatchPhase phase) =>
            MatchStateChanged?.Invoke(phase);

        public static void RaiseCountdownTick(int secondsLeft) =>
            CountdownTick?.Invoke(secondsLeft);

        public static void RaiseParryTriggered(int playerId) =>
            ParryTriggered?.Invoke(playerId);

        /// <summary>A parry that actually met the ball (timed within the active window).</summary>
        public static void RaiseParrySucceeded(int playerId) =>
            ParrySucceeded?.Invoke(playerId);

        public static void RaiseInputConnectionChanged(int playerId, bool isConnected) =>
            InputConnectionChanged?.Invoke(playerId, isConnected);

        /// <summary>
        /// The project uses "Enter Play Mode Options" with domain reload disabled, so static
        /// state survives between Play presses. Clearing the events here prevents listeners
        /// from a previous session lingering.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        public static void ClearAllListeners()
        {
            GoalScored = null;
            ScoreChanged = null;
            BallHitPaddle = null;
            BallHitWall = null;
            BallHitTile = null;
            TileDestroyed = null;
            MatchStateChanged = null;
            CountdownTick = null;
            ParryTriggered = null;
            ParrySucceeded = null;
            InputConnectionChanged = null;
        }
    }
}
