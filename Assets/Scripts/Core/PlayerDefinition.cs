using System;
using UnityEngine;

namespace SSRP.Core
{
    /// <summary>
    /// Data describing one player slot in a match. MatchConfig holds a list of these,
    /// so the number of players, their names, colours and sides are data, not code.
    /// </summary>
    [Serializable]
    public class PlayerDefinition
    {
        [Min(1)]
        public int playerId = 1;

        public string displayName = "Player";

        public Color colour = Color.white;

        public PaddleSide side = PaddleSide.Left;

        public InputSourceType inputSource = InputSourceType.Keyboard;
    }
}
