using UnityEngine;

namespace SSRP.Core
{
    [CreateAssetMenu(fileName = "MatchConfig", menuName = "SSRP/Configs/Match Config")]
    public class MatchConfig : ScriptableObject
    {
        [Min(1)]
        public int targetScore = 5;
    }
}
