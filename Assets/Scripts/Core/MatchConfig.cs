using System.Collections.Generic;
using UnityEngine;

namespace SSRP.Core
{
    /// <summary>
    /// Everything that defines one match. The Match Setup screen edits a *runtime copy*
    /// (see <see cref="CreateRuntimeCopy"/>), never the asset itself: editing a
    /// ScriptableObject asset in Play Mode writes the change to disk.
    /// </summary>
    [CreateAssetMenu(fileName = "MatchConfig", menuName = "SSRP/Configs/Match Config")]
    public class MatchConfig : ScriptableObject
    {
        [Min(1)]
        public int targetScore = 5;

        [Tooltip("Ball tuning used unless the arena overrides it.")]
        public BallConfig ballConfig;

        public PaddleConfig paddleConfig;

        [Tooltip("ArenaDefinition.id of the arena to load.")]
        public string arenaId = "classic";

        [Min(0f)]
        public float countdownSeconds = 3f;

        [Min(0f)]
        public float pointScoredPause = 1f;

        [Tooltip("One entry per player. Data-driven: nothing in code assumes 'Player1/Player2'.")]
        public List<PlayerDefinition> players = new List<PlayerDefinition>
        {
            new PlayerDefinition
            {
                playerId = 1, displayName = "Player 1",
                colour = new Color(0.25f, 0.8f, 1f), side = PaddleSide.Left
            },
            new PlayerDefinition
            {
                playerId = 2, displayName = "Player 2",
                colour = new Color(1f, 0.45f, 0.3f), side = PaddleSide.Right
            }
        };

        public PlayerDefinition GetPlayer(int playerId)
        {
            for (int i = 0; i < players.Count; i++)
            {
                if (players[i].playerId == playerId)
                    return players[i];
            }

            return null;
        }

        public PlayerDefinition GetPlayerOnSide(PaddleSide side)
        {
            for (int i = 0; i < players.Count; i++)
            {
                if (players[i].side == side)
                    return players[i];
            }

            return null;
        }

        /// <summary>A copy that is safe to edit at runtime (asset files stay untouched).</summary>
        public MatchConfig CreateRuntimeCopy()
        {
            MatchConfig copy = Instantiate(this);
            copy.name = name + " (Runtime)";
            return copy;
        }
    }
}
