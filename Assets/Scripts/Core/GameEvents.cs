using System;

namespace SSRP.Core
{
    public static class GameEvents
    {
        public static event Action<int> GoalScored;
        public static event Action BallHitPaddle;
        public static event Action BallHitWall;
        public static event Action BallHitTile;
        public static event Action TileDestroyed;
        public static event Action MatchStateChanged;
        public static event Action<int> ParryTriggered;
        public static event Action<int, bool> InputConnectionChanged;

        public static void RaiseGoalScored(int playerId) =>
            GoalScored?.Invoke(playerId);

        public static void RaiseBallHitPaddle() =>
            BallHitPaddle?.Invoke();

        public static void RaiseBallHitWall() =>
            BallHitWall?.Invoke();

        public static void RaiseBallHitTile() =>
            BallHitTile?.Invoke();

        public static void RaiseTileDestroyed() =>
            TileDestroyed?.Invoke();

        public static void RaiseMatchStateChanged() =>
            MatchStateChanged?.Invoke();

        public static void RaiseParryTriggered(int playerId) =>
            ParryTriggered?.Invoke(playerId);

        public static void RaiseInputConnectionChanged(int playerId, bool isConnected) =>
            InputConnectionChanged?.Invoke(playerId, isConnected);
    }
}