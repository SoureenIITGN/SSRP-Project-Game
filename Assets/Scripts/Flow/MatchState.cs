using System.Collections.Generic;

namespace SSRP.Flow
{
    /// <summary>[D1] Plain C# score keeping (no Unity types, so it is trivially testable).</summary>
    public class MatchState
    {
        private readonly Dictionary<int, int> scores = new Dictionary<int, int>();
        private readonly List<int> playerIds = new List<int>();

        public IReadOnlyList<int> PlayerIds => playerIds;

        public int LastScorerId { get; private set; }

        public void Reset(IEnumerable<int> ids)
        {
            scores.Clear();
            playerIds.Clear();
            LastScorerId = 0;

            foreach (int id in ids)
            {
                if (scores.ContainsKey(id))
                    continue;

                scores[id] = 0;
                playerIds.Add(id);
            }
        }

        /// <summary>Adds one point and returns the new score.</summary>
        public int AddPoint(int playerId)
        {
            scores.TryGetValue(playerId, out int score);
            score++;
            scores[playerId] = score;

            if (!playerIds.Contains(playerId))
                playerIds.Add(playerId);

            LastScorerId = playerId;
            return score;
        }

        public int GetScore(int playerId) => scores.TryGetValue(playerId, out int s) ? s : 0;

        /// <summary>True when a player reached the target score (first-to-N rules).</summary>
        public bool HasWinner(int targetScore, out int winnerId)
        {
            foreach (int id in playerIds)
            {
                if (scores[id] >= targetScore)
                {
                    winnerId = id;
                    return true;
                }
            }

            winnerId = 0;
            return false;
        }
    }
}
