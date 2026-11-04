using System.Collections.Generic;
using UnityEngine;

namespace SSRP.Arena
{
    /// <summary>[D4] The list of arenas offered in Match Setup.</summary>
    [CreateAssetMenu(fileName = "ArenaRegistry", menuName = "SSRP/Arena/Arena Registry")]
    public class ArenaRegistry : ScriptableObject
    {
        public List<ArenaDefinition> arenas = new List<ArenaDefinition>();

        public int Count => arenas.Count;

        /// <summary>The arena with this id, or the first arena if none matches.</summary>
        public ArenaDefinition Get(string id)
        {
            int index = IndexOf(id);

            if (index >= 0)
                return arenas[index];

            return arenas.Count > 0 ? arenas[0] : null;
        }

        public int IndexOf(string id)
        {
            for (int i = 0; i < arenas.Count; i++)
            {
                if (arenas[i] != null && arenas[i].id == id)
                    return i;
            }

            return -1;
        }
    }
}
